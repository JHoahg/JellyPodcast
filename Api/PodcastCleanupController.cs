using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Podcast.Api
{
    /// <summary>
    /// Controller for podcast maintenance operations.
    /// </summary>
    [ApiController]
    [Route("Podcast")]
    [Authorize(Policy = "RequiresElevation")]
    public class PodcastCleanupController : ControllerBase
    {
        private readonly ILogger<PodcastCleanupController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PodcastCleanupController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{PodcastCleanupController}"/> interface.</param>
        public PodcastCleanupController(ILogger<PodcastCleanupController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Cleans up all episode files while keeping podcast metadata.
        /// </summary>
        /// <returns>OK if successful.</returns>
        [HttpPost("Cleanup")]
        public IActionResult CleanupEpisodes()
        {
            try
            {
                _logger.LogInformation("Starting podcast cleanup");

                var config = Plugin.Instance?.Configuration;
                if (config == null)
                {
                    _logger.LogError("Plugin configuration is null");
                    return StatusCode(500, "Plugin configuration not available");
                }

                var libraryPath = config.LibraryPath;
                if (string.IsNullOrEmpty(libraryPath) || !Directory.Exists(libraryPath))
                {
                    _logger.LogError("Library path does not exist: {Path}", libraryPath);
                    return NotFound("Library path not found");
                }

                int filesDeleted = 0;

                // Iterate through all podcast folders
                var podcastDirs = Directory.GetDirectories(libraryPath);
                foreach (var podcastDir in podcastDirs)
                {
                    // Find Season folders
                    var seasonDirs = Directory.GetDirectories(podcastDir, "Season *");
                    foreach (var seasonDir in seasonDirs)
                    {
                        // Delete episode files (.strm, .nfo, .audiourl, -thumb.jpg)
                        var files = Directory.GetFiles(seasonDir);
                        foreach (var file in files)
                        {
                            var fileName = Path.GetFileName(file);
                            // Keep only tvshow.nfo and folder.jpg (though they shouldn't be in Season folders)
                            if (fileName != "tvshow.nfo" && fileName != "folder.jpg")
                            {
                                System.IO.File.Delete(file);
                                filesDeleted++;
                                _logger.LogDebug("Deleted: {File}", file);
                            }
                        }
                    }
                }

                _logger.LogInformation("Cleanup completed. Deleted {Count} files", filesDeleted);
                return Ok(new { FilesDeleted = filesDeleted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                return StatusCode(500, "Cleanup failed");
            }
        }
    }
}
