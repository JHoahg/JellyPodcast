# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Jellyfin plugin that provides universal podcast support via RSS/Atom feeds. Users add feed URLs in the configuration page, and the plugin automatically fetches episodes, creates .strm files for streaming, and organizes podcasts as TV series with episodes.

**Technology Stack:**
- C# targeting .NET 9.0
- Jellyfin 10.11.x
- RSS/Atom XML parsing

## Architecture

### Core Components

**Api/PodcastFeedParser.cs**
- Parses RSS 2.0 and Atom feeds
- Supports iTunes podcast extensions
- Automatically resolves Apple Podcasts URLs to RSS feeds
- Extracts episode metadata (title, description, media URL, duration, thumbnails)
- Correctly handles duration parsing (seconds first, then HH:MM:SS)
- Returns structured `PodcastFeedData` and `PodcastEpisode` objects

**Services/PodcastLibraryManager.cs**
- Main library management logic
- Fetches all enabled podcast feeds
- Creates folder structure: one folder per podcast with "Season 1" subfolder
- Generates files for each episode:
  - `.strm` - Contains direct media URL
  - `.nfo` - Episode metadata with season/episode tags
  - `-thumb.jpg` - Episode thumbnail
- Creates podcast-level metadata:
  - `tvshow.nfo` - Series metadata
  - `folder.jpg` - Podcast artwork
- Handles auto-cleanup of old episodes (scans Season folders)

**ScheduledTasks/RefreshPodcastLibraryTask.cs**
- Scheduled task that runs every 2 hours
- Calls `PodcastLibraryManager.RefreshLibraryAsync()`
- Can be manually triggered from Jellyfin dashboard

**Configuration/PluginConfiguration.cs**
- User settings:
  - `PodcastFeeds` - List of feed configurations (URL, custom name, enabled status)
  - `LibraryPath` - Where .strm files are created
  - `PreferredQuality` - Audio quality preference
  - `MaxEpisodesPerPodcast` - Episode limit per feed
  - `EnableAutoCleanup` / `DaysToKeepEpisodes` - Cleanup settings
  - `DownloadThumbnails` - Toggle thumbnail downloads

**Configuration/configPage.html**
- Web UI for plugin configuration
- Allows adding/removing podcast feeds dynamically
- Each feed has: URL, custom name (optional), enabled checkbox
- JavaScript handles dynamic feed list management

**Plugin.cs**
- Main plugin entry point
- Registers services via `PluginServiceRegistrator`
- Provides configuration page reference

## Feed Parsing

### Apple Podcasts URL Support
The parser automatically detects Apple Podcasts URLs (`podcasts.apple.com`):
- Fetches the Apple Podcasts web page
- Extracts the RSS feed URL from the page's JSON data (looks for `"feedUrl":"..."`)
- Uses the extracted RSS feed URL for parsing
- Falls back to original URL if extraction fails
- Logs the resolution process for debugging

### RSS 2.0 with iTunes Extensions
The parser looks for:
- `<channel>` → Podcast metadata
- `<item>` → Episodes
- `<enclosure url="..." type="..." length="...">` → Media URL
- `<itunes:image href="...">` → Artwork
- `<itunes:duration>` → Episode duration (in seconds or HH:MM:SS format)
- `<itunes:author>` → Podcast author

### Atom
The parser looks for:
- `<feed>` → Podcast metadata
- `<entry>` → Episodes
- `<link rel="enclosure" href="..." type="...">` → Media URL

### Important Details
- Episodes without `<enclosure>` tags are skipped (no media to stream)
- Episode dates parsed from `<pubDate>` (RSS) or `<published>` (Atom)
- GUID used for episode identification (deduplication)
- Duration parsing: tries integer seconds first, then HH:MM:SS format (fixes TimeSpan parsing bug)
- Falls back gracefully when optional fields are missing

## File Organization

