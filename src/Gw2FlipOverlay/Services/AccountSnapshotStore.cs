using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class AccountSnapshotStore {

    private readonly string _snapshotPath;

    public AccountSnapshotStore(string snapshotPath = null) {
        _snapshotPath = string.IsNullOrWhiteSpace(snapshotPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "account-snapshot.json")
            : snapshotPath;

        var directory = Path.GetDirectoryName(_snapshotPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<AccountSnapshot> TryLoadAsync(CancellationToken cancellationToken) {
        if (!File.Exists(_snapshotPath)) {
            return new AccountSnapshot();
        }

        using (var stream = File.OpenRead(_snapshotPath))
        using (var reader = new StreamReader(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<AccountSnapshot>(json) ?? new AccountSnapshot();
        }
    }

    public async Task SaveAsync(AccountSnapshot snapshot, CancellationToken cancellationToken) {
        if (snapshot == null) {
            return;
        }

        var directory = Path.GetDirectoryName(_snapshotPath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_snapshotPath))
        using (var writer = new StreamWriter(stream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        }
    }
}
