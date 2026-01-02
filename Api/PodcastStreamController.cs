using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Podcast.Api
{
    /// <summary>
    /// Controller for streaming podcast episodes with video wrapper.
    /// </summary>
    [ApiController]
    [Route("Podcast/Stream")]
    public class PodcastStreamController : ControllerBase
    {
        private readonly ILogger<PodcastStreamController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PodcastStreamController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{PodcastStreamController}"/> interface.</param>
        public PodcastStreamController(ILogger<PodcastStreamController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Streams a podcast episode wrapped in video with static artwork.
        /// </summary>
        /// <param name="episodeId">The episode ID (base64 encoded path).</param>
        /// <returns>WebM video stream with VP8/Opus encoding.</returns>
        [HttpGet("{episodeId}")]
        public async Task<IActionResult> StreamEpisode(string episodeId)
        {
            try
            {
                _logger.LogInformation("Streaming request for episode ID: {EpisodeId}", episodeId);

                // Decode episode ID to get the episode directory path
                var episodePath = DecodeEpisodeId(episodeId);

                if (!Directory.Exists(episodePath))
                {
                    _logger.LogError("Episode directory not found: {Path}", episodePath);
                    return NotFound("Episode not found");
                }

                // Find the .audiourl file to get the original audio URL
                var audioUrlFile = Directory.GetFiles(episodePath, "*.audiourl").FirstOrDefault();
                if (audioUrlFile == null)
                {
                    _logger.LogError("No .audiourl file found in directory: {Path}", episodePath);
                    return NotFound("Episode audio URL not found");
                }

                // Read the audio URL from .audiourl file
                var audioUrl = await System.IO.File.ReadAllTextAsync(audioUrlFile);
                audioUrl = audioUrl.Trim();

                // Wrap audio in WebM video for all clients
                _logger.LogInformation("Wrapping audio in video. Audio URL: {Url}", audioUrl);

                // Find the thumbnail image
                var thumbnailFile = Directory.GetFiles(episodePath, "*-thumb.jpg").FirstOrDefault()
                                 ?? Directory.GetFiles(episodePath, "*.jpg").FirstOrDefault();

                if (thumbnailFile == null)
                {
                    // Try parent directory for podcast artwork
                    var parentDir = Directory.GetParent(episodePath)?.Parent?.FullName;
                    if (parentDir != null)
                    {
                        thumbnailFile = Path.Combine(parentDir, "folder.jpg");
                        if (!System.IO.File.Exists(thumbnailFile))
                        {
                            thumbnailFile = null;
                        }
                    }
                }

                if (thumbnailFile == null)
                {
                    _logger.LogWarning("No thumbnail found for episode, using blank image");
                    // Could generate a blank image or return error
                    return NotFound("No artwork available");
                }

                _logger.LogInformation("Using thumbnail: {Path}", thumbnailFile);

                // Start ffmpeg process to create video stream
                var ffmpegPath = GetFfmpegPath();
                var arguments = BuildFfmpegArguments(thumbnailFile, audioUrl);

                _logger.LogInformation("Starting ffmpeg: {Path} {Args}", ffmpegPath, arguments);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // Log ffmpeg stderr for debugging
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogDebug("FFmpeg: {Message}", e.Data);
                    }
                };

                process.Start();
                process.BeginErrorReadLine();

                // Set headers to make this look like a real media file
                // This helps Jellyfin recognize it as DirectPlay-compatible
                Response.ContentType = "video/webm";
                Response.Headers["Accept-Ranges"] = "none"; // We can't seek in a live stream
                Response.Headers["Cache-Control"] = "public, max-age=31536000"; // Cache aggressively
                Response.Headers["Connection"] = "keep-alive";
                Response.Headers["X-Content-Type-Options"] = "nosniff";

                _logger.LogInformation("Response headers set, streaming to client");

                // Copy ffmpeg output to response stream
                await process.StandardOutput.BaseStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);

                await process.WaitForExitAsync(HttpContext.RequestAborted);

                _logger.LogInformation("FFmpeg process completed for episode {EpisodeId}", episodeId);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming episode {EpisodeId}", episodeId);
                return StatusCode(500, "Internal server error");
            }
        }

        private string DecodeEpisodeId(string episodeId)
        {
            try
            {
                var bytes = Convert.FromBase64String(episodeId);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                throw new ArgumentException("Invalid episode ID");
            }
        }

        private string GetFfmpegPath()
        {
            // Jellyfin typically uses /usr/lib/jellyfin-ffmpeg/ffmpeg on Linux
            // or includes ffmpeg in PATH on Windows
            var paths = new[]
            {
                "/usr/lib/jellyfin-ffmpeg/ffmpeg",
                "/usr/bin/ffmpeg",
                "ffmpeg" // Will use PATH
            };

            foreach (var path in paths)
            {
                if (path == "ffmpeg" || System.IO.File.Exists(path))
                {
                    return path;
                }
            }

            return "ffmpeg"; // Fallback to PATH
        }

        private string BuildFfmpegArguments(string imagePath, string audioUrl)
        {
            // Build ffmpeg command to wrap audio in video with static image
            // WebM format is used for optimal streaming compatibility:
            // - Streams efficiently through pipes (no moov atom issues)
            // - VP8 video codec with real-time encoding settings
            // - Opus audio codec provides excellent quality at low bitrate
            // - Jellyfin can transcode to HLS for seekable playback
            //
            // -loop 1: Loop the input image
            // -i image: Input image file
            // -re: Read input at native frame rate (helps with network streams)
            // -i audio: Input audio stream
            // -thread_queue_size 512: Larger buffer for slow audio sources
            // -c:v libvpx: VP8 video codec
            // -b:v 100k: Low video bitrate (static image)
            // -deadline realtime: Optimize for real-time encoding
            // -cpu-used 8: Use fastest encoding preset
            // -c:a libopus: Opus audio codec
            // -b:a 128k: Audio bitrate
            // -shortest: End when shortest input ends (audio)
            // -f webm: WebM format for streaming
            // pipe:1: Output to stdout

            return $"-loop 1 -i \"{imagePath}\" -re -thread_queue_size 512 -i \"{audioUrl}\" " +
                   "-c:v libvpx -b:v 100k -deadline realtime -cpu-used 8 " +
                   "-c:a libopus -b:a 128k " +
                   "-shortest -f webm pipe:1";
        }

    }
}
