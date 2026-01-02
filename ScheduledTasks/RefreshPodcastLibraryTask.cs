using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Podcast.Api;
using Jellyfin.Plugin.Podcast.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Podcast.ScheduledTasks
{
    /// <summary>
    /// Scheduled task to refresh the podcast library.
    /// </summary>
    public class RefreshPodcastLibraryTask : IScheduledTask
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshPodcastLibraryTask"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        public RefreshPodcastLibraryTask(
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public string Name => "Refresh Podcast Library";

        /// <inheritdoc />
        public string Description => "Fetches new episodes from configured podcast feeds and creates .strm files";

        /// <inheritdoc />
        public string Category => "Podcast";

        /// <inheritdoc />
        public string Key => "RefreshPodcastLibrary";

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var logger = _loggerFactory.CreateLogger<RefreshPodcastLibraryTask>();
            logger.LogInformation("Starting podcast library refresh task");

            progress?.Report(0);

            try
            {
                var feedParser = new PodcastFeedParser(_httpClientFactory, _loggerFactory.CreateLogger<PodcastFeedParser>());
                var libraryManager = new PodcastLibraryManager(
                    feedParser,
                    _httpClientFactory,
                    _loggerFactory.CreateLogger<PodcastLibraryManager>());

                await libraryManager.RefreshLibraryAsync(cancellationToken);
                progress?.Report(100);

                logger.LogInformation("Podcast library refresh task completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during podcast library refresh task");
                throw;
            }
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run every 2 hours
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(2).Ticks
                }
            };
        }
    }
}
