using Gw2FlipOverlay.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public interface IMarketDataProvider {

    string SourceName { get; }

    Task<MarketScanResult> GetScanAsync(FlipQueryOptions queryOptions, ScanExecutionMode scanMode, CancellationToken cancellationToken, IProgress<ScanProgressUpdate> progress = null);
}
