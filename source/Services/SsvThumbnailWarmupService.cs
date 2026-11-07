using CommonPluginsShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Queues silent background generation of missing image and video thumbnails (images first, then videos).
    /// </summary>
    public sealed class SsvThumbnailWarmupService
    {
        private const string LogPrefix = "[SsvThumbnail][warmup]";

        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly string _pluginName;
        private readonly SsvThumbnailService _thumbnailService;
        private readonly object _queueLock = new object();
        private readonly Dictionary<Guid, WarmupJob> _pendingJobs = new Dictionary<Guid, WarmupJob>();
        private readonly Queue<Guid> _queue = new Queue<Guid>();
        private readonly object _workerLock = new object();
        private bool _workerRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsvThumbnailWarmupService"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin display name used for error logging.</param>
        /// <param name="thumbnailService">Shared thumbnail generation service.</param>
        public SsvThumbnailWarmupService(string pluginName, SsvThumbnailService thumbnailService)
        {
            _pluginName = pluginName ?? string.Empty;
            _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        }

        /// <summary>
        /// Enqueues every game that already has screenshot items in the plugin cache (no folder rescan).
        /// </summary>
        /// <param name="games">Plugin database entries to warm up.</param>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="ffmpegPath">Absolute path to the ffmpeg executable.</param>
        public void EnqueueLibrary(
            IEnumerable<GameScreenshots> games,
            string cacheRootPath,
            string ffmpegPath)
        {
            if (games == null || string.IsNullOrWhiteSpace(cacheRootPath))
            {
                return;
            }

            int enqueuedGames = 0;
            foreach (GameScreenshots game in games)
            {
                if (game?.Items == null || game.Items.Count == 0)
                {
                    continue;
                }

                if (EnqueueGameCore(game.Id, game.Name, game.Items, cacheRootPath, ffmpegPath))
                {
                    enqueuedGames++;
                }
            }

            Logger.Info(string.Format(
                "{0} library enqueuedGames={1}",
                LogPrefix,
                enqueuedGames));
        }

        /// <summary>
        /// Enqueues missing-thumbnail generation for one game (coalesces by <paramref name="gameId"/>).
        /// </summary>
        /// <param name="gameId">Playnite game identifier.</param>
        /// <param name="gameName">Display name for logs.</param>
        /// <param name="items">Screenshot items for the game.</param>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="ffmpegPath">Absolute path to the ffmpeg executable.</param>
        public void EnqueueGame(
            Guid gameId,
            string gameName,
            IList<Screenshot> items,
            string cacheRootPath,
            string ffmpegPath)
        {
            if (gameId == Guid.Empty || items == null || items.Count == 0 || string.IsNullOrWhiteSpace(cacheRootPath))
            {
                return;
            }

            _ = EnqueueGameCore(gameId, gameName, items, cacheRootPath, ffmpegPath);
        }

        private bool EnqueueGameCore(
            Guid gameId,
            string gameName,
            IList<Screenshot> items,
            string cacheRootPath,
            string ffmpegPath)
        {
            List<Screenshot> snapshot = items
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.FileName))
                .ToList();
            if (snapshot.Count == 0)
            {
                return false;
            }

            WarmupJob job = new WarmupJob(
                gameId,
                gameName ?? string.Empty,
                snapshot,
                cacheRootPath,
                ffmpegPath ?? string.Empty);

            bool addedToQueue;
            lock (_queueLock)
            {
                bool alreadyQueued = _pendingJobs.ContainsKey(gameId);
                _pendingJobs[gameId] = job;
                if (alreadyQueued)
                {
                    addedToQueue = false;
                    Common.LogDebug(string.Format(
                        "{0} coalesce gameId={1} name=\"{2}\" items={3}",
                        LogPrefix,
                        gameId,
                        job.GameName,
                        snapshot.Count));
                }
                else
                {
                    _queue.Enqueue(gameId);
                    addedToQueue = true;
                    Common.LogDebug(string.Format(
                        "{0} enqueue gameId={1} name=\"{2}\" items={3}",
                        LogPrefix,
                        gameId,
                        job.GameName,
                        snapshot.Count));
                }
            }

            EnsureWorkerStarted();
            return addedToQueue;
        }

        private void EnsureWorkerStarted()
        {
            lock (_workerLock)
            {
                if (_workerRunning)
                {
                    return;
                }

                _workerRunning = true;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    WorkerLoop();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, _pluginName);
                }
                finally
                {
                    lock (_workerLock)
                    {
                        _workerRunning = false;
                    }

                    lock (_queueLock)
                    {
                        if (_queue.Count > 0)
                        {
                            EnsureWorkerStarted();
                        }
                    }
                }
            });
        }

        private void WorkerLoop()
        {
            while (true)
            {
                WarmupJob job;
                lock (_queueLock)
                {
                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    Guid gameId = _queue.Dequeue();
                    if (!_pendingJobs.TryGetValue(gameId, out job))
                    {
                        continue;
                    }

                    _pendingJobs.Remove(gameId);
                }

                ProcessJob(job);
            }
        }

        private void ProcessJob(WarmupJob job)
        {
            if (job == null || job.Items == null || job.Items.Count == 0)
            {
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int imageSkip = 0;
            int imageOk = 0;
            int imageFail = 0;
            int videoSkip = 0;
            int videoOk = 0;
            int videoFail = 0;

            Common.LogDebug(string.Format(
                "{0} start gameId={1} name=\"{2}\" items={3}",
                LogPrefix,
                job.GameId,
                job.GameName,
                job.Items.Count));

            List<Screenshot> images = job.Items.Where(x => !x.IsVideo).ToList();
            List<Screenshot> videos = job.Items.Where(x => x.IsVideo).ToList();

            foreach (Screenshot screenshot in images)
            {
                WarmupImageResult result = EnsureImageThumbnail(screenshot, job.CacheRootPath);
                switch (result)
                {
                    case WarmupImageResult.Skipped:
                        imageSkip++;
                        break;
                    case WarmupImageResult.CreatedOrValid:
                        imageOk++;
                        break;
                    default:
                        imageFail++;
                        break;
                }
            }

            foreach (Screenshot screenshot in videos)
            {
                WarmupImageResult result = EnsureVideoThumbnail(screenshot, job.CacheRootPath, job.FfmpegPath);
                switch (result)
                {
                    case WarmupImageResult.Skipped:
                        videoSkip++;
                        break;
                    case WarmupImageResult.CreatedOrValid:
                        videoOk++;
                        break;
                    default:
                        videoFail++;
                        break;
                }
            }

            stopwatch.Stop();
            Common.LogDebug(string.Format(
                "{0} done gameId={1} name=\"{2}\" imagesSkip={3} imagesOk={4} imagesFail={5} videosSkip={6} videosOk={7} videosFail={8} elapsedMs={9}",
                LogPrefix,
                job.GameId,
                job.GameName,
                imageSkip,
                imageOk,
                imageFail,
                videoSkip,
                videoOk,
                videoFail,
                stopwatch.ElapsedMilliseconds));
        }

        private WarmupImageResult EnsureImageThumbnail(Screenshot screenshot, string cacheRootPath)
        {
            if (screenshot == null || string.IsNullOrWhiteSpace(screenshot.FileName) || !File.Exists(screenshot.FileName))
            {
                return WarmupImageResult.Skipped;
            }

            string thumbnailPath = SsvThumbnailService.GetImageThumbnailCachePath(
                cacheRootPath,
                Path.GetFileName(screenshot.FileName));
            string failureMarkerPath = SsvThumbnailService.GetFailureMarkerPath(thumbnailPath);

            if (File.Exists(failureMarkerPath))
            {
                return WarmupImageResult.Skipped;
            }

            if (SsvThumbnailService.IsCacheEntryValid(screenshot.FileName, screenshot.Modifed, thumbnailPath))
            {
                return WarmupImageResult.Skipped;
            }

            try
            {
                string ensured = _thumbnailService.TryEnsureImageThumbnail(
                    screenshot.FileName,
                    cacheRootPath,
                    screenshot.Modifed);
                return string.IsNullOrEmpty(ensured)
                    ? WarmupImageResult.Failed
                    : WarmupImageResult.CreatedOrValid;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, _pluginName);
                return WarmupImageResult.Failed;
            }
        }

        private WarmupImageResult EnsureVideoThumbnail(
            Screenshot screenshot,
            string cacheRootPath,
            string ffmpegPath)
        {
            if (screenshot == null || string.IsNullOrWhiteSpace(screenshot.FileName) || !File.Exists(screenshot.FileName))
            {
                return WarmupImageResult.Skipped;
            }

            try
            {
                double durationSeconds = screenshot.Duration.TotalSeconds;
                string thumbnailPath = SsvThumbnailService.GetVideoThumbnailCachePath(
                    cacheRootPath,
                    Path.GetFileName(screenshot.FileName),
                    screenshot.FileSize,
                    durationSeconds);
                string failureMarkerPath = SsvThumbnailService.GetFailureMarkerPath(thumbnailPath);

                if (File.Exists(failureMarkerPath))
                {
                    return WarmupImageResult.Skipped;
                }

                if (SsvThumbnailService.IsCacheEntryValid(screenshot.FileName, screenshot.Modifed, thumbnailPath))
                {
                    return WarmupImageResult.Skipped;
                }

                string ensured = _thumbnailService.TryEnsureVideoThumbnail(
                    screenshot.FileName,
                    cacheRootPath,
                    screenshot.Modifed,
                    screenshot.FileSize,
                    durationSeconds,
                    ffmpegPath);
                return string.IsNullOrEmpty(ensured)
                    ? WarmupImageResult.Failed
                    : WarmupImageResult.CreatedOrValid;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, _pluginName);
                return WarmupImageResult.Failed;
            }
        }

        private enum WarmupImageResult
        {
            Skipped,
            CreatedOrValid,
            Failed
        }

        private sealed class WarmupJob
        {
            public WarmupJob(
                Guid gameId,
                string gameName,
                List<Screenshot> items,
                string cacheRootPath,
                string ffmpegPath)
            {
                GameId = gameId;
                GameName = gameName;
                Items = items;
                CacheRootPath = cacheRootPath;
                FfmpegPath = ffmpegPath;
            }

            public Guid GameId { get; }

            public string GameName { get; }

            public List<Screenshot> Items { get; }

            public string CacheRootPath { get; }

            public string FfmpegPath { get; }
        }
    }
}
