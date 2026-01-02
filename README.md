# JellyPodcast

> Universal podcast support for Jellyfin - Listen to any podcast in your media server

![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.x-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

JellyPodcast brings seamless podcast integration to Jellyfin. Simply add RSS feed URLs and the plugin automatically organizes your podcasts as TV shows, complete with artwork, metadata, and automatic updates.

## ✨ Features

- 🎙️ **Universal Support** - Works with any RSS/Atom podcast feed
- 🍎 **Apple Podcasts** - Paste Apple Podcasts URLs directly (auto-converts to RSS)
- 📺 **TV Show Organization** - Podcasts appear as series with proper episode ordering
- 🔄 **Auto-Refresh** - New episodes every 2 hours
- 🎨 **Rich Metadata** - Episode descriptions, artwork, and thumbnails
- 🌐 **Web Player Compatible** - Audio podcasts work smoothly in browser
- 📱 **Mobile Ready** - Native playback on Jellyfin mobile apps
- 🎬 **Video Support** - Works with both audio and video podcasts
- 🧹 **Auto-Cleanup** - Optionally remove old episodes automatically

## 🚀 Quick Start

### 1. Installation

**Option A: From Release (Recommended)**

1. Download `Jellyfin.Plugin.Podcast.dll` from [latest release](https://github.com/JHoahg/JellyPodcast/releases)
2. Create plugin directory:
   - **Docker**: `/config/plugins/Podcast_1.0.0.0/`
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\Podcast_1.0.0.0\`
   - **Linux**: `/var/lib/jellyfin/plugins/Podcast_1.0.0.0/`
   - **macOS**: `/Users/<username>/Library/Application Support/jellyfin/plugins/Podcast_1.0.0.0/`
3. Copy the DLL to this directory
4. Restart Jellyfin

**Option B: Build from Source**

```bash
git clone https://github.com/JHoahg/JellyPodcast.git
cd JellyPodcast
dotnet build --configuration Release
# Copy bin/Release/net9.0/Jellyfin.Plugin.Podcast.dll to plugins directory
```

### 2. Configuration

1. Go to **Jellyfin Dashboard** → **Plugins** → **Podcast**
2. Set **Library Path**:
   - **Docker**: `/config/data/podcasts`
   - **Other**: Choose any directory (will be created automatically)
3. Add podcast feeds by clicking **"Add Feed"**:
   - Paste RSS feed URL or Apple Podcasts URL
   - Optionally set a custom name
   - Click the checkbox to enable
4. Click **Save**

### 3. Initial Setup

1. Go to **Dashboard** → **Scheduled Tasks**
2. Find **"Refresh Podcast Library"** and click **▶ Run Now**
3. Wait for completion (check progress in task status)
4. Go to **Dashboard** → **Libraries** → **Add Library**
   - Content type: **Shows**
   - Folder: Point to your Library Path (e.g., `/config/data/podcasts`)
5. Scan the library
6. Your podcasts will appear in the home screen!

## 📖 Usage Guide

### Adding Podcasts

**Finding RSS Feeds:**
- **Apple Podcasts**: Just paste the Apple Podcasts URL (e.g., `https://podcasts.apple.com/podcast/id123456`)
- **Podcast Websites**: Look for RSS icon or "Subscribe" link
- **Podcast Directories**: Most provide RSS feed URLs

**Supported Feed Formats:**
- RSS 2.0 with iTunes extensions ✅
- Atom feeds ✅
- Apple Podcasts URLs ✅

### Web Player Compatibility Mode

Choose how audio podcasts are handled:

- **Auto (Recommended)**: Wraps audio in video for smooth web playback
- **Always Enable**: Forces video wrapping (uses server CPU)
- **Always Disable**: Direct audio URLs (web player may not work)

💡 **Tip**: Leave on "Auto" for best experience across all devices.

⚠️ **Important**: After changing this setting:
1. Click "Clean Up All Episodes" button
2. Run "Refresh Podcast Library" task

### Episode Management

**Auto-Cleanup:**
- Enable to automatically delete old episodes
- Set retention period (default: 30 days)
- Keeps your library fresh

**Manual Cleanup:**
- Click "Clean Up All Episodes" to remove all episode files
- Podcast metadata is preserved
- Run "Refresh Podcast Library" to repopulate

**Episode Limits:**
- Set max episodes per podcast (default: 50)
- Only newest episodes are kept
- Perfect for daily news podcasts

## 🔧 Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| **Library Path** | Where podcast files are stored | `/config/data/podcasts` |
| **Web Player Compatibility** | How to handle audio playback | Auto |
| **Max Episodes Per Podcast** | Episode limit per feed | 50 |
| **Download Thumbnails** | Download episode artwork | Enabled |
| **Auto Cleanup** | Remove old episodes automatically | Disabled |
| **Days to Keep Episodes** | Retention period for cleanup | 30 days |

## ❓ Troubleshooting

### Podcasts Not Appearing

**Check these steps:**
1. Verify Library Path is correct
2. Ensure library content type is "Shows" (not Movies)
3. Run "Refresh Podcast Library" task
4. Check Jellyfin logs: `docker logs jellyfin | grep -i podcast`
5. Verify files exist: `docker exec jellyfin ls /config/data/podcasts/`

### Audio Podcasts Won't Play in Web Player

The plugin includes automatic web player compatibility:
- Ensure "Web Player Compatibility Mode" is set to "Auto" or "Always Enable"
- Mobile and desktop apps work natively without this

### Feed Not Updating

**Common causes:**
- Feed URL is incorrect or changed
- Feed requires authentication (not supported yet)
- Feed disabled in settings
- Check: Dashboard → Scheduled Tasks → View refresh task logs

### Path Issues (Docker)

**Critical**: Use container paths, not host paths!
- ✅ Correct: `/config/data/podcasts`
- ❌ Wrong: `/storage/jellyfin/config/data/podcasts`

## 🌟 Example Feeds

Try these to get started:

- **NPR News Now**: `https://feeds.npr.org/500005/podcast.xml`
- **ZEIT Nur eine Frage**: `https://newsfeed.zeit.de/serie/nur-eine-frage`
- **Or paste any Apple Podcasts URL!**

## 🤝 Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development setup
- Code guidelines
- Pull request process
- Architecture documentation

## 📚 Documentation

- **[CLAUDE.md](CLAUDE.md)** - Detailed developer documentation
- **[RESEARCH.md](RESEARCH.md)** - Background on web player compatibility
- **[TODO.md](TODO.md)** - Planned features and improvements

## 🐛 Support

- **Bug Reports**: [Open an issue](https://github.com/JHoahg/JellyPodcast/issues)
- **Feature Requests**: [Start a discussion](https://github.com/JHoahg/JellyPodcast/discussions)
- **Community**: [Jellyfin Forums](https://forum.jellyfin.org/)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with the [Jellyfin Plugin SDK](https://github.com/jellyfin/jellyfin-plugin-sdk)
- Inspired by the amazing Jellyfin community

---

**Enjoying JellyPodcast?** ⭐ Star the repo to show your support!
