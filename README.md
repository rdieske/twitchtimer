# Twitch Stream Timer

Multi-user Twitch stream timer with customizable display colors and OBS overlay support.

## Setup

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd timer
   ```

2. **Configure Twitch Credentials**
   ```bash
   cp .env.example .env
   ```
   
   Edit `.env` and add your Twitch API credentials:
   ```
   TWITCH_CLIENT_ID=your_client_id_here
   TWITCH_CLIENT_SECRET=your_client_secret_here
   TWITCH_REDIRECT_URI=http://localhost:7283/auth/callback
   ```
   
   Get your credentials from: https://dev.twitch.tv/console/apps

3. **Run with Docker**
   ```bash
   docker compose up -d --build
   ```

4. **Access the application**
   - Main App: http://localhost:8080
   - Overlay: http://localhost:8080/overlay

## Features

- **Multi-User Support**: Each Twitch user has their own independent timer
- **Customizable Colors**: Background and text colors per user
- **OBS Overlay**: Transparent overlay for streaming software
- **Event Tracking**: Automatic tracking of Subs and Bits (when Twitch integration is configured)
- **Manual Controls**: Add events manually for testing or missed events

## Configuration

Default timer settings (can be changed per-user in Settings tab):
- **Minimum Duration**: 24 hours
- **Maximum Duration**: 60 days
- **Sub Tier 1**: 60 seconds
- **Sub Tier 2**: 120 seconds
- **Sub Tier 3**: 180 seconds
- **Bits**: 60 seconds per 1000 bits

## Development

**Requirements:**
- .NET 10 SDK
- Docker Desktop

**Local Development:**
```bash
cd TwitchStreamTimer.Web
dotnet run
```

## Security Notes

- **Never commit `.env` file** - Contains sensitive Twitch credentials
- **Never commit `Data/` folder** - Contains user-specific timer data
- Use `.env.example` as a template for new deployments
"# twitchtimer" 