### Directory Structure
```
LibraryPath/
├── Podcast Name 1/
│   ├── folder.jpg                           # Podcast artwork
│   ├── tvshow.nfo                          # Series metadata
│   └── Season 1/
│       ├── 2025-01-15 - Episode Title.strm
│       ├── 2025-01-15 - Episode Title.nfo
│       ├── 2025-01-15 - Episode Title-thumb.jpg
│       └── ...
├── Podcast Name 2/
│   └── Season 1/
│       └── ...
```

### File Naming
- Pattern: `{YYYY-MM-DD} - {Sanitized Episode Title}.{ext}`
- Podcast folders use custom name if provided, otherwise feed title
- Invalid filename characters replaced with `_` or `-`
- Long filenames truncated to 200 characters

### NFO Format
**tvshow.nfo** (podcast level):
```xml
<tvshow>
  <title>Podcast Name</title>
  <plot>Description</plot>
  <studio>Author</studio>
  <genre>Podcast</genre>
  <thumb>Image URL</thumb>
</tvshow>
```

**episodedetails.nfo** (episode level):
```xml
<episodedetails>
  <title>Episode Title</title>
  <plot>Description</plot>
  <season>1</season>
  <episode>1</episode>
  <aired>2025-01-15</aired>
  <year>2025</year>
  <runtime>45</runtime>
  <genre>Podcast</genre>
  <thumb>Image URL</thumb>
</episodedetails>
```

## Build & Deployment

### Build Commands

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release
```

Build output: `bin/Debug/net9.0/Jellyfin.Plugin.Podcast.dll`

### Deployment to Remote Server

```bash
# Build and deploy (adjust server and paths for your setup)
dotnet build && \
scp bin/Debug/net9.0/Jellyfin.Plugin.Podcast.dll user@server:/path/to/jellyfin/plugins/Podcast_1.0.0.0/ && \
ssh user@server "docker restart jellyfin"

# Wait for Jellyfin to restart
sleep 20

# Check plugin loaded
ssh user@server "docker logs jellyfin 2>&1 | grep -i 'loaded plugin: podcast'"
```

### Testing

After deployment:
1. Go to Jellyfin Dashboard → Plugins → Podcast
2. Add podcast feed URLs
3. Go to Dashboard → Scheduled Tasks
4. Run "Refresh Podcast Library" manually
5. Check logs: `docker logs jellyfin 2>&1 | grep -i podcast`
6. Verify files: `ls -la /config/data/podcasts/`
7. Create/scan Jellyfin library pointing to the podcast directory

## Common Development Tasks

### Adding Debug Logging

```csharp
_logger.LogInformation("Processing feed: {Url}", feedUrl);
_logger.LogDebug("Found {Count} episodes", episodes.Count);
_logger.LogWarning("Failed to download thumbnail: {Url}", imageUrl);
_logger.LogError(ex, "Error parsing feed: {Url}", feedUrl);
```

### Adding New Configuration Options

1. Add property to `Configuration/PluginConfiguration.cs`
2. Add UI control in `Configuration/configPage.html`
3. Add JavaScript to load/save in configPage.html
4. Access via `Plugin.Instance?.Configuration?.PropertyName`

### Supporting New Feed Elements

1. Modify `PodcastFeedParser.ParseRssFeed()` or `ParseAtomFeed()`
2. Add new properties to `PodcastFeedData` or `PodcastEpisode`
3. Update NFO generation in `PodcastLibraryManager`
4. Add XML documentation to avoid build warnings

### Handling Feed-Specific Quirks

Some feeds may have non-standard formats. Add special handling in the parser:
```csharp
// Example: Some feeds use description instead of summary
var description = item.Element("description")?.Value
                ?? item.Element(itunesNs + "summary")?.Value
                ?? string.Empty;
```

## Debugging

**View Jellyfin logs:**
```bash
# Docker
docker logs jellyfin 2>&1 | tail -100
docker logs jellyfin 2>&1 | grep -i podcast

# System service
journalctl -u jellyfin -f
```

**Check created files:**
```bash
ls -la /config/data/podcasts/
find /config/data/podcasts -name "*.strm" | head -10
cat "/config/data/podcasts/Some Podcast/2025-01-15 - Episode.strm"
```

**Test feed parsing:**
```bash
# Download and inspect feed
curl -s "https://feeds.example.com/podcast.xml" | head -100

