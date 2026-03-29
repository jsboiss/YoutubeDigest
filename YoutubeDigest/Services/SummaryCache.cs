using System.Collections.Concurrent;

namespace YoutubeDigest.Services;

public class SummaryCache
{
    private ConcurrentDictionary<string, string> Cache { get; } = new();

    public bool TryGet(string videoId, out string summary) => this.Cache.TryGetValue(videoId, out summary!);

    public void Set(string videoId, string summary) => this.Cache[videoId] = summary;
}
