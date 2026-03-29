using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using YoutubeDigest.Models;
using YoutubeExplode;

namespace YoutubeDigest.Services;

public class YouTubeService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<YouTubeService> logger)
{
    private HttpClient HttpClient { get; } = httpClientFactory.CreateClient("YouTube");
    private YoutubeClient YoutubeClient { get; } = new YoutubeClient();

    public static string? ExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var shortMatch = Regex.Match(url, @"youtu\.be/([a-zA-Z0-9_-]{11})");
        if (shortMatch.Success)
        {
            return shortMatch.Groups[1].Value;
        }

        var longMatch = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");
        if (longMatch.Success)
        {
            return longMatch.Groups[1].Value;
        }

        var embedMatch = Regex.Match(url, @"(?:embed|shorts)/([a-zA-Z0-9_-]{11})");
        if (embedMatch.Success)
        {
            return embedMatch.Groups[1].Value;
        }

        return null;
    }

    public async Task<string> GetTranscript(string videoId, CancellationToken ct = default)
    {
        try
        {
            var trackManifest = await YoutubeClient.Videos.ClosedCaptions.GetManifestAsync(videoId, ct);

            var track = trackManifest.TryGetByLanguage("en")
                ?? trackManifest.Tracks.FirstOrDefault()
                ?? throw new InvalidOperationException("No captions found for this video. It may not have subtitles enabled.");

            var captions = await YoutubeClient.Videos.ClosedCaptions.GetAsync(track, ct);

            return string.Join(" ", captions.Captions.Select(x => x.Text));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex.Message.Contains("not available"))
        {
            throw new InvalidOperationException("This video is not available. It may be private, region-locked, or age-restricted.");
        }
    }

    public async Task<VideoAnalysis> GetVideoMetadata(string videoId, CancellationToken ct = default)
    {
        var apiKey = config["YouTube:ApiKey"];
        var result = new VideoAnalysis { VideoId = videoId };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("YouTube API key not configured; skipping metadata fetch.");
            return result;
        }

        var videoUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics&id={videoId}&key={apiKey}";
        var videoResponse = await HttpClient.GetStringAsync(videoUrl, ct);

        using var doc = JsonDocument.Parse(videoResponse);
        var items = doc.RootElement.GetProperty("items");

        if (items.GetArrayLength() == 0)
        {
            return result;
        }

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        var stats = item.GetProperty("statistics");

        result.Title = snippet.GetProperty("title").GetString() ?? "";
        result.ChannelName = snippet.GetProperty("channelTitle").GetString() ?? "";
        result.PublishedAt = snippet.GetProperty("publishedAt").GetString() ?? "";

        if (snippet.TryGetProperty("thumbnails", out var thumbnails) && thumbnails.TryGetProperty("maxres", out var maxres))
        {
            result.ThumbnailUrl = maxres.GetProperty("url").GetString() ?? "";
        }
            
        else if (snippet.TryGetProperty("thumbnails", out var thumbs2) && thumbs2.TryGetProperty("high", out var high))
        {
            result.ThumbnailUrl = high.GetProperty("url").GetString() ?? "";
        }

        if (stats.TryGetProperty("viewCount", out var views) && long.TryParse(views.GetString(), out var viewCount))
        {
            result.ViewCount = viewCount;
        }

        if (stats.TryGetProperty("likeCount", out var likes) && long.TryParse(likes.GetString(), out var likeCount))
        {
            result.LikeCount = likeCount;
        }

        if (stats.TryGetProperty("commentCount", out var comments) && long.TryParse(comments.GetString(), out var commentCount))
        {
            result.CommentCount = commentCount;
        }

        return result;
    }

    public async Task<List<VideoComment>> GetTopComments(string videoId, int maxResults = 5, CancellationToken ct = default)
    {
        var apiKey = config["YouTube:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("YouTube API key not configured; skipping comments fetch.");
            return [];
        }

        var url = $"https://www.googleapis.com/youtube/v3/commentThreads?part=snippet&videoId={videoId}&order=relevance&maxResults={maxResults}&key={apiKey}";
        var json = await HttpClient.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        var result = new List<VideoComment>();

        foreach (var item in items.EnumerateArray())
        {
            var top = item.GetProperty("snippet").GetProperty("topLevelComment").GetProperty("snippet");
            var text = top.GetProperty("textDisplay").GetString() ?? "";
            var author = top.GetProperty("authorDisplayName").GetString() ?? "";
            long likes = 0;
            if (top.TryGetProperty("likeCount", out var likeCount))
            {
                likes = likeCount.GetInt64();
            }

            result.Add(new VideoComment { Author = author, Text = WebUtility.HtmlDecode(text), LikeCount = likes });
        }

        return result;
    }
}