# Check for enclosure tags
curl -s "https://feeds.example.com/podcast.xml" | grep -i enclosure

# Validate XML
curl -s "https://feeds.example.com/podcast.xml" | xmllint --format - | head -50
```

**Test .strm files in Jellyfin:**
1. Ensure library is set to "Shows" content type
2. Scan library
3. Check if episodes appear with correct metadata
4. Try playing an episode to verify stream URL works

## Important Implementation Details

**File Existence Checks**
- All file creation methods check `File.Exists()` before writing
- Prevents re-downloading thumbnails and regenerating metadata
- Allows safe re-running of refresh task

**Filename Sanitization**
- Invalid path characters replaced to prevent I/O errors
- Colons → dashes, slashes → dashes, quotes → apostrophes
- Special characters removed: `? * " < > |`
- Filenames limited to 200 characters

**Episode Limits**
- Episodes sorted by pub date (newest first)
- Only top N episodes processed (based on `MaxEpisodesPerPodcast`)
- Prevents unlimited growth for long-running podcasts

**Auto-Cleanup**
- Based on file creation time, not episode pub date
- Deletes all associated files (.strm, .nfo, -thumb.jpg)
- Scans Season folders for old episodes
- Removes empty season directories after cleanup
- Runs during refresh task if enabled

**Dependency Injection**
- All Jellyfin package references must include: `<ExcludeAssets>runtime</ExcludeAssets>`
- Services registered in `PluginServiceRegistrator.RegisterServices()`
- Use constructor injection for `IHttpClientFactory`, `ILogger`

**Error Handling**
- Feed parsing errors logged but don't stop other feeds from processing
- Missing enclosures → episode skipped
- Failed thumbnail downloads → logged as warning, continue
- Invalid XML → returns null, logs error

## Version Compatibility

**Critical Version Constraints:**
- Jellyfin.Controller: 10.11.0 (must match server version)
- Jellyfin.Model: 10.11.0 (must match server version)
- .NET: 9.0 (Jellyfin 10.11.x requires .NET 9)

When upgrading Jellyfin server, update package versions in `.csproj` to match.

## Known Limitations

