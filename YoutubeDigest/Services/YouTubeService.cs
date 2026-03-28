using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using YoutubeDigest.Models;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;

namespace YoutubeDigest.Services;

public class YouTubeService
{
    private readonly HttpClient _http;
    private readonly YoutubeClient _youtube;
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeService> _logger;

    public YouTubeService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<YouTubeService> logger)
    {
        _http = httpClientFactory.CreateClient("YouTube");
        _youtube = new YoutubeClient();
        _config = config;
        _logger = logger;
    }

    public static string? ExtractVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var shortMatch = Regex.Match(url, @"youtu\.be/([a-zA-Z0-9_-]{11})");
        if (shortMatch.Success) return shortMatch.Groups[1].Value;

        var longMatch = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");
        if (longMatch.Success) return longMatch.Groups[1].Value;

        var embedMatch = Regex.Match(url, @"(?:embed|shorts)/([a-zA-Z0-9_-]{11})");
        if (embedMatch.Success) return embedMatch.Groups[1].Value;

        return null;
    }

    public async Task<string> GetTranscriptAsync(string videoId, CancellationToken ct = default)
    {
        var trackManifest = await _youtube.Videos.ClosedCaptions.GetManifestAsync(videoId, ct);

        var track = trackManifest.TryGetByLanguage("en")
            ?? trackManifest.Tracks.FirstOrDefault()
            ?? throw new InvalidOperationException("No captions found for this video. It may not have subtitles enabled.");

        var captions = await _youtube.Videos.ClosedCaptions.GetAsync(track, ct);

        return string.Join(" ", captions.Captions.Select(c => c.Text));
    }

    public async Task<VideoAnalysis> GetVideoMetadataAsync(string videoId, CancellationToken ct = default)
    {
        var apiKey = _config["YouTube:ApiKey"];
        var result = new VideoAnalysis { VideoId = videoId };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("YouTube API key not configured; skipping metadata fetch.");
            return result;
        }

        var videoUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics&id={videoId}&key={apiKey}";
        var videoResponse = await _http.GetStringAsync(videoUrl, ct);

        using var doc = JsonDocument.Parse(videoResponse);
        var items = doc.RootElement.GetProperty("items");

        if (items.GetArrayLength() == 0)
            return result;

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        var stats = item.GetProperty("statistics");

        result.Title = snippet.GetProperty("title").GetString() ?? "";
        result.ChannelName = snippet.GetProperty("channelTitle").GetString() ?? "";
        result.PublishedAt = snippet.GetProperty("publishedAt").GetString() ?? "";

        if (snippet.TryGetProperty("thumbnails", out var thumbnails) &&
            thumbnails.TryGetProperty("maxres", out var maxres))
            result.ThumbnailUrl = maxres.GetProperty("url").GetString() ?? "";
        else if (snippet.TryGetProperty("thumbnails", out var thumbs2) &&
                 thumbs2.TryGetProperty("high", out var high))
            result.ThumbnailUrl = high.GetProperty("url").GetString() ?? "";

        if (stats.TryGetProperty("viewCount", out var views) &&
            long.TryParse(views.GetString(), out var viewCount))
            result.ViewCount = viewCount;

        if (stats.TryGetProperty("likeCount", out var likes) &&
            long.TryParse(likes.GetString(), out var likeCount))
            result.LikeCount = likeCount;

        if (stats.TryGetProperty("commentCount", out var comments) &&
            long.TryParse(comments.GetString(), out var commentCount))
            result.CommentCount = commentCount;

        return result;
    }

    public async Task<List<VideoComment>> GetTopCommentsAsync(string videoId, int maxResults = 5, CancellationToken ct = default)
    {
        var apiKey = _config["YouTube:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("YouTube API key not configured; skipping comments fetch.");
            return new();
        }

        var url = $"https://www.googleapis.com/youtube/v3/commentThreads?part=snippet&videoId={videoId}&order=relevance&maxResults={maxResults}&key={apiKey}";
        var json = await _http.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        var result = new List<VideoComment>();

        foreach (var item in items.EnumerateArray())
        {
            var top = item.GetProperty("snippet").GetProperty("topLevelComment").GetProperty("snippet");
            var text = top.GetProperty("textDisplay").GetString() ?? "";
            var author = top.GetProperty("authorDisplayName").GetString() ?? "";
            long likes = 0;
            if (top.TryGetProperty("likeCount", out var lc)) likes = lc.GetInt64();

            result.Add(new VideoComment { Author = author, Text = WebUtility.HtmlDecode(text), LikeCount = likes });
        }

        return result;
    }
}
