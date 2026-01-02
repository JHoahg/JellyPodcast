using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Podcast.Api;
using Jellyfin.Plugin.Podcast.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Podcast.Services;

/// <summary>
/// Manages the podcast library by creating .strm files for episodes.
/// </summary>
public class PodcastLibraryManager
{
    private readonly PodcastFeedParser _feedParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PodcastLibraryManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodcastLibraryManager"/> class.
    /// </summary>
    /// <param name="feedParser">Podcast feed parser.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    public PodcastLibraryManager(
        PodcastFeedParser feedParser,
        IHttpClientFactory httpClientFactory,
        ILogger<PodcastLibraryManager> logger)
    {
        _feedParser = feedParser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the podcast library by fetching all feeds and creating .strm files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshLibraryAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogWarning("Plugin configuration is null");
            return;
        }

        var libraryPath = config.LibraryPath;
        if (string.IsNullOrEmpty(libraryPath))
        {
            _logger.LogWarning("Library path is not configured");
            return;
        }

        // Ensure library directory exists
        Directory.CreateDirectory(libraryPath);

        _logger.LogInformation("Starting podcast library refresh. Processing {Count} feeds", config.PodcastFeeds.Count);

        foreach (var feedConfig in config.PodcastFeeds.Where(f => f.Enabled))
        {
            try
            {
                await ProcessPodcastFeedAsync(feedConfig, libraryPath, config, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing podcast feed: {Url}", feedConfig.Url);
            }
        }

        // Cleanup old episodes if enabled
        if (config.EnableAutoCleanup)
        {
            CleanupOldEpisodes(libraryPath, config);
        }

        _logger.LogInformation("Podcast library refresh completed");
    }

    private async Task ProcessPodcastFeedAsync(
        PodcastFeed feedConfig,
        string libraryPath,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing podcast feed: {Url}", feedConfig.Url);

        var feedData = await _feedParser.ParseFeedAsync(feedConfig.Url, cancellationToken);
        if (feedData == null)
        {
            _logger.LogWarning("Failed to parse feed: {Url}", feedConfig.Url);
            return;
        }

        // Use custom name if provided, otherwise use feed title
        var podcastName = !string.IsNullOrEmpty(feedConfig.CustomName) ? feedConfig.CustomName : feedData.Title;
        var podcastDir = Path.Combine(libraryPath, SanitizeFileName(podcastName));

        Directory.CreateDirectory(podcastDir);

        // Download podcast cover art
        if (config.DownloadThumbnails && !string.IsNullOrEmpty(feedData.ImageUrl))
        {
            await DownloadImageAsync(feedData.ImageUrl, Path.Combine(podcastDir, "folder.jpg"), cancellationToken);
        }

        // Create tvshow.nfo for the podcast series
        CreatePodcastNfo(podcastDir, feedData, podcastName);

        // Limit episodes if configured
        var episodesToProcess = feedData.Episodes
            .OrderByDescending(e => e.PubDate)
            .Take(config.MaxEpisodesPerPodcast)
            .ToList();

        _logger.LogInformation("Creating files for {Count} episodes of {Podcast}", episodesToProcess.Count, podcastName);

        int episodeNumber = 1;
        foreach (var episode in episodesToProcess)
        {
            try
            {
                _logger.LogInformation("Processing episode: {Title} - MediaURL: {Url}", episode.Title, episode.MediaUrl);
                await CreateEpisodeFilesAsync(podcastDir, episode, episodeNumber, config, cancellationToken);
                _logger.LogInformation("Successfully created files for episode: {Title}", episode.Title);
                episodeNumber++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating files for episode: {Title}", episode.Title);
            }
        }

        _logger.LogInformation("Finished processing all episodes for {Podcast}", podcastName);
    }

    private async Task CreateEpisodeFilesAsync(
        string podcastDir,
        PodcastEpisode episode,
        int episodeNumber,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        var safeTitle = SanitizeFileName(episode.Title);
        var datePrefix = episode.PubDate != DateTime.MinValue
            ? episode.PubDate.ToString("yyyy-MM-dd")
            : "unknown";

        // Put all episodes in Season 1
        var seasonDir = Path.Combine(podcastDir, "Season 1");
        Directory.CreateDirectory(seasonDir);

        var baseFileName = $"{datePrefix} - {safeTitle}";
        var strmPath = Path.Combine(seasonDir, $"{baseFileName}.strm");
        var nfoPath = Path.Combine(seasonDir, $"{baseFileName}.nfo");
        var thumbPath = Path.Combine(seasonDir, $"{baseFileName}-thumb.jpg");

        // Create .strm file
        if (!File.Exists(strmPath))
        {
            var isVideo = IsVideoFile(episode.MediaUrl);
            var mode = config.WebPlayerCompatibilityMode ?? "auto";

            // For audio files, create .audiourl file if using controller (alwaysOn or auto mode)
            if (!isVideo && (mode == "alwaysOn" || mode == "auto"))
            {
                var audioUrlPath = Path.Combine(seasonDir, $"{baseFileName}.audiourl");
                await File.WriteAllTextAsync(audioUrlPath, episode.MediaUrl, cancellationToken);
            }

            var strmContent = GetStrmContent(seasonDir, episode.MediaUrl, config);
            await File.WriteAllTextAsync(strmPath, strmContent, cancellationToken);
            _logger.LogDebug("Created .strm file: {Path} (Type: {Type}, Mode: {Mode})", strmPath, isVideo ? "Video" : "Audio", mode);
        }

        // Create .nfo file
        if (!File.Exists(nfoPath))
        {
            CreateEpisodeNfo(nfoPath, episode, episodeNumber);
            _logger.LogDebug("Created .nfo file: {Path}", nfoPath);
        }

        // Download episode thumbnail
        if (config.DownloadThumbnails && !string.IsNullOrEmpty(episode.ImageUrl) && !File.Exists(thumbPath))
        {
            await DownloadImageAsync(episode.ImageUrl, thumbPath, cancellationToken);
        }
    }

    private void CreatePodcastNfo(string podcastDir, PodcastFeedData feedData, string podcastName)
    {
        var nfoPath = Path.Combine(podcastDir, "tvshow.nfo");
        if (File.Exists(nfoPath))
        {
            return;
        }

        var nfo = new StringBuilder();
        nfo.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        nfo.AppendLine("<tvshow>");
        nfo.AppendLine($"  <title>{System.Security.SecurityElement.Escape(podcastName)}</title>");
        nfo.AppendLine($"  <plot>{System.Security.SecurityElement.Escape(feedData.Description)}</plot>");
        nfo.AppendLine($"  <studio>{System.Security.SecurityElement.Escape(feedData.Author)}</studio>");
        nfo.AppendLine("  <genre>Podcast</genre>");
        if (!string.IsNullOrEmpty(feedData.ImageUrl))
        {
            nfo.AppendLine($"  <thumb>{System.Security.SecurityElement.Escape(feedData.ImageUrl)}</thumb>");
        }
        nfo.AppendLine("</tvshow>");

        File.WriteAllText(nfoPath, nfo.ToString());
        _logger.LogDebug("Created podcast NFO: {Path}", nfoPath);
    }

    private void CreateEpisodeNfo(string nfoPath, PodcastEpisode episode, int episodeNumber)
    {
        var nfo = new StringBuilder();
        nfo.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        nfo.AppendLine("<episodedetails>");
        nfo.AppendLine($"  <title>{System.Security.SecurityElement.Escape(episode.Title)}</title>");
        nfo.AppendLine($"  <plot>{System.Security.SecurityElement.Escape(episode.Description)}</plot>");
        nfo.AppendLine("  <season>1</season>");
        nfo.AppendLine($"  <episode>{episodeNumber}</episode>");

        if (episode.PubDate != DateTime.MinValue)
        {
            nfo.AppendLine($"  <aired>{episode.PubDate:yyyy-MM-dd}</aired>");
            nfo.AppendLine($"  <year>{episode.PubDate.Year}</year>");
        }

        if (episode.Duration > TimeSpan.Zero)
        {
            nfo.AppendLine($"  <runtime>{(int)episode.Duration.TotalMinutes}</runtime>");
        }

        nfo.AppendLine("  <genre>Podcast</genre>");

        if (!string.IsNullOrEmpty(episode.ImageUrl))
        {
            nfo.AppendLine($"  <thumb>{System.Security.SecurityElement.Escape(episode.ImageUrl)}</thumb>");
        }

        nfo.AppendLine("</episodedetails>");

        File.WriteAllText(nfoPath, nfo.ToString());
    }

    private async Task DownloadImageAsync(string imageUrl, string savePath, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(savePath))
            {
                return;
            }

            using var httpClient = _httpClientFactory.CreateClient();
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
            await File.WriteAllBytesAsync(savePath, imageBytes, cancellationToken);

            _logger.LogDebug("Downloaded image: {Path}", savePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image from {Url}", imageUrl);
        }
    }

    private void CleanupOldEpisodes(string libraryPath, PluginConfiguration config)
    {
        _logger.LogInformation("Starting cleanup of episodes older than {Days} days", config.DaysToKeepEpisodes);

        var cutoffDate = DateTime.Now.AddDays(-config.DaysToKeepEpisodes);

        try
        {
            foreach (var podcastDir in Directory.GetDirectories(libraryPath))
            {
                // Check season directories
                foreach (var seasonDir in Directory.GetDirectories(podcastDir))
                {
                    var files = Directory.GetFiles(seasonDir, "*.strm");

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            // Delete .strm, .nfo, .audiourl, and thumbnail files
                            var basePath = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));

                            DeleteFileIfExists(file);
                            DeleteFileIfExists($"{basePath}.nfo");
                            DeleteFileIfExists($"{basePath}.audiourl");
                            DeleteFileIfExists($"{basePath}-thumb.jpg");

                            _logger.LogDebug("Deleted old episode: {File}", file);
                        }
                    }

                    // Remove empty season directories
                    if (!Directory.EnumerateFileSystemEntries(seasonDir).Any())
                    {
                        Directory.Delete(seasonDir);
                        _logger.LogDebug("Deleted empty season directory: {Dir}", seasonDir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetStrmContent(string episodePath, string audioUrl, PluginConfiguration config)
    {
        // Check if this is a video file - videos don't need wrapping
        var isVideo = IsVideoFile(audioUrl);

        if (isVideo)
        {
            // Video files: always direct URL
            return audioUrl;
        }

        // Audio files: check compatibility mode setting
        var mode = config.WebPlayerCompatibilityMode ?? "auto";

        if (mode == "alwaysOff")
        {
            // Direct audio URL (web player may not work)
            return audioUrl;
        }

        if (mode == "alwaysOn" || mode == "auto")
        {
            // Route through controller for WebM wrapping
            var episodeId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(episodePath));
            return $"http://localhost:8096/Podcast/Stream/{episodeId}";
        }

        // Fallback to direct URL
        return audioUrl;
    }

    private bool IsVideoFile(string url)
    {
        var videoExtensions = new[] { ".mp4", ".webm", ".mkv", ".avi", ".mov", ".m4v", ".mpg", ".mpeg" };
        return videoExtensions.Any(ext => url.Contains(ext, StringComparison.OrdinalIgnoreCase));
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Also replace some problematic characters
        sanitized = sanitized
            .Replace(":", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("?", string.Empty)
            .Replace("*", string.Empty)
            .Replace("\"", "'")
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace("|", "-");

        // Trim to reasonable length
        if (sanitized.Length > 200)
        {
            sanitized = sanitized.Substring(0, 200);
        }

        return sanitized.Trim();
    }
}
