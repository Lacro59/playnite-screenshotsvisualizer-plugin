using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Display option for screenshot list toolbar ComboBoxes (value + localized label).
    /// </summary>
    /// <typeparam name="T">Underlying enum value.</typeparam>
    public sealed class SsvScreenshotListChoice<T>
    {
        /// <summary>
        /// Gets or sets the enum value stored in settings.
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the localized label shown in the ComboBox.
        /// </summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// Aggregate counts and sizes for a screenshot list (typically after media filter).
    /// </summary>
    public sealed class SsvScreenshotListStats
    {
        /// <summary>
        /// Gets or sets the number of image items.
        /// </summary>
        public int ImageCount { get; set; }

        /// <summary>
        /// Gets or sets the number of video items.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// Gets or sets the total byte size of image items.
        /// </summary>
        public long ImageTotalSize { get; set; }

        /// <summary>
        /// Gets or sets the total byte size of video items.
        /// </summary>
        public long VideoTotalSize { get; set; }

        /// <summary>
        /// Gets the total number of files.
        /// </summary>
        public int FileCount => ImageCount + VideoCount;

        /// <summary>
        /// Gets the total byte size of all files.
        /// </summary>
        public long FileTotalSize => ImageTotalSize + VideoTotalSize;
    }

    /// <summary>
    /// Filters (existing files, media type) and sorts screenshot lists without mutating the source collection (cache-safe).
    /// </summary>
    public static class SsvScreenshotListOrder
    {
        /// <summary>
        /// Builds localized sort-by choices for the list toolbar.
        /// </summary>
        /// <returns>Non-empty list of choices.</returns>
        public static List<SsvScreenshotListChoice<SsvScreenshotSortBy>> CreateSortByChoices()
        {
            return new List<SsvScreenshotListChoice<SsvScreenshotSortBy>>
            {
                new SsvScreenshotListChoice<SsvScreenshotSortBy>
                {
                    Value = SsvScreenshotSortBy.Date,
                    Label = ResourceProvider.GetString("LOCSsvSortByDate")
                },
                new SsvScreenshotListChoice<SsvScreenshotSortBy>
                {
                    Value = SsvScreenshotSortBy.Name,
                    Label = ResourceProvider.GetString("LOCSsvSortByName")
                },
                new SsvScreenshotListChoice<SsvScreenshotSortBy>
                {
                    Value = SsvScreenshotSortBy.Size,
                    Label = ResourceProvider.GetString("LOCSsvSortBySize")
                },
                new SsvScreenshotListChoice<SsvScreenshotSortBy>
                {
                    Value = SsvScreenshotSortBy.Type,
                    Label = ResourceProvider.GetString("LOCSsvSortByType")
                }
            };
        }

        /// <summary>
        /// Builds localized sort-direction choices for the list toolbar.
        /// </summary>
        /// <returns>Non-empty list of choices.</returns>
        public static List<SsvScreenshotListChoice<SsvScreenshotSortDirection>> CreateSortDirectionChoices()
        {
            return new List<SsvScreenshotListChoice<SsvScreenshotSortDirection>>
            {
                new SsvScreenshotListChoice<SsvScreenshotSortDirection>
                {
                    Value = SsvScreenshotSortDirection.Ascending,
                    Label = ResourceProvider.GetString("LOCSsvSortAscending")
                },
                new SsvScreenshotListChoice<SsvScreenshotSortDirection>
                {
                    Value = SsvScreenshotSortDirection.Descending,
                    Label = ResourceProvider.GetString("LOCSsvSortDescending")
                }
            };
        }

        /// <summary>
        /// Builds localized media-filter choices for the list toolbar.
        /// </summary>
        /// <returns>Non-empty list of choices.</returns>
        public static List<SsvScreenshotListChoice<SsvScreenshotMediaFilter>> CreateMediaFilterChoices()
        {
            return new List<SsvScreenshotListChoice<SsvScreenshotMediaFilter>>
            {
                new SsvScreenshotListChoice<SsvScreenshotMediaFilter>
                {
                    Value = SsvScreenshotMediaFilter.All,
                    Label = ResourceProvider.GetString("LOCSsvFilterAll")
                },
                new SsvScreenshotListChoice<SsvScreenshotMediaFilter>
                {
                    Value = SsvScreenshotMediaFilter.Images,
                    Label = ResourceProvider.GetString("LOCSsvFilterImages")
                },
                new SsvScreenshotListChoice<SsvScreenshotMediaFilter>
                {
                    Value = SsvScreenshotMediaFilter.Videos,
                    Label = ResourceProvider.GetString("LOCSsvFilterVideos")
                }
            };
        }

        /// <summary>
        /// Keeps screenshots whose files exist on disk, preserving order.
        /// Existence checks run in parallel. Does not mutate <paramref name="candidates"/>.
        /// </summary>
        /// <param name="candidates">Ordered candidate screenshots (may be null).</param>
        /// <returns>New list of screenshots with an existing file; empty when none qualify.</returns>
        public static List<Screenshot> FilterExistingFiles(IEnumerable<Screenshot> candidates)
        {
            int skippedMissing;
            return FilterExistingFiles(candidates, out skippedMissing);
        }

        /// <summary>
        /// Keeps screenshots whose files exist on disk, preserving order.
        /// Existence checks run in parallel. Does not mutate <paramref name="candidates"/>.
        /// </summary>
        /// <param name="candidates">Ordered candidate screenshots (may be null).</param>
        /// <param name="skippedMissing">Count of non-null candidates excluded (empty path or missing file).</param>
        /// <returns>New list of screenshots with an existing file; empty when none qualify.</returns>
        public static List<Screenshot> FilterExistingFiles(IEnumerable<Screenshot> candidates, out int skippedMissing)
        {
            skippedMissing = 0;
            if (candidates == null)
            {
                return new List<Screenshot>();
            }

            List<Screenshot> list = new List<Screenshot>();
            foreach (Screenshot item in candidates)
            {
                if (item != null)
                {
                    list.Add(item);
                }
            }

            if (list.Count == 0)
            {
                return list;
            }

            bool[] exists = new bool[list.Count];
            Parallel.For(0, list.Count, i =>
            {
                string fileName = list[i].FileName;
                exists[i] = !string.IsNullOrEmpty(fileName) && File.Exists(fileName);
            });

            List<Screenshot> existing = new List<Screenshot>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (exists[i])
                {
                    existing.Add(list[i]);
                }
                else
                {
                    skippedMissing++;
                }
            }

            return existing;
        }

        /// <summary>
        /// Returns a new list of existing files, filtered by <paramref name="mediaFilter"/> then sorted by
        /// <paramref name="sortBy"/> / <paramref name="sortDirection"/>.
        /// Never calls <c>Sort</c> on <paramref name="source"/>.
        /// </summary>
        /// <param name="source">Source items (may be null).</param>
        /// <param name="mediaFilter">Media-type filter.</param>
        /// <param name="sortBy">Sort criterion.</param>
        /// <param name="sortDirection">Sort direction.</param>
        /// <returns>New list; empty when <paramref name="source"/> is null or empty.</returns>
        public static List<Screenshot> Apply(
            IEnumerable<Screenshot> source,
            SsvScreenshotMediaFilter mediaFilter,
            SsvScreenshotSortBy sortBy,
            SsvScreenshotSortDirection sortDirection)
        {
            int skippedMissing;
            return Apply(source, mediaFilter, sortBy, sortDirection, out skippedMissing);
        }

        /// <summary>
        /// Returns a new list of existing files, filtered by <paramref name="mediaFilter"/> then sorted by
        /// <paramref name="sortBy"/> / <paramref name="sortDirection"/>.
        /// Never calls <c>Sort</c> on <paramref name="source"/>.
        /// </summary>
        /// <param name="source">Source items (may be null).</param>
        /// <param name="mediaFilter">Media-type filter.</param>
        /// <param name="sortBy">Sort criterion.</param>
        /// <param name="sortDirection">Sort direction.</param>
        /// <param name="skippedMissing">Count of non-null candidates excluded (empty path or missing file).</param>
        /// <returns>New list; empty when <paramref name="source"/> is null or empty.</returns>
        public static List<Screenshot> Apply(
            IEnumerable<Screenshot> source,
            SsvScreenshotMediaFilter mediaFilter,
            SsvScreenshotSortBy sortBy,
            SsvScreenshotSortDirection sortDirection,
            out int skippedMissing)
        {
            List<Screenshot> existing = FilterExistingFiles(source, out skippedMissing);
            List<Screenshot> result = new List<Screenshot>(existing.Count);

            foreach (Screenshot item in existing)
            {
                if (!MatchesFilter(item, mediaFilter))
                {
                    continue;
                }

                result.Add(item);
            }

            result.Sort((x, y) => Compare(x, y, sortBy, sortDirection));
            return result;
        }

        /// <summary>
        /// Computes image/video counts and sizes for <paramref name="items"/> (no filter).
        /// Call with the filtered list when counters must follow the media filter.
        /// </summary>
        /// <param name="items">Items to aggregate (may be null).</param>
        /// <returns>Non-null stats; zeros when <paramref name="items"/> is null or empty.</returns>
        public static SsvScreenshotListStats ComputeStats(IEnumerable<Screenshot> items)
        {
            SsvScreenshotListStats stats = new SsvScreenshotListStats();
            if (items == null)
            {
                return stats;
            }

            foreach (Screenshot item in items)
            {
                if (item == null)
                {
                    continue;
                }

                long size = item.FileSize;
                if (item.IsVideo)
                {
                    stats.VideoCount++;
                    stats.VideoTotalSize += size;
                }
                else
                {
                    stats.ImageCount++;
                    stats.ImageTotalSize += size;
                }
            }

            return stats;
        }

        private static bool MatchesFilter(Screenshot item, SsvScreenshotMediaFilter mediaFilter)
        {
            switch (mediaFilter)
            {
                case SsvScreenshotMediaFilter.Images:
                    return !item.IsVideo;
                case SsvScreenshotMediaFilter.Videos:
                    return item.IsVideo;
                case SsvScreenshotMediaFilter.All:
                default:
                    return true;
            }
        }

        private static int Compare(
            Screenshot x,
            Screenshot y,
            SsvScreenshotSortBy sortBy,
            SsvScreenshotSortDirection sortDirection)
        {
            int comparison;
            switch (sortBy)
            {
                case SsvScreenshotSortBy.Name:
                    comparison = string.Compare(
                        x.FileNameOnly,
                        y.FileNameOnly,
                        StringComparison.CurrentCultureIgnoreCase);
                    break;
                case SsvScreenshotSortBy.Size:
                    comparison = x.FileSize.CompareTo(y.FileSize);
                    break;
                case SsvScreenshotSortBy.Type:
                    comparison = x.IsVideo.CompareTo(y.IsVideo);
                    break;
                case SsvScreenshotSortBy.Date:
                default:
                    comparison = x.Modifed.CompareTo(y.Modifed);
                    break;
            }

            if (sortDirection == SsvScreenshotSortDirection.Descending)
            {
                comparison = -comparison;
            }

            return comparison;
        }
    }
}
