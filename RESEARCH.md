# Jellyfin Podcast Plugin Research

This document contains research findings about existing Jellyfin podcast plugins and solutions, conducted January 2026.

---

## Research Summary: Jellyfin Podcast Plugins and Solutions

Based on systematic web research, here's what exists in the Jellyfin podcast ecosystem:

---

### 1. **Official/Community Podcast Plugins**

**Status**: No official podcast plugin exists, but there are a few community attempts:

**A. Jellypod Plugin (dtourolle)**
- **Repository**: https://gitea.tourolle.paris/dtourolle/jellypod
- **Status**: Early development, described as "still pretty barebones but provides a nice foundation"
- **Installation**: Should be installable via Jellyfin web interface
- **Limitation**: Very limited information available; appears to be a small personal project
- **Forum Discussion**: [Podcast manager plugin](https://forum.jellyfin.org/t-podcast-manager-plugin)

**B. Jellyfin-RSS Plugin (SamCullin)**
- **Repository**: https://github.com/SamCullin/jellyfin-rss
- **Purpose**: Brings RSS feeds into Jellyfin
- **Language**: .NET (C#)
- **Status**: Unclear activity level, limited documentation in search results

**C. Archived RSS Examples**
- **Legacy Code**: [emby-plugin-channels/iPlayer RSS.cs](https://github.com/jellyfin-archive/emby-plugin-channels/blob/master/MediaBrowser.Channels.iPlayer/RSS.cs)
- **Use**: Provides example of RSS parsing using `SyndicationFeedXmlReader` in C#

---

### 2. **Alternative Approaches Documented by Others**

**A. PodGrab Integration (Most Common Workaround)**
- **Repository**: https://github.com/akhilrex/podgrab
- **Approach**: Two-tool workflow
  - PodGrab: Self-hosted podcast manager that downloads episodes from RSS feeds
  - Jellyfin: Serves the downloaded files as a music/audiobook library
- **Features**:
  - Add podcasts via RSS URL, OMPL import, or search
  - Automatic downloads when new episodes are published
  - Built-in podcast player (can stream or play downloaded files)
  - Dark mode, Docker support, authentication
- **Integration Method**: File-based sync (PodGrab downloads to folder, Jellyfin monitors folder)
- **Limitations**: No playback state synchronization between PodGrab and Jellyfin
- **Community Discussion**: [Podgrab as a Podcast back-end](https://features.jellyfin.org/posts/1066/podgrab-as-a-podcast-back-end-for-jellyfin)

**B. Manual File Management**
- Users download podcast episodes manually and add to Jellyfin as music files
- **Issues**: No resume functionality, no easy way to mark as played/delete

**C. Lightphone-Musiccast Approach**
- **Repository**: https://github.com/nateswart/lightphone-musiccast
- **Direction**: Opposite of what most need - creates RSS feeds FROM Jellyfin playlists
- **Use Case**: Export Jellyfin playlists as podcast feeds for external players

---

### 3. **Audio Playback Issues in Web Player**

**Known Problems Documented:**

**A. .strm File Issues with Audio**
- **Critical Issue**: [Jellyfin doesn't work well with .strm files containing MP3/audio](https://github.com/jellyfin/jellyfin/discussions/13472)
- **Specific Error**: `NullReferenceException` at `GetAudioDirectPlayProfile`
- **Behavior**: .strm files work in movie libraries but fail in music libraries
- **Impact**: This is a significant blocker for streaming-based podcast plugins using .strm files for audio
- **Related**: [.strm file working in movie library but not in music library](https://github.com/jellyfin/jellyfin/issues/8201)

**B. General Audio Web Player Issues**
- [MP3/AUDIO FILES NOT PLAYING](https://github.com/jellyfin/jellyfin-web/issues/4788): Some users get "client isn't compatible with the media" errors
- [No audio device compatibility](https://github.com/jellyfin/jellyfin-web/issues/3677): Issues with certain USB audio devices
- [Audio sync issues](https://github.com/jellyfin/jellyfin/issues/9330): Out of sync during direct streaming

**C. Codec Support**
- **Web Player Supports**: MP3, AAC (well-supported across browsers)
- **Transcoding Preference**: AAC is preferred over MP3 for HLS streaming
- **Limitation**: MP3 Mono incorrectly reported as unsupported, will transcode to AAC
- **Documentation**: [Codec Support](https://jellyfin.org/docs/general/clients/codec-support/)

**Our Plugin's Approach**: Using .strm files in a TV Shows library (not Music library) avoids the Music library .strm bug, but audio playback still fails in the web player due to HLS buffering issues.

**Observed Behavior in Web Player (January 2026)**:
- First 5 seconds of audio play
- Those 5 seconds repeat once
- Playback crashes with "No valid media source found"
- Logs show multiple HLS transcode attempts, then cancellation
- This matches the documented HLS buffering issues in Firefox and other browsers

---

### 4. **Common Challenges and Solutions**

**Challenge 1: Lack of Native Podcast Support**
- **Status**: [Feature request #479](https://github.com/jellyfin/jellyfin/issues/479) and [#770](https://github.com/jellyfin/jellyfin/issues/770) have been open for years
- **Community Sentiment**: Users want it as "first-class citizen" not requiring plugins
- **Requested Features**:
  - Resume/playback position tracking
  - Mark as played/auto-delete
  - RSS feed subscription management
  - Auto-download new episodes

**Challenge 2: Audio Playback Position**
- **Issue**: Jellyfin doesn't save listening progress for audio files on iOS
- **Impact**: Users must manually find where they left off in podcasts
- **Related**: [Continue listening for audio content](https://github.com/jellyfin/jellyfin-ios/issues/172)

**Challenge 3: Organizing Podcasts**
- **Common Approach**: Treat as TV shows with seasons
- **Structure**: Our implementation matches this:
  ```
  Podcast Name/
    Season 1/
      episode.nfo
      episode.strm
  ```
- **Metadata Format**: [TV Shows NFO specification](https://jellyfin.org/docs/general/server/media/shows/)
- **Episode NFO**: Uses `<episodedetails>` root with `<season>`, `<episode>`, `<aired>`, etc.

**Challenge 4: Audiobook vs Podcast**
- **Bookshelf Plugin**: [jellyfin-plugin-bookshelf](https://github.com/jellyfin/jellyfin-plugin-bookshelf) exists for audiobooks
- **Issues**: M4b files don't work in Safari, impacting iOS users
- **Alternative Client**: [Plappa](https://github.com/LeoKlaus/plappa) - dedicated audiobook client for Jellyfin
- **Lesson**: Audio-only content has general playback issues in Jellyfin web player

---

### 5. **Key Findings About Our Implementation**

**What Makes Our Plugin Unique:**

1. **We're actually creating .strm files**: Most solutions download files; we stream directly
2. **TV Shows organization**: Smart choice - avoids the music library .strm bug
3. **Season 1 structure**: Matches Jellyfin's TV show expectations
4. **NFO metadata**: Proper use of `<episodedetails>` format
5. **Apple Podcasts URL support**: No other documented solution does this

**Why Audio Doesn't Play in Web Player:**

Based on research, this is likely a **Jellyfin limitation, not our bug**:
- Web player has known issues with audio-only content
- The .strm + music library combo is particularly problematic
- Our workaround (TV Shows library) is correct
- Mobile/desktop clients work because they have better audio codec handling

**Similar Patterns in the Wild:**
- Our approach is similar to how [ytdlp2STRM](https://github.com/fe80Grau/ytdlp2STRM) creates .strm files for YouTube content
- The TV show organization matches community recommendations for podcast-like content

---

### 6. **What Others Haven't Solved (That We Have)**

1. **Direct RSS streaming**: PodGrab downloads, we stream
2. **Apple Podcasts URL resolution**: No other plugin documents this
3. **Automated library management**: Auto-refresh task
4. **Episode metadata extraction**: Duration parsing, thumbnails, etc.
5. **Multi-feed support**: Clean UI for managing multiple podcasts

---

### 7. **Recommendations Based on Research**

**For Our Documentation:**
- Emphasize that web player audio limitation is a **Jellyfin issue**, not our plugin
- Link to related Jellyfin issues ([#13472](https://github.com/jellyfin/jellyfin/discussions/13472), [#8201](https://github.com/jellyfin/jellyfin/issues/8201))
- Highlight that PodGrab requires downloading, our plugin streams (major differentiator)

**Potential Improvements:**
- Consider adding optional download mode (like PodGrab) for users who want offline access
- Document that video podcasts work in web player (codec support is better)
- Add note about using dedicated Jellyfin clients for best audio experience

**Community Positioning:**
- Our plugin is the **most complete streaming-based solution** found
- Position as complementary to PodGrab (stream vs. download)
- Could contribute to [awesome-jellyfin](https://github.com/awesome-jellyfin/awesome-jellyfin) list

---

## Additional Sources - HLS Audio Streaming Issues (January 2026)

Research conducted to understand the "play 5 seconds, repeat, crash" behavior observed in web player:

**HLS Buffering and Playback Issues:**
- [Frequent playback stalls, insufficient buffering](https://github.com/jellyfin/jellyfin-web/issues/2856)
- [Playback stops due to HLS Error (bufferFullError) on Firefox](https://github.com/jellyfin/jellyfin-web/issues/2223)
- [HLS playback on Firefox not working (bufferStalledError)](https://github.com/jellyfin/jellyfin/issues/11872)
- [Constant buffering when streaming](https://forum.jellyfin.org/t-solved-constant-buffering-when-streaming)
- [Extreme Buffering without Transcoding](https://github.com/jellyfin/jellyfin/issues/13039)

**.strm File Specific Issues:**
- [User-agent not working in strm files](https://github.com/jellyfin/jellyfin/issues/9019)
- [Server returned 401 Unauthorized with STRM file](https://github.com/jellyfin/jellyfin/issues/10164)

**Audio Streaming and Remuxing:**
- [Stream issues when remuxing from multiple audio tracks](https://github.com/jellyfin/jellyfin/issues/15430)
- [Video freezes during transcoding while audio is fine](https://github.com/jellyfin/jellyfin-androidtv/issues/4159)

---

## Original Sources

- [Podcast Support Feature Request](https://features.jellyfin.org/posts/48/podcast-support)
- [Podcast Support Issue #479](https://github.com/jellyfin/jellyfin/issues/479)
- [Not podcast friendly yet Issue #770](https://github.com/jellyfin/jellyfin/issues/770)
- [Podcast manager plugin forum](https://forum.jellyfin.org/t-podcast-manager-plugin)
- [PodGrab GitHub Repository](https://github.com/akhilrex/podgrab)
- [Podgrab as Jellyfin backend](https://features.jellyfin.org/posts/1066/podgrab-as-a-podcast-back-end-for-jellyfin)
- [Lightphone-musiccast](https://github.com/nateswart/lightphone-musiccast)
- [Jellyfin .strm audio issue](https://github.com/jellyfin/jellyfin/discussions/13472)
- [.strm file in music library issue](https://github.com/jellyfin/jellyfin/issues/8201)
- [MP3/Audio files not playing](https://github.com/jellyfin/jellyfin-web/issues/4788)
- [Jellyfin Codec Support](https://jellyfin.org/docs/general/clients/codec-support/)
- [SamCullin jellyfin-rss](https://github.com/SamCullin/jellyfin-rss)
- [Archived RSS parser example](https://github.com/jellyfin-archive/emby-plugin-channels/blob/master/MediaBrowser.Channels.iPlayer/RSS.cs)
- [Jellyfin TV Shows Documentation](https://jellyfin.org/docs/general/server/media/shows/)
- [Jellyfin NFO Metadata](https://jellyfin.org/docs/general/server/metadata/nfo/)
- [Jellyfin Bookshelf Plugin](https://github.com/jellyfin/jellyfin-plugin-bookshelf)
- [Plappa Audiobook Client](https://github.com/LeoKlaus/plappa)
- [Awesome Jellyfin Collection](https://github.com/awesome-jellyfin/awesome-jellyfin)

---

## Solution Implemented: WebM Video Wrapper with Automatic Client Detection

After researching the HLS buffering and audio playback issues documented above, we implemented a working solution with automatic client detection:

### Implementation

- Created `Api/PodcastStreamController.cs` that wraps audio in WebM video with static artwork
- Uses VP8 video codec (low bitrate, real-time encoding)
- Uses Opus audio codec (efficient, browser-compatible)
- **Automatic client detection**: Parses `Authorization` header to identify web vs mobile/desktop clients
- **Web player**: Wraps audio in WebM video for Jellyfin to transcode to HLS
- **Mobile/Desktop clients**: Redirects to direct audio URL for native playback
- Jellyfin transcodes the WebM stream to HLS segments (web player only)

### Results

- ✅ Smooth playback in web player
- ✅ Seeking works correctly
- ✅ Stable for full-length episodes
- ✅ Bypasses the HLS audio buffering issues
- ✅ Optimal performance for all clients (no manual configuration needed)
- ✅ Mobile/desktop clients use direct audio playback (no transcoding overhead)

### Trade-offs

- Web player requires server CPU for real-time encoding
- Mobile/desktop clients have zero overhead (direct playback)
- Client detection based on `Authorization` header format (reliable but depends on Jellyfin's auth scheme)

This solution effectively works around Jellyfin's audio streaming limitations while maintaining full web player functionality and optimal performance for mobile/desktop clients.

---

**Research Date**: January 1, 2026
