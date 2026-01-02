# Contributing to JellyPodcast

Thank you for considering contributing to JellyPodcast! This document provides guidelines and information for developers.

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Jellyfin 10.11.x server (for testing)
- Basic knowledge of C# and Jellyfin plugin development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/JHoahg/JellyPodcast.git
cd JellyPodcast

# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Clean build
dotnet clean && dotnet build
```

Build output: `bin/Release/net9.0/Jellyfin.Plugin.Podcast.dll`

### Project Structure

```
JellyPodcast/
├── Api/
│   ├── PodcastFeedParser.cs        # RSS/Atom feed parsing
│   ├── PodcastStreamController.cs  # Audio wrapping for web player
│   └── PodcastCleanupController.cs # Episode cleanup endpoint
├── Configuration/
│   ├── PluginConfiguration.cs      # Plugin settings
│   └── configPage.html             # Web UI
├── Services/
│   └── PodcastLibraryManager.cs    # Library management & file creation
├── ScheduledTasks/
│   └── RefreshPodcastLibraryTask.cs # Auto-refresh task
└── Plugin.cs                        # Main plugin entry point
```

## Development Workflow

1. Make your changes
2. Test locally with a Jellyfin instance
3. Ensure code compiles without warnings
4. Update documentation if needed
5. Commit with descriptive messages
6. Submit a pull request

## Testing

### Manual Testing

1. Build the plugin
2. Copy DLL to your Jellyfin plugins directory
3. Restart Jellyfin
4. Test the changes in the Jellyfin UI

### Checking Logs

```bash
# Docker
docker logs jellyfin 2>&1 | grep -i podcast

# System service
journalctl -u jellyfin -f | grep -i podcast
```

## Code Style

- Follow standard C# conventions
- Use XML documentation comments for public methods
- Keep line length reasonable (no strict limit, but be sensible)
- Prefer clarity over cleverness

## Pull Request Guidelines

### Before Submitting

- [ ] Code compiles without errors or warnings
- [ ] Tested with actual podcast feeds
- [ ] Updated README/documentation if needed
- [ ] Commit messages are clear and descriptive

### PR Description

Please include:
- What changes were made and why
- Any breaking changes
- Testing performed
- Screenshots (if UI changes)

## Architecture Notes

### Feed Parsing

The `PodcastFeedParser` handles:
- RSS 2.0 with iTunes extensions
- Atom feeds
- Apple Podcasts URL resolution

Episode extraction looks for `<enclosure>` tags for media URLs. Episodes without enclosures are skipped.

### File Organization

Episodes are organized as TV shows:
```
Podcast Name/
├── tvshow.nfo
├── folder.jpg
└── Season 1/
    ├── YYYY-MM-DD - Episode Title.strm
    ├── YYYY-MM-DD - Episode Title.nfo
    └── YYYY-MM-DD - Episode Title-thumb.jpg
```

### Web Player Compatibility

For audio podcasts, the plugin can wrap audio in WebM video with static artwork:

1. Creates `.audiourl` files with original audio URL
2. `.strm` files point to controller endpoint
3. `PodcastStreamController` wraps audio in WebM (VP8/Opus)
4. Jellyfin transcodes to HLS for seekable playback

Video podcasts always use direct URLs.

### Scheduled Refresh

The `RefreshPodcastLibraryTask` runs every 2 hours (configurable in code):
- Fetches latest episodes from all enabled feeds
- Creates new .strm/.nfo files
- Downloads thumbnails if enabled
- Runs cleanup if enabled

## Common Development Tasks

### Adding New Configuration Options

1. Add property to `Configuration/PluginConfiguration.cs`
2. Add UI control in `Configuration/configPage.html`
3. Add JavaScript to load/save in configPage.html
4. Access via `Plugin.Instance?.Configuration?.PropertyName`

### Supporting New Feed Elements

1. Modify `PodcastFeedParser.ParseRssFeed()` or `ParseAtomFeed()`
2. Add new properties to `PodcastFeedData` or `PodcastEpisode`
3. Update NFO generation in `PodcastLibraryManager`

### Debugging Tips

**Issue: Files created but not appearing**
- Check library type (must be "Shows", not "Movies")
- Verify Season folder structure exists
- Check Jellyfin scanner logs

**Issue: Feed parsing errors**
- Test feed URL directly: `curl -s "URL" | head -100`
- Verify `<enclosure>` tags exist
- Check for valid XML: `curl -s "URL" | xmllint --format -`

## Resources

- [Jellyfin Plugin SDK](https://github.com/jellyfin/jellyfin-plugin-sdk)
- [Jellyfin Plugin Documentation](https://jellyfin.org/docs/general/server/plugins/)
- [CLAUDE.md](CLAUDE.md) - Detailed developer documentation
- [RESEARCH.md](RESEARCH.md) - Background research on audio playback issues

## Questions?

Feel free to:
- Open an issue for bugs or feature requests
- Start a discussion for general questions
- Check existing issues and discussions first

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
