using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using YoutubeDigest.Models;

namespace YoutubeDigest.Services;

public class CerebrasService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<CerebrasService> logger)
{
    private const string BaseUrl = "https://api.cerebras.ai/v1/chat/completions";
    private const string Model = "qwen-3-235b-a22b-instruct-2507";
    private const int MaxTranscriptChars = 180000;

    private HttpClient HttpClient { get; } = httpClientFactory.CreateClient("Cerebras");

    public async Task<string> DigestTranscript(string transcript, string videoTitle = "", CancellationToken ct = default)
    {
        var truncated = transcript.Length > MaxTranscriptChars
            ? transcript[..MaxTranscriptChars] + "\n\n[Transcript truncated for length]"
            : transcript;

        var titleContext = string.IsNullOrWhiteSpace(videoTitle) ? "" : $"Video title: \"{videoTitle}\"\n\n";

        var prompt = $"{titleContext}Please provide a detailed summary of the following YouTube video transcript. " +
                     "Cover the main topics, key points, and any conclusions. " +
                     "Format your response in clear paragraphs.\n\nTRANSCRIPT:\n" + truncated;

        return await PromptCerebras("You are an expert at summarizing video content accurately and thoroughly.", prompt, 1500, ct);
    }

    public async Task<(string summary, SentimentLabel label)> AnalyseCommentSentiment(List<VideoComment> comments, CancellationToken ct = default)
    {
        if (comments.Count == 0)
        {
            return ("No comments available to analyze.", SentimentLabel.Unknown);
        }

        var commentBlock = string.Join("\n---\n", comments.Select(c => $"{c.Author}: {c.Text}"));

        var prompt = "Analyze the overall audience sentiment from these YouTube comments. " +
                     "Start your response with exactly one of: \"Positive:\", \"Negative:\", \"Mixed:\", or \"Neutral:\" " +
                     "followed by a 2-3 sentence summary of what viewers think.\n\nCOMMENTS:\n" + commentBlock;

        var response = await PromptCerebras("You are an expert at analyzing audience sentiment and reactions.", prompt, 300, ct);

        var label = SentimentLabel.Neutral;
        if (response.StartsWith("Positive:", StringComparison.OrdinalIgnoreCase)) label = SentimentLabel.Positive;
        else if (response.StartsWith("Negative:", StringComparison.OrdinalIgnoreCase)) label = SentimentLabel.Negative;
        else if (response.StartsWith("Mixed:", StringComparison.OrdinalIgnoreCase)) label = SentimentLabel.Mixed;

        return (response, label);
    }

    private async Task<string> PromptCerebras(string systemPrompt, string userPrompt, int maxTokens, CancellationToken ct)
    {
        var apiKey = config["Cerebras:ApiKey"]
            ?? throw new InvalidOperationException("Cerebras API key is not configured.");

        var payload = new
        {
            model = Model,
            max_tokens = maxTokens,
            temperature = 0.3,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await HttpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Cerebras API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Cerebras API error ({response.StatusCode}): {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
