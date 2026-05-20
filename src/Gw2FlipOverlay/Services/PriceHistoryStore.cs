using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class PriceHistoryStore {

    private readonly string _rootPath;

    public PriceHistoryStore(string rootPath = null) {
        _rootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "price-history")
            : rootPath;

        Directory.CreateDirectory(_rootPath);
    }

    public string RootPath => _rootPath;

    public async Task<PriceHistoryLoadResult> TryLoadLatestSnapshotAsync(CancellationToken cancellationToken) {
        var snapshots = await TryLoadRecentSnapshotsAsync(1, cancellationToken);
        return snapshots.FirstOrDefault() ?? new PriceHistoryLoadResult();
    }

    public async Task<PriceHistoryPairLoadResult> TryLoadLatestSnapshotPairAsync(CancellationToken cancellationToken) {
        var snapshots = await TryLoadRecentSnapshotsAsync(2, cancellationToken);

        return new PriceHistoryPairLoadResult() {
            CurrentSnapshot = snapshots.ElementAtOrDefault(0) ?? new PriceHistoryLoadResult(),
            PreviousSnapshot = snapshots.ElementAtOrDefault(1) ?? new PriceHistoryLoadResult()
        };
    }

    public async Task<IReadOnlyList<PriceHistoryLoadResult>> TryLoadRecentSnapshotsAsync(int count, CancellationToken cancellationToken) {
        return await LoadRecentSnapshotsAsync(count, cancellationToken);
    }

    public async Task<PriceHistorySaveResult> SaveSnapshotAsync(IReadOnlyList<PriceSnapshotEntry> rows, DateTimeOffset recordedAtUtc, int retentionDays, CancellationToken cancellationToken) {
        Directory.CreateDirectory(_rootPath);

        var dayPath = Path.Combine(_rootPath, recordedAtUtc.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayPath);

        var snapshotPath = Path.Combine(dayPath, recordedAtUtc.ToString("yyyyMMdd-HHmmss") + ".jsonl.gz");

        using (var fileStream = File.Create(snapshotPath))
        using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzipStream)) {
            foreach (var row in rows) {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(JsonConvert.SerializeObject(row));
            }
        }

        DeleteOlderThan(recordedAtUtc.AddDays(-Math.Max(1, retentionDays)));

        return new PriceHistorySaveResult() {
            SnapshotPath = snapshotPath,
            SnapshotCount = CountSnapshots()
        };
    }

    private async Task<IReadOnlyList<PriceHistoryLoadResult>> LoadRecentSnapshotsAsync(int count, CancellationToken cancellationToken) {
        var snapshotPaths = GetSnapshotPaths()
            .Take(Math.Max(1, count))
            .ToList();

        var snapshots = new List<PriceHistoryLoadResult>(snapshotPaths.Count);

        foreach (var snapshotPath in snapshotPaths) {
            if (!File.Exists(snapshotPath)) {
                continue;
            }

            snapshots.Add(await LoadSnapshotAsync(snapshotPath, cancellationToken));
        }

        return snapshots;
    }

    private async Task<PriceHistoryLoadResult> LoadSnapshotAsync(string snapshotPath, CancellationToken cancellationToken) {
        var rows = new Dictionary<int, PriceSnapshotEntry>();

        using (var fileStream = File.OpenRead(snapshotPath))
        using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzipStream)) {
            while (!reader.EndOfStream) {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                var row = JsonConvert.DeserializeObject<PriceSnapshotEntry>(line);

                if (row != null) {
                    rows[row.ItemId] = row;
                }
            }
        }

        return new PriceHistoryLoadResult() {
            Rows = rows,
            SnapshotCount = CountSnapshots(),
            SnapshotPath = snapshotPath,
            RecordedAtUtc = rows.Values.FirstOrDefault()?.RecordedAtUtc
        };
    }

    private IEnumerable<string> GetSnapshotPaths() {
        if (!Directory.Exists(_rootPath)) {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(_rootPath, "*.jsonl.gz", SearchOption.AllDirectories)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private int CountSnapshots() {
        return Directory.Exists(_rootPath)
            ? Directory.EnumerateFiles(_rootPath, "*.jsonl.gz", SearchOption.AllDirectories).Count()
            : 0;
    }

    private void DeleteOlderThan(DateTimeOffset minimumUtc) {
        if (!Directory.Exists(_rootPath)) {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_rootPath, "*.jsonl.gz", SearchOption.AllDirectories)) {
            var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));

            if (!DateTimeOffset.TryParseExact(fileName, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)) {
                continue;
            }

            if (timestamp < minimumUtc) {
                File.Delete(path);
            }
        }
    }
}

public sealed class PriceHistoryLoadResult {

    public IReadOnlyDictionary<int, PriceSnapshotEntry> Rows { get; set; } = new Dictionary<int, PriceSnapshotEntry>();

    public int SnapshotCount { get; set; }

    public string SnapshotPath { get; set; } = string.Empty;

    public DateTimeOffset? RecordedAtUtc { get; set; }
}

public sealed class PriceHistoryPairLoadResult {

    public PriceHistoryLoadResult CurrentSnapshot { get; set; } = new PriceHistoryLoadResult();

    public PriceHistoryLoadResult PreviousSnapshot { get; set; } = new PriceHistoryLoadResult();
}

public sealed class PriceHistorySaveResult {

    public int SnapshotCount { get; set; }

    public string SnapshotPath { get; set; } = string.Empty;
}
