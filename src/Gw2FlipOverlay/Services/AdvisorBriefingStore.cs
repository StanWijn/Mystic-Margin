using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class AdvisorBriefingStore {

    private readonly string _briefingPath;

    public AdvisorBriefingStore(string briefingPath = null) {
        _briefingPath = string.IsNullOrWhiteSpace(briefingPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "advisor-briefing.json")
            : briefingPath;

        var directory = Path.GetDirectoryName(_briefingPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<AdvisorBriefing> TryLoadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_briefingPath)) {
            return null;
        }

        using (var stream = File.OpenRead(_briefingPath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<AdvisorBriefing>(json);
        }
    }

    public async Task SaveAsync(AdvisorBriefing briefing, CancellationToken cancellationToken) {
        if (briefing == null) {
            return;
        }

        var directory = Path.GetDirectoryName(_briefingPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_briefingPath))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(briefing, Formatting.Indented));
        }
    }
}
