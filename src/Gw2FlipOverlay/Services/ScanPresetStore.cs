using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class ScanPresetStore {

    private static readonly HashSet<string> LegacyDefaultPresetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "money-printer",
        "low-capital-mats",
        "high-confidence",
        "value-dips",
        "watchlist-sniper",
        "cooldown-daily",
        "investment-watch"
    };

    private static readonly HashSet<string> GoldTunedDefaultPresetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "starter-volume",
        "daily-volume",
        "craft-margin",
        "deep-value",
        "daily-cooldowns",
        "seasonal-watch"
    };

    private readonly string _presetPath;

    public ScanPresetStore(string presetPath = null) {
        _presetPath = string.IsNullOrWhiteSpace(presetPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "scan-presets.json")
            : presetPath;

        var directory = Path.GetDirectoryName(_presetPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<List<ScanPreset>> LoadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_presetPath)) {
            var defaults = ScanPreset.CreateDefaults();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        string json;
        using (var stream = File.OpenRead(_presetPath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            json = await reader.ReadToEndAsync();
        }

        var loaded = JsonConvert.DeserializeObject<List<ScanPreset>>(json) ?? new List<ScanPreset>();

        if (loaded.Count == 0) {
            loaded = ScanPreset.CreateDefaults();
        }

        if (loaded.Any(preset => LegacyDefaultPresetIds.Contains(preset.Id)) ||
            loaded.Any(IsStaleGoldTunedDefaultPreset) ||
            GoldTunedDefaultPresetIds.Any(defaultId => loaded.All(preset => !string.Equals(preset.Id, defaultId, StringComparison.OrdinalIgnoreCase)))) {
            var customPresets = loaded
                .Where(preset =>
                    !LegacyDefaultPresetIds.Contains(preset.Id) &&
                    !GoldTunedDefaultPresetIds.Contains(preset.Id))
                .ToList();
            loaded = ScanPreset.CreateDefaults()
                .Concat(customPresets)
                .ToList();
            await SaveAsync(loaded, cancellationToken);
        }

        foreach (var preset in loaded.Where(preset => preset.AlertRules == null || preset.AlertRules.Count == 0)) {
            preset.AlertRules = AlertRule.CreateDefaultRules();
        }

        return loaded;
    }

    private static bool IsStaleGoldTunedDefaultPreset(ScanPreset preset) {
        if (preset == null || string.IsNullOrWhiteSpace(preset.Id)) {
            return false;
        }

        if (string.Equals(preset.Id, "starter-volume", StringComparison.OrdinalIgnoreCase)) {
            return preset.MinimumMarketDepth >= 3000 ||
                   preset.MinimumProfitCopper >= 300 ||
                   preset.MinimumRoiPercent >= 5 ||
                   preset.MaxVolatilityPercent <= 15 ||
                   preset.MaxAcquireCostCopper > 50000;
        }

        if (string.Equals(preset.Id, "daily-volume", StringComparison.OrdinalIgnoreCase)) {
            return preset.MinimumMarketDepth >= 3000 ||
                   preset.MinimumProfitCopper >= 800 ||
                   preset.MinimumRoiPercent >= 6 ||
                   preset.MaxVolatilityPercent <= 15 ||
                   preset.MaxAcquireCostCopper > 200000;
        }

        if (string.Equals(preset.Id, "craft-margin", StringComparison.OrdinalIgnoreCase)) {
            return preset.MinimumMarketDepth >= 1500 ||
                   preset.MaxVolatilityPercent <= 18;
        }

        if (string.Equals(preset.Id, "deep-value", StringComparison.OrdinalIgnoreCase)) {
            return preset.MinimumMarketDepth >= 5000 ||
                   preset.MinimumProfitCopper >= 1000 ||
                   preset.MaxVolatilityPercent <= 15;
        }

        return false;
    }

    public async Task SaveAsync(IReadOnlyList<ScanPreset> presets, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_presetPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_presetPath))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(presets ?? Array.Empty<ScanPreset>(), Formatting.Indented));
        }
    }
}