- No support for password-protected feeds (HTTP auth not implemented)
- No support for Spotify proprietary formats (Spotify doesn't provide RSS feeds)
- Video podcasts work but no special handling for video-specific metadata
- No built-in search/discovery (user must provide feed URLs)
- Feed refresh interval fixed at 2 hours (not user-configurable)
- All episodes are placed in "Season 1" regardless of publication date
- Web player uses server CPU for real-time audio transcoding (mobile/desktop clients play audio directly without overhead)

## Critical Path Mapping Issue (Docker)

**VERY IMPORTANT**: The plugin creates files using the configured `LibraryPath`. For Docker deployments:

- **Correct path**: `/config/data/podcasts` (container perspective)
- **Wrong path**: `/storage/jellyfin/config/data/podcasts` (also works inside container but wrong)

Docker mounts `/storage/jellyfin/config` (host) to `/config` (container). The plugin runs inside the container, so:
- Files created at `/config/data/podcasts` are accessible to Jellyfin
- Files created at `/storage/jellyfin/config/data/podcasts` exist but in wrong location from Jellyfin's view

**Testing**: Always verify with:
```bash
docker exec jellyfin ls -la /config/data/podcasts/
```

## Testing Procedure

1. **Build**: `dotnet build --configuration Release`
2. **Deploy**: Copy DLL to your Jellyfin plugins directory (e.g., `<jellyfin-data>/plugins/Podcast_1.0.0.0/`)
3. **Restart**: Restart Jellyfin and wait ~25 seconds
4. **Configure**: Set `LibraryPath` to `/config/data/podcasts` (Docker) or your podcasts directory
5. **Add Feeds**: Use real RSS feed URLs (not Apple Podcasts/Spotify URLs)
6. **Refresh**: Dashboard → Scheduled Tasks → "Refresh Podcast Library" → Run
7. **Verify Files**: `docker exec jellyfin ls -la /config/data/podcasts/`
8. **Create Library**: Dashboard → Libraries → Add → Shows → `/config/data/podcasts`
9. **Scan**: Scan library and check if episodes appear

## Tested Feeds

**Working**:
- NPR News Now: `https://feeds.npr.org/500005/podcast.xml` (audio - works on mobile/desktop clients)
- ZEIT Nur eine Frage: `https://newsfeed.zeit.de/serie/nur-eine-frage` (video - works everywhere)
- OK America: Apple Podcasts URL (audio - works on mobile/desktop clients)

**Playback Notes**:
- Video podcasts play in all Jellyfin clients including web player
- Audio-only podcasts automatically adapt to the client type

## Automatic Client Detection

### Overview

The plugin automatically detects the client type and adapts playback accordingly:
- **Web player**: Wraps audio in WebM video with static artwork for smooth playback
- **Mobile/Desktop clients**: Redirects to direct audio URL for native playback
- **Video podcasts**: Always use direct URL (all clients)

### How It Works

1. For all audio podcasts, creates `.audiourl` files containing the original audio URL
2. `.strm` files point to `http://localhost:8096/Podcast/Stream/{episodeId}` (controller endpoint)
3. `PodcastStreamController` receives playback requests and:
   - Parses `Authorization` header to extract client information (`Client="Jellyfin Web"` vs `Client="Android"`, etc.)
   - **If web client**: Wraps audio in WebM with VP8/Opus codecs
   - **If mobile/desktop**: Returns HTTP redirect to direct audio URL
4. For web player, Jellyfin transcodes WebM to HLS for seekable playback

### Client Detection Method

The controller uses the `Authorization` header format:
```
Authorization: MediaBrowser Client="Jellyfin Web", Device="Firefox", Version="10.11.0"
```

Extracts the `Client` parameter:
- `"Jellyfin Web"` → Web player → Wrap audio in video
- `"Android"`, `"iOS"`, `"Android TV"`, etc. → Mobile/Desktop → Direct audio URL

### Technical Details

**FFmpeg command**:
```bash
ffmpeg -loop 1 -i "thumbnail.jpg" -i "audio_url" \
  -c:v libvpx -b:v 100k -deadline realtime -cpu-used 8 \
  -c:a libopus -b:a 128k \
  -shortest -f webm pipe:1
```

**Why WebM?**
- Streams efficiently through pipes (no MP4 moov atom issues)
- Real-time encoding fast enough for live transcoding
- Browser-compatible codecs (VP8/Opus)
- Jellyfin can transcode to HLS for seeking

### Performance

- CPU usage: Moderate (real-time encoding per concurrent stream)
- Tested: Stable for full-length episodes with smooth seeking

## Common Development Issues

### Issue: Files Created But Not Appearing in Jellyfin

**Cause**: Jellyfin expects TV shows to have Season folder structure

**Solution**: Plugin now creates Season 1 folders:
```
/Podcast Name/
  /Season 1/
    episode.strm
    episode.nfo
```

**Status**: Fixed - all episodes are organized in Season 1 folders

### Issue: Config UI Shows JavaScript Literals

**Symptoms**: Feed URL shows `feed.Url || ''` instead of actual value

**Cause**: Template literals in innerHTML not evaluated in Jellyfin's browser environment

**Fix**: Replaced template literals with `createElement()` and DOM manipulation (fixed in current version)

### Issue: ZEIT Feed Has Images Instead of Audio

**Observation**: ZEIT RSS feeds sometimes have image URLs in `<enclosure>` tags instead of audio URLs

**Impact**: Episodes created but .strm files point to images, won't play

**Status**: Feed-specific issue, not plugin bug

## Future Enhancements

Potential features to add:
- Feed authentication support (HTTP Basic Auth)
- Custom refresh intervals per feed
- Episode filtering (by date range, keywords)
- Download episodes locally instead of streaming
- Support for podcast chapters
- OPML import/export for feed management
- Integration with podcast directories for discovery
