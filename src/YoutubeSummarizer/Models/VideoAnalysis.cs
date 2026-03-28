namespace YoutubeSummarizer.Models;

public class VideoAnalysis
{
    public string VideoId { get; set; } = "";
    public string Title { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public string Transcript { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<VideoComment> TopComments { get; set; } = new();
    public string SentimentSummary { get; set; } = "";
    public SentimentLabel Sentiment { get; set; } = SentimentLabel.Unknown;
}

public class VideoComment
{
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public long LikeCount { get; set; }
}

public enum SentimentLabel
{
    Unknown,
    Positive,
    Negative,
    Mixed,
    Neutral
}
