using System.Net.Http.Headers;
using System.Text.Json;
using YoutubeDigest.Models;

namespace YoutubeDigest.Services;

public class SubscriptionService(IHttpClientFactory factory, ILogger<SubscriptionService> logger)
{
    private HttpClient HttpClient { get; } = factory.CreateClient("YouTube");

    public async Task<List<FeedVideo>> GetSubscriptionFeed(
        string accessToken, 
        int maxChannels = 10, 
        int videosPerChannel = 5, 
        CancellationToken ct = default)
    {
        var channelIds = await GetSubscribedChannelIds(accessToken, maxChannels, ct);
        if (channelIds.Count == 0) return [];

        var uploadsPlaylists = await GetUploadsPlaylistIds(channelIds, accessToken, ct);

        var allVideos = new List<FeedVideo>();
        foreach (var (channelName, playlistId) in uploadsPlaylists)
        {
            var videos = await GetRecentVideos(playlistId, channelName, accessToken, videosPerChannel, ct);
            allVideos.AddRange(videos);
        }

        return allVideos.OrderByDescending(x => x.PublishedAt).ToList();
    }

    private async Task<List<string>> GetSubscribedChannelIds(string accessToken, int maxChannels, CancellationToken ct)
    {
        var url = $"https://www.googleapis.com/youtube/v3/subscriptions?part=snippet&mine=true&maxResults={maxChannels}&order=relevance";
        using var req = AuthorisedGet(url, accessToken);
        var response = await HttpClient.SendAsync(req, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return [];

        return items.EnumerateArray()
            .Select(i => i.GetProperty("snippet").GetProperty("resourceId").GetProperty("channelId").GetString())
            .Where(id => id != null)
            .Cast<string>()
            .ToList();
    }

    private async Task<List<(string channelName, string playlistId)>> GetUploadsPlaylistIds(
        List<string> channelIds, 
        string accessToken, 
        CancellationToken ct)
    {
        var ids = string.Join(",", channelIds);
        var url = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,contentDetails&id={ids}";
        using var request = AuthorisedGet(url, accessToken);
        var response = await HttpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return [];

        var result = new List<(string, string)>();
        foreach (var item in items.EnumerateArray())
        {
            var channelName = item.GetProperty("snippet").GetProperty("title").GetString() ?? "";
            var playlistId = item
                .GetProperty("contentDetails")
                .GetProperty("relatedPlaylists")
                .GetProperty("uploads")
                .GetString();

            if (playlistId != null)
                result.Add((channelName, playlistId));
        }
        return result;
    }

    private async Task<List<FeedVideo>> GetRecentVideos(
        string playlistId, 
        string channelName, 
        string accessToken, 
        int maxResults, 
        CancellationToken ct)
    {
        var url = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&playlistId={playlistId}&maxResults={maxResults}";
        using var req = AuthorisedGet(url, accessToken);
        var response = await HttpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return [];

        var videos = new List<FeedVideo>();
        foreach (var item in items.EnumerateArray())
        {
            var snippet = item.GetProperty("snippet");
            var videoId = snippet.GetProperty("resourceId").GetProperty("videoId").GetString() ?? "";
            var title = snippet.GetProperty("title").GetString() ?? "";

            if (string.IsNullOrEmpty(videoId) || title is "Private video" or "Deleted video") continue;

            var publishedAt = snippet.GetProperty("publishedAt").GetString() ?? "";
            var thumbnailUrl = GetBestThumbnail(snippet);

            videos.Add(new FeedVideo
            {
                VideoId = videoId,
                Title = title,
                ChannelName = channelName,
                ThumbnailUrl = thumbnailUrl,
                PublishedAt = publishedAt
            });
        }
        return videos;
    }

    private static string GetBestThumbnail(JsonElement snippet)
    {
        if (!snippet.TryGetProperty("thumbnails", out var thumbs)) return "";
        foreach (var quality in new[] { "maxres", "standard", "high", "medium", "default" })
        {
            if (thumbs.TryGetProperty(quality, out var t))
            {
                return t.GetProperty("url").GetString() ?? "";
            }
        }
        return "";
    }

    private static HttpRequestMessage AuthorisedGet(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
