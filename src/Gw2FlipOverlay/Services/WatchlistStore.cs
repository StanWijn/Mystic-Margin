using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class WatchlistStore {

    private readonly string _watchlistPath;

    public WatchlistStore(string watchlistPath = null) {
        _watchlistPath = string.IsNullOrWhiteSpace(watchlistPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "watchlist.json")
            : watchlistPath;

        var directory = Path.GetDirectoryName(_watchlistPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<HashSet<int>> TryLoadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_watchlistPath)) {
            return new HashSet<int>();
        }

        using (var stream = File.OpenRead(_watchlistPath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();
            var ids = JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
            return new HashSet<int>(ids);
        }
    }

    public async Task SaveAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_watchlistPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        var ids = (itemIds ?? Array.Empty<int>()).Distinct().OrderBy(id => id).ToList();

        using (var stream = File.Create(_watchlistPath))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(ids, Formatting.Indented));
        }
    }
}
