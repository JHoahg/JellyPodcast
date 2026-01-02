using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Podcast.Api;

/// <summary>
/// Podcast feed parser for RSS and Atom feeds.
/// </summary>
public class PodcastFeedParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PodcastFeedParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodcastFeedParser"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    public PodcastFeedParser(IHttpClientFactory httpClientFactory, ILogger<PodcastFeedParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Parses a podcast feed from the given URL.
    /// </summary>
    /// <param name="feedUrl">The feed URL to parse.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed podcast feed data.</returns>
    public async Task<PodcastFeedData?> ParseFeedAsync(string feedUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Check if this is an Apple Podcasts URL and resolve to RSS feed
            var resolvedUrl = await ResolveApplePodcastsUrlAsync(feedUrl, cancellationToken);

            _logger.LogInformation("Fetching podcast feed from {Url}", resolvedUrl);

            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetStringAsync(resolvedUrl, cancellationToken);

            var doc = XDocument.Parse(response);
            var root = doc.Root;

            if (root == null)
            {
                _logger.LogWarning("Feed has no root element: {Url}", feedUrl);
                return null;
            }

            // Detect feed type (RSS or Atom)
            if (root.Name.LocalName == "rss")
            {
                return ParseRssFeed(doc, feedUrl);
            }
            else if (root.Name.LocalName == "feed")
            {
                return ParseAtomFeed(doc, feedUrl);
            }

            _logger.LogWarning("Unknown feed format for {Url}", feedUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing podcast feed from {Url}", feedUrl);
            return null;
        }
    }

    private PodcastFeedData? ParseRssFeed(XDocument doc, string feedUrl)
    {
        var channel = doc.Descendants("channel").FirstOrDefault();
        if (channel == null)
        {
            return null;
        }

        var itunesNs = XNamespace.Get("http://www.itunes.com/dtds/podcast-1.0.dtd");

        var feed = new PodcastFeedData
        {
            FeedUrl = feedUrl,
            Title = channel.Element("title")?.Value ?? "Unknown Podcast",
            Description = channel.Element("description")?.Value ?? string.Empty,
            Author = channel.Element(itunesNs + "author")?.Value ?? channel.Element("author")?.Value ?? string.Empty,
            ImageUrl = channel.Element(itunesNs + "image")?.Attribute("href")?.Value
                      ?? channel.Element("image")?.Element("url")?.Value
                      ?? string.Empty,
            Language = channel.Element("language")?.Value ?? "en",
            Episodes = new List<PodcastEpisode>()
        };

        foreach (var item in channel.Descendants("item"))
        {
            var enclosure = item.Element("enclosure");
            if (enclosure == null)
            {
                continue; // Skip items without audio/video enclosures
            }

            var episode = new PodcastEpisode
            {
                Title = item.Element("title")?.Value ?? "Untitled Episode",
                Description = item.Element("description")?.Value
                            ?? item.Element(itunesNs + "summary")?.Value
                            ?? string.Empty,
                PubDate = ParsePubDate(item.Element("pubDate")?.Value),
                MediaUrl = enclosure.Attribute("url")?.Value ?? string.Empty,
                MediaType = enclosure.Attribute("type")?.Value ?? "audio/mpeg",
                MediaLength = ParseLong(enclosure.Attribute("length")?.Value),
                Duration = ParseDuration(item.Element(itunesNs + "duration")?.Value),
                ImageUrl = item.Element(itunesNs + "image")?.Attribute("href")?.Value ?? feed.ImageUrl,
                Guid = item.Element("guid")?.Value ?? Guid.NewGuid().ToString()
            };

            feed.Episodes.Add(episode);
        }

        _logger.LogInformation("Parsed RSS feed: {Title} with {Count} episodes", feed.Title, feed.Episodes.Count);
        return feed;
    }

    private PodcastFeedData? ParseAtomFeed(XDocument doc, string feedUrl)
    {
        var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
        var root = doc.Root;

        if (root == null)
        {
            return null;
        }

        var feed = new PodcastFeedData
        {
            FeedUrl = feedUrl,
            Title = root.Element(atomNs + "title")?.Value ?? "Unknown Podcast",
            Description = root.Element(atomNs + "subtitle")?.Value ?? string.Empty,
            Author = root.Element(atomNs + "author")?.Element(atomNs + "name")?.Value ?? string.Empty,
            ImageUrl = root.Elements(atomNs + "link")
                          .FirstOrDefault(e => e.Attribute("rel")?.Value == "icon")
                          ?.Attribute("href")?.Value ?? string.Empty,
            Language = "en",
            Episodes = new List<PodcastEpisode>()
        };

        foreach (var entry in root.Descendants(atomNs + "entry"))
        {
            var link = entry.Elements(atomNs + "link")
                           .FirstOrDefault(e => e.Attribute("rel")?.Value == "enclosure");

            if (link == null)
            {
                continue;
            }

            var episode = new PodcastEpisode
            {
                Title = entry.Element(atomNs + "title")?.Value ?? "Untitled Episode",
                Description = entry.Element(atomNs + "summary")?.Value ?? string.Empty,
                PubDate = ParseAtomDate(entry.Element(atomNs + "published")?.Value),
                MediaUrl = link.Attribute("href")?.Value ?? string.Empty,
                MediaType = link.Attribute("type")?.Value ?? "audio/mpeg",
                MediaLength = ParseLong(link.Attribute("length")?.Value),
                ImageUrl = feed.ImageUrl,
                Guid = entry.Element(atomNs + "id")?.Value ?? Guid.NewGuid().ToString()
            };

            feed.Episodes.Add(episode);
        }

        _logger.LogInformation("Parsed Atom feed: {Title} with {Count} episodes", feed.Title, feed.Episodes.Count);
        return feed;
    }

    private DateTime ParsePubDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(dateStr, out var date))
        {
            return date;
        }

        return DateTime.MinValue;
    }

    private DateTime ParseAtomDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return DateTime.MinValue;
        }

        if (DateTimeOffset.TryParse(dateStr, out var date))
        {
            return date.DateTime;
        }

        return DateTime.MinValue;
    }

    private long ParseLong(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        if (long.TryParse(value, out var result))
        {
            return result;
        }

        return 0;
    }

    private TimeSpan ParseDuration(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
        {
            return TimeSpan.Zero;
        }

        // Try seconds only first (most common for podcast durations)
        if (int.TryParse(duration, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Try HH:MM:SS format
        if (TimeSpan.TryParse(duration, out var timespan))
        {
            return timespan;
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Resolves Apple Podcasts URLs to their RSS feed URLs.
    /// </summary>
    /// <param name="url">The URL to check and resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RSS feed URL if Apple Podcasts URL, otherwise the original URL.</returns>
    private async Task<string> ResolveApplePodcastsUrlAsync(string url, CancellationToken cancellationToken)
    {
        // Check if this is an Apple Podcasts URL
        if (!url.Contains("podcasts.apple.com", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        try
        {
            _logger.LogInformation("Detected Apple Podcasts URL, resolving to RSS feed: {Url}", url);

            using var httpClient = _httpClientFactory.CreateClient();
            var htmlContent = await httpClient.GetStringAsync(url, cancellationToken);

            // Look for "feedUrl":"..." in the HTML/JSON
            var feedUrlMarker = "\"feedUrl\":\"";
            var startIndex = htmlContent.IndexOf(feedUrlMarker, StringComparison.Ordinal);

            if (startIndex == -1)
            {
                _logger.LogWarning("Could not find RSS feed URL in Apple Podcasts page, using original URL");
                return url;
            }

            startIndex += feedUrlMarker.Length;
            var endIndex = htmlContent.IndexOf("\"", startIndex, StringComparison.Ordinal);

            if (endIndex == -1)
            {
                _logger.LogWarning("Could not extract RSS feed URL from Apple Podcasts page");
                return url;
            }

            var rssUrl = htmlContent.Substring(startIndex, endIndex - startIndex);

            // Unescape JSON string (handle \/ -> /)
            rssUrl = rssUrl.Replace("\\/", "/");

            _logger.LogInformation("Resolved Apple Podcasts URL to RSS feed: {RssUrl}", rssUrl);

            return rssUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving Apple Podcasts URL, using original URL");
            return url;
        }
    }
}

/// <summary>
/// Represents parsed podcast feed data.
/// </summary>
public class PodcastFeedData
{
    /// <summary>
    /// Gets or sets the feed URL.
    /// </summary>
    public string FeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the podcast title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the podcast description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the podcast author.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the podcast image URL.
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the podcast language.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of episodes.
    /// </summary>
    public List<PodcastEpisode> Episodes { get; set; } = new List<PodcastEpisode>();
}

/// <summary>
/// Represents a podcast episode.
/// </summary>
public class PodcastEpisode
{
    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the episode description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the publication date.
    /// </summary>
    public DateTime PubDate { get; set; }

    /// <summary>
    /// Gets or sets the media URL.
    /// </summary>
    public string MediaUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media type (MIME type).
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media length in bytes.
    /// </summary>
    public long MediaLength { get; set; }

    /// <summary>
    /// Gets or sets the episode duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the episode image URL.
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the episode GUID.
    /// </summary>
    public string Guid { get; set; } = string.Empty;
}
