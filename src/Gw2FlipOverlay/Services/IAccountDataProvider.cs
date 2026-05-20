using Gw2FlipOverlay.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public interface IAccountDataProvider {

    string SourceName { get; }

    Task<AccountSnapshot> GetSnapshotAsync(string apiKey, CancellationToken cancellationToken);
}
