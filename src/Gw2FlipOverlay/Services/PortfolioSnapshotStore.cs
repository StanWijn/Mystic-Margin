using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class PortfolioSnapshotStore {

    private readonly string _snapshotPath;

    public PortfolioSnapshotStore(string snapshotPath = null) {
        _snapshotPath = string.IsNullOrWhiteSpace(snapshotPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "portfolio-snapshots.json")
            : snapshotPath;

        var directory = Path.GetDirectoryName(_snapshotPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IReadOnlyList<PortfolioSnapshot>> LoadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_snapshotPath)) {
            return Array.Empty<PortfolioSnapshot>();
        }

        using (var stream = File.OpenRead(_snapshotPath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json)) {
                return Array.Empty<PortfolioSnapshot>();
            }

            try {
                var token = JToken.Parse(json);

                if (token.Type == JTokenType.Array) {
                    return token.ToObject<List<PortfolioSnapshot>>() ?? new List<PortfolioSnapshot>();
                }

                if (token.Type == JTokenType.Object) {
                    var snapshot = token.ToObject<PortfolioSnapshot>();
                    return snapshot == null
                        ? Array.Empty<PortfolioSnapshot>()
                        : new List<PortfolioSnapshot>() { snapshot };
                }
            } catch (JsonException) {
                return Array.Empty<PortfolioSnapshot>();
            }

            return Array.Empty<PortfolioSnapshot>();
        }
    }

    public async Task SaveSnapshotAsync(PortfolioSnapshot snapshot, int retentionDays, CancellationToken cancellationToken) {
        if (snapshot == null) {
            return;
        }

        var existing = (await LoadAsync(cancellationToken)).ToList();
        existing.Add(snapshot);
        var minimumTimestamp = snapshot.CapturedAtUtc.AddDays(-Math.Max(7, retentionDays));
        existing = existing
            .Where(entry => entry.CapturedAtUtc >= minimumTimestamp)
            .OrderBy(entry => entry.CapturedAtUtc)
            .ToList();

        var directory = Path.GetDirectoryName(_snapshotPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_snapshotPath))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(existing, Formatting.Indented));
        }
    }
}
