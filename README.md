# YouTube Digest

An AI-powered YouTube tool built with Blazor Server. Paste any YouTube URL for an instant AI summary — or sign in with Google to browse your subscription feed as a text-based dashboard.

Feel free to test it out here:
https://youtubedigest-production-568f.up.railway.app/

<img width="1902" height="655" alt="image" src="https://github.com/user-attachments/assets/ddf9c60d-b800-4c41-9539-7874c9b781ed" />

## Features

- **AI Summaries** — Generates a detailed summary of the video content using the Cerebras API (Qwen 3 235B)
- **Subscription Feed** — Sign in with Google to see a YouTube-style grid of recent uploads from your subscribed channels; click any video to read its AI summary instead of watching it
- **Video Metadata** — Displays title, channel, publish date, view count, likes, and comment count
- **Sentiment Analysis** — Analyzes top comments and classifies overall audience sentiment (Positive, Negative, Mixed, or Neutral)
- **Full Transcript** — Shows the complete video transcript in an expandable section
- **Dark Mode** — Toggle between light and dark themes
- **Parallel Processing** — Transcript extraction and metadata/comment fetching run concurrently for speed

## Tech Stack

- .NET 8 / ASP.NET Core
- Blazor Server (interactive server-side rendering)
- [MudBlazor](https://mudblazor.com/) — Material Design component library
- YouTube Data API v3
- Google OAuth 2.0
- Cerebras AI API

## Getting Started

### Prerequisites

- .NET 8 SDK
- A [YouTube Data API v3](https://console.cloud.google.com/) key
- A [Cerebras](https://cerebras.ai/) API key
- A [Google OAuth 2.0 client](https://console.cloud.google.com/) *(optional — required for the subscription feed)*

### Configuration

Add your keys to `YoutubeDigest/appsettings.Development.json` (or via environment variables in production):

```json
{
  "YouTube": {
    "ApiKey": "YOUR_YOUTUBE_API_KEY"
  },
  "Cerebras": {
    "ApiKey": "YOUR_CEREBRAS_API_KEY"
  },
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  }
}
```

> - The YouTube API key is optional for the Analyse page — transcripts still work without it, but metadata and comments won't be available.
> - The Google OAuth credentials are optional — without them the app runs normally but the "My Feed" page will be unavailable.

#### Setting up Google OAuth (for the subscription feed)

1. Go to [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Services** → **Credentials**
2. Create an **OAuth 2.0 Client ID** (Web application)
3. Add `http://localhost:5243/signin-google` as an authorised redirect URI (adjust port as needed)
4. Enable the **YouTube Data API v3** for the project
5. Under **OAuth consent screen** → **Test users**, add your Google account

### Run

```bash
dotnet run --project YoutubeDigest
```

Then open your browser to `http://localhost:5243`.
