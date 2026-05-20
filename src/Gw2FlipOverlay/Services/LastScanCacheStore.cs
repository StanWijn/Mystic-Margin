using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class LastScanCacheStore {

    private readonly string _cacheDirectory;

    public LastScanCacheStore(string cacheDirectory = null) {
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache")
            : cacheDirectory;

        if (!string.IsNullOrWhiteSpace(_cacheDirectory)) {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    public async Task<MarketScanResult> TryLoadAsync(OpportunityMode opportunityMode, CancellationToken cancellationToken) {
        var cachePath = GetCachePath(opportunityMode);

        if (!File.Exists(cachePath)) {
            return null;
        }

        using (var stream = File.OpenRead(cachePath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<MarketScanResult>(json);
        }
    }

    public async Task SaveAsync(OpportunityMode opportunityMode, MarketScanResult scanResult, CancellationToken cancellationToken) {
        if (scanResult == null) {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_cacheDirectory)) {
            Directory.CreateDirectory(_cacheDirectory);
        }

        using (var stream = File.Create(GetCachePath(opportunityMode)))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(scanResult, Formatting.Indented));
        }
    }

    private string GetCachePath(OpportunityMode opportunityMode) {
        var fileName = opportunityMode switch {
            OpportunityMode.Craft => "last-scan-craft.json",
            OpportunityMode.Cooldown => "last-scan-cooldown.json",
            OpportunityMode.Investment => "last-scan-investment.json",
            OpportunityMode.Value => "last-scan-value.json",
            _ => "last-scan-flip.json"
        };

        return Path.Combine(_cacheDirectory, fileName);
    }
}
