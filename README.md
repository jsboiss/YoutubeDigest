# YouTube Digest

An AI-powered YouTube video analysis tool built with Blazor Server. Paste a YouTube URL and get an instant summary, video stats, top comments, and audience sentiment analysis.

Feel free to test it out here:
https://youtubedigest-production-568f.up.railway.app/

<img width="1902" height="655" alt="image" src="https://github.com/user-attachments/assets/ddf9c60d-b800-4c41-9539-7874c9b781ed" />

## Features

- **AI Summaries** — Generates a detailed summary of the video content using the Cerebras API (Llama 3.3-70B)
- **Video Metadata** — Displays title, channel, publish date, view count, likes, and comment count
- **Sentiment Analysis** — Analyzes top comments and classifies overall audience sentiment (Positive, Negative, Mixed, or Neutral)
- **Full Transcript** — Shows the complete video transcript in an expandable section
- **Parallel Processing** — Transcript extraction and metadata/comment fetching run concurrently for speed

## Tech Stack

- .NET 8 / ASP.NET Core
- Blazor Server (interactive server-side rendering)
- [MudBlazor](https://mudblazor.com/) — Material Design component library
- YouTube Data API v3
- Cerebras AI API

## Getting Started

### Prerequisites

- .NET 8 SDK
- A [YouTube Data API v3](https://console.cloud.google.com/) key
- A [Cerebras](https://cerebras.ai/) API key

### Configuration

Add your API keys to `YoutubeDigest/appsettings.json`:

```json
{
  "YouTube": {
    "ApiKey": "YOUR_YOUTUBE_API_KEY"
  },
  "Cerebras": {
    "ApiKey": "YOUR_CEREBRAS_API_KEY"
  }
}
```

> The YouTube API key is optional — the app will still summarize transcripts without it, but video metadata and comments will not be available.

### Run

```bash
dotnet run --project YoutubeDigest
```

Then open your browser to `https://localhost:5001`.
