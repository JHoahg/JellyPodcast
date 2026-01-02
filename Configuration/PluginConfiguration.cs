using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Podcast.Configuration;

/// <summary>
/// Plugin configuration for the Podcast plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the list of podcast feed URLs.
    /// </summary>
    public List<PodcastFeed> PodcastFeeds { get; set; } = new List<PodcastFeed>();

    /// <summary>
    /// Gets or sets the library path where podcast .strm files will be created.
    /// </summary>
    public string LibraryPath { get; set; } = "/config/data/podcasts";

    /// <summary>
    /// Gets or sets the preferred audio quality (high, medium, low).
    /// </summary>
    public string PreferredQuality { get; set; } = "high";

    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic cleanup of old episodes.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of days to keep episodes before cleanup.
    /// </summary>
    public int DaysToKeepEpisodes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of episodes to keep per podcast.
    /// </summary>
    public int MaxEpisodesPerPodcast { get; set; } = 50;

    /// <summary>
    /// Gets or sets a value indicating whether to download thumbnail images.
    /// </summary>
    public bool DownloadThumbnails { get; set; } = true;

    /// <summary>
    /// Gets or sets the web player compatibility mode setting.
    /// Options: "auto" (recommended), "alwaysOn", "alwaysOff"
    /// </summary>
    public string WebPlayerCompatibilityMode { get; set; } = "auto";
}

/// <summary>
/// Represents a podcast feed configuration.
/// </summary>
public class PodcastFeed
{
    /// <summary>
    /// Gets or sets the unique identifier for this feed.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the podcast feed URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for this podcast (optional, will use feed title if empty).
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this feed is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
