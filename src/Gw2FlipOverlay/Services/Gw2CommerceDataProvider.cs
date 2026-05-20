using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class Gw2CommerceDataProvider : IMarketDataProvider, IDisposable {

    private static readonly HashSet<string> PracticalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "CraftingMaterial",
        "Consumable",
        "UpgradeComponent",
        "Trophy",
        "Container"
    };

    private static readonly HashSet<string> CraftDisciplines = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "Artificer",
        "Armorsmith",
        "Chef",
        "Huntsman",
        "Jeweler",
        "Leatherworker",
        "Tailor",
        "Weaponsmith"
    };

    private const int PageSize = 200;
    private const int PageBatchSize = 6;
    private const decimal MinimumSpreadPercent = 4.0m;
    private const decimal MaximumSpreadPercent = 35.0m;
    private const int CandidatePoolCap = 1200;
    private const int ValueHistoryWindow = 8;
    private const int MinimumValueHistoryPoints = 3;
    private const decimal MinimumValueDiscountPercent = 2.0m;
    private const decimal MaximumValueDiscountPercent = 55.0m;
    private const decimal MinimumFlipTurnoverScore = 0.35m;
    private const decimal MinimumCraftTurnoverScore = 0.20m;
    private const decimal MinimumValueTurnoverScore = 0.20m;
    private const decimal MinimumInvestmentDiscountPercent = 6.0m;
    private const int MinimumInvestmentHistoryPoints = 4;
    private static readonly string[] CooldownKeywords = {
        "Deldrimor Steel",
        "Damask",
        "Spiritwood",
        "Elonian Leather",
        "Charged Quartz",
        "Grow Lamp",
        "Skyscale Food",
        "Jeweled Damask"
    };
    private static readonly string[] InvestmentKeywords = {
        "Super ",
        "Bauble",
        "Moto",
        "Jorbreaker",
        "Dragon Coffer",
        "Zephyrite",
        "Candy Corn",
        "Trick-or-Treat",
        "Wintersday",
        "Snowflake",
        "Lucky Envelope",
        "Red Lantern"
    };

    private readonly HttpClient _httpClient;
    private readonly FlipScoringService _scoringService;
    private readonly PriceHistoryStore _historyStore;
    private readonly RecipeCacheStore _recipeCacheStore;

    public Gw2CommerceDataProvider(FlipScoringService scoringService, PriceHistoryStore historyStore) {
        _scoringService = scoringService;
        _historyStore = historyStore;
        _recipeCacheStore = new RecipeCacheStore();
        ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 12);
        ServicePointManager.Expect100Continue = false;
        _httpClient = new HttpClient() {
            BaseAddress = new Uri("https://api.guildwars2.com/v2/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string SourceName => "live commerce scan";

    public async Task<MarketScanResult> GetScanAsync(FlipQueryOptions queryOptions, ScanExecutionMode scanMode, CancellationToken cancellationToken, IProgress<ScanProgressUpdate> progress = null) {
        var snapshotPair = scanMode == ScanExecutionMode.Full
            ? await LoadPairForFullScanAsync(cancellationToken)
            : await _historyStore.TryLoadLatestSnapshotPairAsync(cancellationToken);

        IReadOnlyList<PriceSnapshotEntry> currentRows;
        DateTimeOffset recordedAtUtc;
        var historySaveResult = new PriceHistorySaveResult() {
            SnapshotCount = snapshotPair.CurrentSnapshot.SnapshotCount,
            SnapshotPath = snapshotPair.CurrentSnapshot.SnapshotPath
        };

        if (scanMode == ScanExecutionMode.Full) {
            progress?.Report(new ScanProgressUpdate() {
                StatusMessage = $"Running full scan for {GetModeLabel(queryOptions.OpportunityMode)}..."
            });

            var fetchedRows = await FetchAllPricePagesAsync(queryOptions.OpportunityMode, progress, cancellationToken).ConfigureAwait(false);
            recordedAtUtc = DateTimeOffset.UtcNow;

            currentRows = fetchedRows
                .Select(price => new PriceSnapshotEntry() {
                    ItemId = price.Id,
                    HighestBuy = price.Buys?.UnitPrice ?? 0,
                    LowestSell = price.Sells?.UnitPrice ?? 0,
                    BuyQuantity = price.Buys?.Quantity ?? 0,
                    SellQuantity = price.Sells?.Quantity ?? 0,
                    RecordedAtUtc = recordedAtUtc
                })
                .ToList();

            historySaveResult = await _historyStore.SaveSnapshotAsync(currentRows, recordedAtUtc, queryOptions.HistoryRetentionDays, cancellationToken).ConfigureAwait(false);
        } else {
            if (snapshotPair.CurrentSnapshot.Rows.Count == 0) {
                throw new InvalidOperationException("Quick scan requires a previous full scan. Run Full Scan first.");
            }

            recordedAtUtc = snapshotPair.CurrentSnapshot.RecordedAtUtc ?? DateTimeOffset.UtcNow;
            currentRows = snapshotPair.CurrentSnapshot.Rows.Values.ToList();
            progress?.Report(new ScanProgressUpdate() {
                StatusMessage = $"Running quick scan from cached prices saved at {recordedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}..."
            });
        }

        var previousRows = snapshotPair.PreviousSnapshot.Rows;
        var recentSnapshots = await _historyStore.TryLoadRecentSnapshotsAsync(ValueHistoryWindow, cancellationToken).ConfigureAwait(false);
        List<FlipCandidate> candidateUniverse;

        switch (queryOptions.OpportunityMode) {
            case OpportunityMode.Craft:
                candidateUniverse = await BuildCraftCandidatesAsync(queryOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);
                break;
            case OpportunityMode.Cooldown:
                candidateUniverse = await BuildCooldownCandidatesAsync(queryOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);
                break;
            case OpportunityMode.Investment:
                candidateUniverse = await BuildInvestmentCandidatesAsync(queryOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);
                break;
            case OpportunityMode.Value:
                candidateUniverse = await BuildValueCandidatesAsync(queryOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);
                break;
            default:
                candidateUniverse = await BuildFlipCandidatesAsync(queryOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);
                break;
        }

        var fullUniverse = ApplySort(candidateUniverse, FlipSortMode.Score)
            .Take(CandidatePoolCap)
            .ToList();

        var result = new MarketScanResult() {
            Candidates = fullUniverse,
            TotalPriceRows = currentRows.Count,
            SavedSnapshotCount = historySaveResult.SnapshotCount,
            SnapshotRootPath = _historyStore.RootPath,
            SourceName = BuildSourceName(queryOptions.OpportunityMode, scanMode),
            GeneratedAtUtc = recordedAtUtc,
            OpportunityMode = queryOptions.OpportunityMode,
            UniverseCandidateCount = fullUniverse.Count,
            FilteredCandidateCount = fullUniverse.Count,
            ActivePresetName = queryOptions.ActivePresetName ?? string.Empty
        };

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = scanMode == ScanExecutionMode.Full
                ? $"Full scan finished with {fullUniverse.Count} candidates."
                : $"Quick scan rebuilt {fullUniverse.Count} candidates from cached market data.",
            PartialResult = result
        });

        return result;
    }

    public void Dispose() {
        _httpClient.Dispose();
    }

    private async Task<PriceHistoryPairLoadResult> LoadPairForFullScanAsync(CancellationToken cancellationToken) {
        return await _historyStore.TryLoadLatestSnapshotPairAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<FlipCandidate>> BuildFlipCandidatesAsync(FlipQueryOptions queryOptions, IReadOnlyList<PriceSnapshotEntry> currentRows, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyList<PriceHistoryLoadResult> recentSnapshots, DateTimeOffset recordedAtUtc, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        var historyByItem = BuildHistoricalSellMap(recentSnapshots);
        var priceHistoryByItem = BuildPriceHistoryMap(recentSnapshots);
        var roughCandidates = currentRows
            .Select(row => CreateFlipCandidate(row, previousRows, historyByItem, priceHistoryByItem))
            .Where(candidate =>
                candidate.HighestBuy > 0 &&
                candidate.LowestSell > 0 &&
                candidate.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                candidate.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                candidate.SpreadPercent >= MinimumSpreadPercent &&
                candidate.SpreadPercent <= MaximumSpreadPercent &&
                candidate.MarketDepth >= queryOptions.MinimumMarketDepth &&
                candidate.TurnoverScore >= MinimumFlipTurnoverScore &&
                PassesAcquireCap(candidate, queryOptions.MaxAcquireCostCopper))
            .OrderByDescending(candidate => candidate.FastFlipScore)
            .ThenByDescending(candidate => candidate.EstimatedProfit)
            .Take(CandidatePoolCap)
            .ToList();

        if (roughCandidates.Count == 0) {
            return new List<FlipCandidate>();
        }

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Ranked {roughCandidates.Count} provisional flip candidates. Enriching item names...",
            PartialResult = CreatePartialResult(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, roughCandidates.Count)
        });

        await EnrichCandidatesWithItemsAsync(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, progress, cancellationToken).ConfigureAwait(false);

        return roughCandidates
            .Where(candidate => !queryOptions.PracticalOnly || IsPracticalType(candidate.ItemType))
            .ToList();
    }

    private async Task<List<FlipCandidate>> BuildCraftCandidatesAsync(FlipQueryOptions queryOptions, IReadOnlyList<PriceSnapshotEntry> currentRows, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyList<PriceHistoryLoadResult> recentSnapshots, DateTimeOffset recordedAtUtc, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = "Loading recipe cache and rebuilding craft opportunities..."
        });

        var recipes = await _recipeCacheStore.GetRecipesAsync(_httpClient, cancellationToken).ConfigureAwait(false);
        var priceMap = currentRows.ToDictionary(row => row.ItemId, row => row);
        var historyByItem = BuildHistoricalSellMap(recentSnapshots);
        var priceHistoryByItem = BuildPriceHistoryMap(recentSnapshots);

        var roughCandidates = recipes
            .Where(recipe =>
                recipe.OutputItemId > 0 &&
                recipe.OutputItemCount > 0 &&
                recipe.Ingredients.Count > 0 &&
                recipe.Disciplines.Any(discipline => CraftDisciplines.Contains(discipline)))
            .Select(recipe => CreateCraftCandidate(recipe, priceMap, previousRows, historyByItem, priceHistoryByItem))
            .Where(candidate =>
                candidate != null &&
                candidate.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                candidate.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                candidate.MarketDepth >= queryOptions.MinimumMarketDepth &&
                candidate.TurnoverScore >= MinimumCraftTurnoverScore &&
                PassesAcquireCap(candidate, queryOptions.MaxAcquireCostCopper))
            .OrderByDescending(candidate => candidate.FastFlipScore)
            .ThenByDescending(candidate => candidate.EstimatedProfit)
            .Take(CandidatePoolCap)
            .ToList();

        if (roughCandidates.Count == 0) {
            return new List<FlipCandidate>();
        }

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Ranked {roughCandidates.Count} provisional craft candidates. Enriching item names...",
            PartialResult = CreatePartialResult(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, roughCandidates.Count)
        });

        await EnrichCandidatesWithItemsAsync(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, progress, cancellationToken).ConfigureAwait(false);

        return roughCandidates
            .Where(candidate => !queryOptions.PracticalOnly || IsPracticalType(candidate.ItemType))
            .ToList();
    }

    private async Task<List<FlipCandidate>> BuildValueCandidatesAsync(FlipQueryOptions queryOptions, IReadOnlyList<PriceSnapshotEntry> currentRows, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyList<PriceHistoryLoadResult> recentSnapshots, DateTimeOffset recordedAtUtc, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = "Comparing live sell floors against recent local fair values..."
        });

        var historyByItem = BuildHistoricalSellMap(recentSnapshots);
        var priceHistoryByItem = BuildPriceHistoryMap(recentSnapshots);
        var roughCandidates = currentRows
            .Select(row => CreateValueCandidate(row, previousRows, historyByItem, priceHistoryByItem))
            .Where(candidate =>
                candidate != null &&
                candidate.LowestSell > 0 &&
                candidate.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                candidate.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                candidate.MarketDepth >= queryOptions.MinimumMarketDepth &&
                candidate.TurnoverScore >= MinimumValueTurnoverScore &&
                candidate.HistoricalSampleCount >= MinimumValueHistoryPoints &&
                candidate.DiscountPercent >= Math.Max(MinimumValueDiscountPercent, queryOptions.MinimumDiscountPercent) &&
                candidate.DiscountPercent <= MaximumValueDiscountPercent &&
                PassesAcquireCap(candidate, queryOptions.MaxAcquireCostCopper))
            .OrderByDescending(candidate => candidate.ValueScore)
            .ThenByDescending(candidate => candidate.EstimatedProfit)
            .Take(CandidatePoolCap)
            .ToList();

        if (roughCandidates.Count == 0) {
            return new List<FlipCandidate>();
        }

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Ranked {roughCandidates.Count} provisional value deals. Enriching item names...",
            PartialResult = CreatePartialResult(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, roughCandidates.Count)
        });

        await EnrichCandidatesWithItemsAsync(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, progress, cancellationToken).ConfigureAwait(false);

        return roughCandidates
            .Where(candidate => !queryOptions.PracticalOnly || IsPracticalType(candidate.ItemType))
            .ToList();
    }

    private async Task EnrichCandidatesWithItemsAsync(List<FlipCandidate> candidates, DateTimeOffset recordedAtUtc, OpportunityMode opportunityMode, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        var candidateIds = candidates
            .Select(candidate => candidate.ItemId)
            .Concat(candidates.SelectMany(candidate => candidate.CraftIngredients ?? new List<CraftIngredientNeed>()).Select(ingredient => ingredient.ItemId))
            .Distinct()
            .ToArray();
        var totalChunks = (int)Math.Ceiling(candidateIds.Length / (double)PageSize);
        var chunkIndex = 0;

        foreach (var chunk in Chunk(candidateIds, PageSize)) {
            cancellationToken.ThrowIfCancellationRequested();

            var joinedIds = string.Join(",", chunk);
            var response = await _httpClient.GetAsync($"items?ids={joinedIds}", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var items = await DeserializeResponseAsync<List<ItemDto>>(response).ConfigureAwait(false);
            var itemMap = items.ToDictionary(item => item.Id, item => item);

            foreach (var candidate in candidates) {
                if (!itemMap.TryGetValue(candidate.ItemId, out var item)) {
                    continue;
                }

                candidate.ItemName = item?.Name ?? candidate.ItemName;
                candidate.ItemType = item?.Type ?? candidate.ItemType;
                candidate.Rarity = item?.Rarity ?? candidate.Rarity;
                candidate.IconUrl = item?.Icon ?? candidate.IconUrl;

                foreach (var ingredient in candidate.CraftIngredients ?? new List<CraftIngredientNeed>()) {
                    if (itemMap.TryGetValue(ingredient.ItemId, out var ingredientItem)) {
                        ingredient.ItemName = ingredientItem?.Name ?? ingredient.ItemName;
                    }
                }
            }

            chunkIndex++;

            progress?.Report(new ScanProgressUpdate() {
                StatusMessage = $"Resolved item details batch {chunkIndex}/{totalChunks}...",
                PartialResult = CreatePartialResult(candidates, recordedAtUtc, opportunityMode, scanMode, candidates.Count)
            });
        }
    }

    private FlipCandidate CreateFlipCandidate(PriceSnapshotEntry row, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyDictionary<int, List<int>> historicalSellMap, IReadOnlyDictionary<int, List<PriceSnapshotEntry>> priceHistoryMap) {
        var candidate = _scoringService.CreatePriceCandidate(row.ItemId, row.HighestBuy, row.LowestSell);

        previousRows.TryGetValue(row.ItemId, out var previousRow);
        candidate = _scoringService.FinalizeCandidate(candidate, $"Item {row.ItemId}", row.BuyQuantity, row.SellQuantity, SourceName);
        candidate.AcquisitionCostCopper = candidate.HighestBuy;
        candidate.MarketDepth = Math.Min(row.BuyQuantity, row.SellQuantity);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(row.BuyQuantity, row.SellQuantity);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.FastFlipScore = CalculateFastFlipScore(candidate);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, 1.0m);
        candidate.Score = candidate.FastFlipScore;
        candidate.BuyDeltaCopper = previousRow == null ? 0 : candidate.HighestBuy - previousRow.HighestBuy;
        candidate.SellDeltaCopper = previousRow == null ? 0 : candidate.LowestSell - previousRow.LowestSell;
        candidate.PreviousSeenUtc = previousRow?.RecordedAtUtc;
        candidate.OpportunityMode = OpportunityMode.Flip;
        candidate.StrategyTag = AdvisorStrategyTag.FastFlip;
        ApplyHistoricalValuation(candidate, historicalSellMap, row.LowestSell);
        ApplyPriceHistory(candidate, priceHistoryMap, row);
        ApplyAdvisorMetrics(candidate);

        return candidate;
    }

    private FlipCandidate CreateCraftCandidate(RecipeRecord recipe, IReadOnlyDictionary<int, PriceSnapshotEntry> priceMap, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyDictionary<int, List<int>> historicalSellMap, IReadOnlyDictionary<int, List<PriceSnapshotEntry>> priceHistoryMap) {
        if (!priceMap.TryGetValue(recipe.OutputItemId, out var outputPrice) || outputPrice.LowestSell <= 0) {
            return null;
        }

        var totalCraftCost = 0;
        var craftIngredients = new List<CraftIngredientNeed>();

        foreach (var ingredient in recipe.Ingredients) {
            if (!priceMap.TryGetValue(ingredient.ItemId, out var ingredientPrice) || ingredientPrice.LowestSell <= 0) {
                return null;
            }

            totalCraftCost += ingredientPrice.LowestSell * ingredient.Count;
            craftIngredients.Add(new CraftIngredientNeed() {
                ItemId = ingredient.ItemId,
                RequiredCount = ingredient.Count,
                UnitBuyPriceCopper = ingredientPrice.LowestSell
            });
        }

        var grossSellValue = outputPrice.LowestSell * Math.Max(1, recipe.OutputItemCount);
        var netSellValue = (int)Math.Floor(grossSellValue * 0.85m);
        var estimatedProfit = netSellValue - totalCraftCost;

        if (estimatedProfit <= 0) {
            return null;
        }

        previousRows.TryGetValue(recipe.OutputItemId, out var previousRow);

        var candidate = new FlipCandidate() {
            ItemId = recipe.OutputItemId,
            ItemName = $"Craft {recipe.OutputItemId}",
            HighestBuy = totalCraftCost,
            LowestSell = outputPrice.LowestSell,
            NetResaleValue = netSellValue,
            EstimatedProfit = estimatedProfit,
            SpreadPercent = totalCraftCost <= 0 ? 0m : Math.Round((estimatedProfit / (decimal)totalCraftCost) * 100m, 1),
            BuyStackSize = outputPrice.BuyQuantity,
            SellStackSize = outputPrice.SellQuantity,
            AcquisitionCostCopper = totalCraftCost,
            MarketDepth = Math.Min(Math.Max(1, outputPrice.BuyQuantity), Math.Max(1, outputPrice.SellQuantity)),
            Source = "craft-profit scan",
            BuyDeltaCopper = 0,
            SellDeltaCopper = previousRow == null ? 0 : outputPrice.LowestSell - previousRow.LowestSell,
            PreviousSeenUtc = previousRow?.RecordedAtUtc,
            OpportunityMode = OpportunityMode.Craft,
            StrategyTag = AdvisorStrategyTag.CraftMargin,
            CraftIngredients = craftIngredients
        };

        candidate.LiquidityScore = _scoringService.CalculateLiquidityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.StabilityScore = _scoringService.CalculateStabilityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(outputPrice.BuyQuantity, outputPrice.SellQuantity);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.FastFlipScore = CalculateFastFlipScore(candidate);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, 1.0m);
        candidate.Score = candidate.FastFlipScore;
        ApplyHistoricalValuation(candidate, historicalSellMap, outputPrice.LowestSell);
        ApplyPriceHistory(candidate, priceHistoryMap, outputPrice);
        ApplyAdvisorMetrics(candidate);

        return candidate;
    }

    private FlipCandidate CreateValueCandidate(PriceSnapshotEntry row, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyDictionary<int, List<int>> historicalSellMap, IReadOnlyDictionary<int, List<PriceSnapshotEntry>> priceHistoryMap) {
        if (row.LowestSell <= 0 || !historicalSellMap.TryGetValue(row.ItemId, out var historicalSells) || historicalSells.Count < MinimumValueHistoryPoints) {
            return null;
        }

        var fairValue = CalculateMedian(historicalSells);

        if (fairValue <= 0 || row.LowestSell >= fairValue) {
            return null;
        }

        previousRows.TryGetValue(row.ItemId, out var previousRow);
        var buyQuantity = row.BuyQuantity;
        var sellQuantity = row.SellQuantity;
        var netFairResaleValue = _scoringService.CalculateNetResaleValue(fairValue);
        var estimatedProfit = netFairResaleValue - row.LowestSell;

        if (estimatedProfit <= 0) {
            return null;
        }

        var discountPercent = Math.Round(((fairValue - row.LowestSell) / (decimal)fairValue) * 100m, 1);
        var marketDepth = Math.Min(Math.Max(1, buyQuantity), Math.Max(1, sellQuantity));
        var demandPressure = CalculateDemandPressure(buyQuantity, sellQuantity);
        var volumeScore = CalculateVolumeScore(marketDepth);
        var turnoverScore = CalculateTurnoverScore(demandPressure, row.LowestSell);
        var liquidityScore = _scoringService.CalculateLiquidityScoreForDisplay(buyQuantity, sellQuantity);
        var stabilityScore = _scoringService.CalculateStabilityScoreForDisplay(buyQuantity, sellQuantity);
        var reversionStrength = Clamp(discountPercent / 18m, 0.35m, 2.50m);
        var historyConfidence = Clamp(historicalSells.Count / 8m, 0.35m, 1.00m);
        var confidenceScore = CalculateConfidenceScore(liquidityScore, stabilityScore, volumeScore, turnoverScore, historyConfidence);
        var valueScore = Math.Round(estimatedProfit * reversionStrength * (confidenceScore / 100m), 2);

        var candidate = new FlipCandidate() {
            ItemId = row.ItemId,
            ItemName = $"Value {row.ItemId}",
            HighestBuy = row.HighestBuy,
            LowestSell = row.LowestSell,
            NetResaleValue = netFairResaleValue,
            EstimatedProfit = estimatedProfit,
            SpreadPercent = row.LowestSell <= 0 ? 0m : Math.Round((estimatedProfit / (decimal)row.LowestSell) * 100m, 1),
            BuyStackSize = buyQuantity,
            SellStackSize = sellQuantity,
            LiquidityScore = liquidityScore,
            StabilityScore = stabilityScore,
            Score = valueScore,
            Source = "value-deal scan",
            BuyDeltaCopper = previousRow == null ? 0 : row.HighestBuy - previousRow.HighestBuy,
            SellDeltaCopper = previousRow == null ? 0 : row.LowestSell - previousRow.LowestSell,
            PreviousSeenUtc = previousRow?.RecordedAtUtc,
            AcquisitionCostCopper = row.LowestSell,
            MarketDepth = marketDepth,
            VolumeScore = volumeScore,
            TurnoverScore = turnoverScore,
            DemandPressure = demandPressure,
            FastFlipScore = valueScore,
            OpportunityMode = OpportunityMode.Value,
            FairValueCopper = fairValue,
            FairValueRecentMedianCopper = fairValue,
            FairValueWeightedCopper = CalculateWeightedAverage(historicalSells),
            DiscountPercent = discountPercent,
            ValueScore = valueScore,
            HistoricalSampleCount = historicalSells.Count,
            ConfidenceScore = confidenceScore,
            SoldThroughConfidence = Math.Round(historyConfidence * 100m, 1),
            VolatilityPercent = CalculateVolatilityPercent(historicalSells),
            StrategyTag = AdvisorStrategyTag.ValueReversion
        };

        ApplyPriceHistory(candidate, priceHistoryMap, row);
        ApplyAdvisorMetrics(candidate, 0.70m);
        return candidate;
    }

    private async Task<List<FlipCandidate>> BuildCooldownCandidatesAsync(FlipQueryOptions queryOptions, IReadOnlyList<PriceSnapshotEntry> currentRows, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyList<PriceHistoryLoadResult> recentSnapshots, DateTimeOffset recordedAtUtc, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = "Rebuilding daily cooldown crafts from craft-profit data..."
        });

        var cloneOptions = CloneOptions(queryOptions);
        cloneOptions.OpportunityMode = OpportunityMode.Craft;
        cloneOptions.PracticalOnly = false;
        cloneOptions.MinimumMarketDepth = Math.Min(queryOptions.MinimumMarketDepth, 250);
        var craftCandidates = await BuildCraftCandidatesAsync(cloneOptions, currentRows, previousRows, recentSnapshots, recordedAtUtc, scanMode, progress, cancellationToken).ConfigureAwait(false);

        return craftCandidates
            .Where(candidate => IsKeywordMatch(candidate.ItemName, CooldownKeywords))
            .Select(candidate => {
                candidate.OpportunityMode = OpportunityMode.Cooldown;
                candidate.StrategyTag = AdvisorStrategyTag.Cooldown;
                candidate.InvestmentHorizonDays = 1;
                candidate.SeasonWindowState = "Daily cooldown";
                candidate.AdvisorWhyNow = "This craft behaves like a daily converter: limited throughput, but reliable margin when it clears fees.";
                candidate.AdvisorRiskNotes = "Cooldown items can be profitable on paper but still slow down if too many crafters list the same output.";
                ApplyAdvisorMetrics(candidate, 0.85m);
                return candidate;
            })
            .Where(candidate =>
                candidate.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                candidate.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                candidate.MarketDepth >= Math.Min(queryOptions.MinimumMarketDepth, 250) &&
                PassesAcquireCap(candidate, queryOptions.MaxAcquireCostCopper))
            .OrderByDescending(candidate => candidate.AdvisorScore)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .Take(CandidatePoolCap)
            .ToList();
    }

    private async Task<List<FlipCandidate>> BuildInvestmentCandidatesAsync(FlipQueryOptions queryOptions, IReadOnlyList<PriceSnapshotEntry> currentRows, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyList<PriceHistoryLoadResult> recentSnapshots, DateTimeOffset recordedAtUtc, ScanExecutionMode scanMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = "Scanning seasonal and rotation watch candidates..."
        });

        var historyByItem = BuildHistoricalSellMap(recentSnapshots);
        var priceHistoryByItem = BuildPriceHistoryMap(recentSnapshots);
        var roughCandidates = currentRows
            .Select(row => CreateInvestmentCandidate(row, previousRows, historyByItem, priceHistoryByItem))
            .Where(candidate =>
                candidate != null &&
                candidate.MarketDepth >= Math.Max(1, queryOptions.MinimumMarketDepth) &&
                PassesAcquireCap(candidate, queryOptions.MaxAcquireCostCopper))
            .OrderByDescending(candidate => candidate.AdvisorScore)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .Take(CandidatePoolCap)
            .ToList();

        if (roughCandidates.Count == 0) {
            return new List<FlipCandidate>();
        }

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Ranked {roughCandidates.Count} provisional investment watch rows. Enriching item names...",
            PartialResult = CreatePartialResult(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, roughCandidates.Count)
        });

        await EnrichCandidatesWithItemsAsync(roughCandidates, recordedAtUtc, queryOptions.OpportunityMode, scanMode, progress, cancellationToken).ConfigureAwait(false);

        return roughCandidates
            .Where(candidate => IsKeywordMatch(candidate.ItemName, InvestmentKeywords))
            .Select(candidate => {
                candidate.SeasonWindowState = ResolveSeasonWindow(candidate.ItemName, recordedAtUtc);
                candidate.AdvisorWhyNow = candidate.DiscountPercent >= 4m
                    ? "Current price sits below the local fair-value baseline, so the addon sees room to accumulate."
                    : "This looks more like a watch candidate than an immediate buy, but the seasonal family is worth tracking.";
                candidate.AdvisorRiskNotes = "Seasonal items can stay cheap longer than expected, especially while the event is still supplying stock.";
                ApplyAdvisorMetrics(candidate, candidate.DiscountPercent >= 4m ? 0.45m : 0.25m);
                return candidate;
            })
            .Where(candidate =>
                candidate.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                candidate.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                candidate.DiscountPercent >= Math.Max(MinimumInvestmentDiscountPercent, queryOptions.MinimumDiscountPercent) &&
                candidate.HistoricalSampleCount >= MinimumInvestmentHistoryPoints &&
                candidate.TurnoverScore >= MinimumValueTurnoverScore)
            .OrderByDescending(candidate => candidate.AdvisorScore)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .Take(CandidatePoolCap)
            .ToList();
    }

    private FlipCandidate CreateInvestmentCandidate(PriceSnapshotEntry row, IReadOnlyDictionary<int, PriceSnapshotEntry> previousRows, IReadOnlyDictionary<int, List<int>> historicalSellMap, IReadOnlyDictionary<int, List<PriceSnapshotEntry>> priceHistoryMap) {
        if (row.LowestSell <= 0) {
            return null;
        }

        var candidate = _scoringService.CreatePriceCandidate(row.ItemId, row.HighestBuy, row.LowestSell);
        previousRows.TryGetValue(row.ItemId, out var previousRow);
        candidate = _scoringService.FinalizeCandidate(candidate, $"Watch {row.ItemId}", row.BuyQuantity, row.SellQuantity, SourceName);
        candidate.AcquisitionCostCopper = row.LowestSell;
        candidate.MarketDepth = Math.Min(Math.Max(1, row.BuyQuantity), Math.Max(1, row.SellQuantity));
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(row.BuyQuantity, row.SellQuantity);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.BuyDeltaCopper = previousRow == null ? 0 : row.HighestBuy - previousRow.HighestBuy;
        candidate.SellDeltaCopper = previousRow == null ? 0 : row.LowestSell - previousRow.LowestSell;
        candidate.PreviousSeenUtc = previousRow?.RecordedAtUtc;
        candidate.OpportunityMode = OpportunityMode.Investment;
        candidate.StrategyTag = AdvisorStrategyTag.Seasonal;
        candidate.InvestmentHorizonDays = 21;
        ApplyHistoricalValuation(candidate, historicalSellMap, row.LowestSell);
        ApplyPriceHistory(candidate, priceHistoryMap, row);

        if (candidate.FairValueWeightedCopper <= 0) {
            return null;
        }

        var fairNet = _scoringService.CalculateNetResaleValue(candidate.FairValueWeightedCopper);
        candidate.EstimatedProfit = Math.Max(0, fairNet - row.LowestSell);
        candidate.SpreadPercent = row.LowestSell <= 0
            ? 0m
            : Math.Round((candidate.EstimatedProfit / (decimal)row.LowestSell) * 100m, 1);
        candidate.DiscountPercent = candidate.FairValueWeightedCopper <= 0
            ? 0m
            : Math.Round(((candidate.FairValueWeightedCopper - row.LowestSell) / (decimal)candidate.FairValueWeightedCopper) * 100m, 1);
        candidate.ValueScore = Math.Round(candidate.EstimatedProfit * (candidate.ConfidenceScore / 100m), 2);
        candidate.FastFlipScore = candidate.ValueScore;
        candidate.Score = candidate.ValueScore;
        ApplyAdvisorMetrics(candidate, 0.35m);
        return candidate;
    }

    private async Task<List<CommercePriceDto>> FetchAllPricePagesAsync(OpportunityMode opportunityMode, IProgress<ScanProgressUpdate> progress, CancellationToken cancellationToken) {
        var firstPageResponse = await _httpClient.GetAsync($"commerce/prices?page=0&page_size={PageSize}", cancellationToken).ConfigureAwait(false);
        firstPageResponse.EnsureSuccessStatusCode();

        var results = await DeserializeResponseAsync<List<CommercePriceDto>>(firstPageResponse).ConfigureAwait(false);
        var pageTotal = ReadPageTotal(firstPageResponse.Headers);

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Fetched market price page 1/{pageTotal}..."
        });

        for (var pageStart = 1; pageStart < pageTotal; pageStart += PageBatchSize) {
            cancellationToken.ThrowIfCancellationRequested();

            var pageEndExclusive = Math.Min(pageStart + PageBatchSize, pageTotal);
            var batchTasks = new List<Task<List<CommercePriceDto>>>();

            for (var page = pageStart; page < pageEndExclusive; page++) {
                batchTasks.Add(FetchPricePageAsync(page, cancellationToken));
            }

            var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);

            foreach (var pageRows in batchResults) {
                results.AddRange(pageRows);
            }

            progress?.Report(new ScanProgressUpdate() {
                StatusMessage = $"Fetched market price pages {pageEndExclusive}/{pageTotal} for {GetModeLabel(opportunityMode)}..."
            });
        }

        return results;
    }

    private static MarketScanResult CreatePartialResult(IReadOnlyList<FlipCandidate> candidates, DateTimeOffset generatedAtUtc, OpportunityMode opportunityMode, ScanExecutionMode scanMode, int universeCandidateCount) {
        return new MarketScanResult() {
            Candidates = candidates.ToList(),
            TotalPriceRows = 0,
            SavedSnapshotCount = 0,
            SnapshotRootPath = "history in progress",
            SourceName = BuildSourceName(opportunityMode, scanMode),
            GeneratedAtUtc = generatedAtUtc,
            OpportunityMode = opportunityMode,
            UniverseCandidateCount = universeCandidateCount,
            FilteredCandidateCount = universeCandidateCount
        };
    }

    private static IReadOnlyDictionary<int, List<int>> BuildHistoricalSellMap(IReadOnlyList<PriceHistoryLoadResult> snapshots) {
        var historyByItem = new Dictionary<int, List<int>>();

        foreach (var snapshot in snapshots.Skip(1)) {
            foreach (var row in snapshot.Rows.Values) {
                if (row.LowestSell <= 0) {
                    continue;
                }

                if (!historyByItem.TryGetValue(row.ItemId, out var samples)) {
                    samples = new List<int>();
                    historyByItem[row.ItemId] = samples;
                }

                samples.Add(row.LowestSell);
            }
        }

        return historyByItem;
    }

    private static IReadOnlyDictionary<int, List<PriceSnapshotEntry>> BuildPriceHistoryMap(IReadOnlyList<PriceHistoryLoadResult> snapshots) {
        var historyByItem = new Dictionary<int, List<PriceSnapshotEntry>>();

        foreach (var snapshot in snapshots.Reverse()) {
            foreach (var row in snapshot.Rows.Values) {
                if (row.LowestSell <= 0 && row.HighestBuy <= 0) {
                    continue;
                }

                if (!historyByItem.TryGetValue(row.ItemId, out var samples)) {
                    samples = new List<PriceSnapshotEntry>();
                    historyByItem[row.ItemId] = samples;
                }

                samples.Add(new PriceSnapshotEntry() {
                    ItemId = row.ItemId,
                    HighestBuy = row.HighestBuy,
                    LowestSell = row.LowestSell,
                    BuyQuantity = row.BuyQuantity,
                    SellQuantity = row.SellQuantity,
                    RecordedAtUtc = row.RecordedAtUtc
                });
            }
        }

        return historyByItem;
    }

    private static int CalculateMedian(IReadOnlyList<int> values) {
        if (values == null || values.Count == 0) {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var middle = ordered.Count / 2;

        if (ordered.Count % 2 == 0) {
            return (ordered[middle - 1] + ordered[middle]) / 2;
        }

        return ordered[middle];
    }

    private void ApplyHistoricalValuation(FlipCandidate candidate, IReadOnlyDictionary<int, List<int>> historicalSellMap, int fallbackSellPrice) {
        if (candidate == null) {
            return;
        }

        if (!historicalSellMap.TryGetValue(candidate.ItemId, out var historicalSells) || historicalSells.Count == 0) {
            candidate.FairValueRecentMedianCopper = fallbackSellPrice;
            candidate.FairValueWeightedCopper = fallbackSellPrice;
            candidate.FairValueCopper = candidate.FairValueCopper > 0 ? candidate.FairValueCopper : fallbackSellPrice;
            candidate.VolatilityPercent = 0m;
            candidate.SoldThroughConfidence = 15m;
            return;
        }

        var weightedAverage = CalculateWeightedAverage(historicalSells);
        candidate.FairValueRecentMedianCopper = CalculateMedian(historicalSells);
        candidate.FairValueWeightedCopper = weightedAverage;
        candidate.FairValueCopper = candidate.FairValueCopper > 0 ? candidate.FairValueCopper : weightedAverage;
        candidate.VolatilityPercent = CalculateVolatilityPercent(historicalSells);
        candidate.SoldThroughConfidence = Math.Round(Clamp((historicalSells.Count / 8m) * (1m - Clamp(candidate.VolatilityPercent / 75m, 0m, 0.70m)), 0.10m, 1.00m) * 100m, 1);
        candidate.ConfidenceScore = Math.Max(candidate.ConfidenceScore, candidate.SoldThroughConfidence);

        if (candidate.OpportunityMode != OpportunityMode.Value && candidate.FairValueWeightedCopper > 0) {
            var netFairValue = _scoringService.CalculateNetResaleValue(candidate.FairValueWeightedCopper);
            candidate.ValueScore = Math.Round(Math.Max(0, netFairValue - candidate.AcquisitionCostCopper) * (candidate.ConfidenceScore / 100m), 2);
        }

        ApplyMarketValueMetrics(candidate);
    }

    private static void ApplyPriceHistory(FlipCandidate candidate, IReadOnlyDictionary<int, List<PriceSnapshotEntry>> priceHistoryMap, PriceSnapshotEntry currentRow) {
        if (candidate == null) {
            return;
        }

        var points = priceHistoryMap != null && priceHistoryMap.TryGetValue(candidate.ItemId, out var samples)
            ? samples.ToList()
            : new List<PriceSnapshotEntry>();

        if (currentRow != null && !points.Any(point => point.RecordedAtUtc == currentRow.RecordedAtUtc)) {
            points.Add(new PriceSnapshotEntry() {
                ItemId = currentRow.ItemId,
                HighestBuy = currentRow.HighestBuy,
                LowestSell = currentRow.LowestSell,
                BuyQuantity = currentRow.BuyQuantity,
                SellQuantity = currentRow.SellQuantity,
                RecordedAtUtc = currentRow.RecordedAtUtc
            });
        }

        var orderedPoints = points
            .Where(point => point.LowestSell > 0 || point.HighestBuy > 0)
            .OrderBy(point => point.RecordedAtUtc)
            .ToList();
        candidate.PriceHistory = orderedPoints
            .Skip(Math.Max(0, orderedPoints.Count - 24))
            .ToList();
        candidate.HistoricalSampleCount = Math.Max(candidate.HistoricalSampleCount, candidate.PriceHistory.Count);
    }

    private void ApplyAdvisorMetrics(FlipCandidate candidate, decimal velocityMultiplier = 1.0m) {
        if (candidate == null) {
            return;
        }

        var confidenceFactor = Clamp(candidate.ConfidenceScore / 100m, 0.15m, 1.00m);
        var soldThroughFactor = Clamp(candidate.SoldThroughConfidence / 100m, 0.10m, 1.00m);
        var marketDepthFactor = Clamp(candidate.MarketDepth / 3000m, 0.15m, 2.50m);
        var turnoverFactor = Clamp(candidate.TurnoverScore / 1.10m, 0.20m, 1.40m);
        var fillsPerDay = Math.Round(Math.Max(0.10m, marketDepthFactor * turnoverFactor * confidenceFactor * soldThroughFactor * velocityMultiplier), 2);
        var exitQuality = Math.Round(Clamp(
            ((candidate.LiquidityScore * 0.24m) +
            (candidate.StabilityScore * 0.22m) +
            (candidate.VolumeScore * 0.18m) +
            (confidenceFactor * 0.20m) +
            (soldThroughFactor * 0.16m)) * 100m,
            10m,
            100m), 1);

        candidate.ExpectedFillsPerDay = candidate.OpportunityMode == OpportunityMode.Cooldown
            ? Math.Max(0.50m, fillsPerDay)
            : fillsPerDay;
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.EstimatedProfit * candidate.ExpectedFillsPerDay);
        candidate.ExitQualityScore = exitQuality;
        candidate.CapitalEfficiencyScore = candidate.AcquisitionCostCopper <= 0
            ? 100m
            : Math.Round(Clamp((candidate.ExpectedGoldPerDayCopper / (decimal)Math.Max(1, candidate.AcquisitionCostCopper)) * 100m, 5m, 100m), 1);
        candidate.AdvisorScore = Math.Round(
            (candidate.ExpectedGoldPerDayCopper / 100m) *
            Clamp(candidate.ExitQualityScore / 100m, 0.15m, 1.00m) *
            Clamp(candidate.CapitalEfficiencyScore / 100m, 0.20m, 1.00m),
            2);
    }

    private static void ApplyMarketValueMetrics(FlipCandidate candidate) {
        if (candidate == null) {
            return;
        }

        var referenceCopper = candidate.FairValueWeightedCopper > 0
            ? candidate.FairValueWeightedCopper
            : (candidate.FairValueCopper > 0 ? candidate.FairValueCopper : candidate.LowestSell);
        var currentValueCopper = candidate.OpportunityMode == OpportunityMode.Investment
            ? Math.Max(0, candidate.LowestSell)
            : Math.Max(0, candidate.AcquisitionCostCopper);

        candidate.MarketValueReferenceCopper = referenceCopper;
        var value = MarketValueHelper.Calculate(currentValueCopper, referenceCopper);
        candidate.MarketValuePercent = value.Percent;
        candidate.MarketValueBand = value.Band;
        candidate.MarketValueLabel = value.Label;
    }

    private static FlipQueryOptions CloneOptions(FlipQueryOptions options) {
        return new FlipQueryOptions() {
            TopCount = options.TopCount,
            MinimumProfitCopper = options.MinimumProfitCopper,
            SortMode = options.SortMode,
            PracticalOnly = options.PracticalOnly,
            HistoryRetentionDays = options.HistoryRetentionDays,
            OpportunityMode = options.OpportunityMode,
            MaxAcquireCostCopper = options.MaxAcquireCostCopper,
            MinimumMarketDepth = options.MinimumMarketDepth,
            MinimumDiscountPercent = options.MinimumDiscountPercent,
            MinimumRoiPercent = options.MinimumRoiPercent,
            WatchlistOnly = options.WatchlistOnly,
            MaxOwnedQuantity = options.MaxOwnedQuantity,
            MaxOpenSellQuantity = options.MaxOpenSellQuantity,
            MaxVolatilityPercent = options.MaxVolatilityPercent,
            AutoFlipQuantity = options.AutoFlipQuantity,
            ActivePresetName = options.ActivePresetName,
            AlertRules = options.AlertRules
        };
    }

    private static bool IsKeywordMatch(string itemName, IEnumerable<string> keywords) {
        return !string.IsNullOrWhiteSpace(itemName) &&
               keywords.Any(keyword => itemName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string ResolveSeasonWindow(string itemName, DateTimeOffset timestamp) {
        if (IsKeywordMatch(itemName, new[] { "Super ", "Bauble", "Moto" })) {
            return IsBetween(timestamp, 3, 20, 4, 25) ? "Active seasonal window: Super Adventure Festival" : "Off-season watch: Super Adventure Festival";
        }

        if (IsKeywordMatch(itemName, new[] { "Jorbreaker", "Dragon Coffer", "Dragon Bash" })) {
            return IsBetween(timestamp, 6, 10, 7, 12) ? "Active seasonal window: Dragon Bash" : "Off-season watch: Dragon Bash";
        }

        if (IsKeywordMatch(itemName, new[] { "Wintersday", "Snowflake" })) {
            return IsBetween(timestamp, 12, 1, 1, 15) ? "Active seasonal window: Wintersday" : "Off-season watch: Wintersday";
        }

        if (IsKeywordMatch(itemName, new[] { "Lucky Envelope", "Red Lantern" })) {
            return IsBetween(timestamp, 1, 20, 3, 15) ? "Active seasonal window: Lunar New Year" : "Off-season watch: Lunar New Year";
        }

        if (IsKeywordMatch(itemName, new[] { "Candy Corn", "Trick-or-Treat", "Plastic " })) {
            return IsBetween(timestamp, 10, 1, 11, 15) ? "Active seasonal window: Halloween" : "Off-season watch: Halloween";
        }

        return "Rotation watch";
    }

    private static bool IsBetween(DateTimeOffset timestamp, int startMonth, int startDay, int endMonth, int endDay) {
        var start = new DateTimeOffset(timestamp.Year, startMonth, startDay, 0, 0, 0, timestamp.Offset);
        var endYear = endMonth < startMonth ? timestamp.Year + 1 : timestamp.Year;
        var end = new DateTimeOffset(endYear, endMonth, endDay, 23, 59, 59, timestamp.Offset);
        var probe = timestamp;

        if (endMonth < startMonth && timestamp.Month < startMonth) {
            probe = timestamp.AddYears(1);
        }

        return probe >= start && probe <= end;
    }

    private static int CalculateWeightedAverage(IReadOnlyList<int> values) {
        if (values == null || values.Count == 0) {
            return 0;
        }

        decimal totalWeight = 0m;
        decimal weightedSum = 0m;

        for (var index = 0; index < values.Count; index++) {
            var weight = values.Count - index;
            weightedSum += values[index] * weight;
            totalWeight += weight;
        }

        return totalWeight <= 0 ? values[0] : (int)Math.Round(weightedSum / totalWeight);
    }

    private static decimal CalculateVolatilityPercent(IReadOnlyList<int> values) {
        if (values == null || values.Count < 2) {
            return 0m;
        }

        var average = values.Average();

        if (average <= 0) {
            return 0m;
        }

        var variance = values.Select(value => Math.Pow(value - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);
        return Math.Round((decimal)(standardDeviation / average) * 100m, 1);
    }

    private static IEnumerable<FlipCandidate> ApplySort(IEnumerable<FlipCandidate> candidates, FlipSortMode sortMode) {
        switch (sortMode) {
            case FlipSortMode.EstimatedProfit:
                return candidates.OrderByDescending(candidate => candidate.EstimatedProfit).ThenByDescending(candidate => candidate.FastFlipScore);
            case FlipSortMode.SpreadPercent:
                return candidates.OrderByDescending(candidate => candidate.SpreadPercent).ThenByDescending(candidate => candidate.FastFlipScore);
            default:
                return candidates.OrderByDescending(candidate => candidate.AdvisorScore).ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper);
        }
    }

    private static bool PassesAcquireCap(FlipCandidate candidate, int maxAcquireCostCopper) {
        return maxAcquireCostCopper <= 0 || candidate.AcquisitionCostCopper <= maxAcquireCostCopper;
    }

    private static bool IsPracticalType(string itemType) {
        return !string.IsNullOrWhiteSpace(itemType) && PracticalTypes.Contains(itemType);
    }

    private static decimal CalculateVolumeScore(int marketDepth) {
        var depthScore = Math.Min(1m, (decimal)Math.Log10(Math.Max(10, marketDepth)) / 4.5m);
        return Math.Max(0.15m, Math.Round(depthScore, 2));
    }

    private static decimal CalculateDemandPressure(int buyDepth, int sellDepth) {
        var ratio = buyDepth / (decimal)Math.Max(1, sellDepth);
        return Math.Round(Clamp(ratio, 0.05m, 3.00m), 2);
    }

    private static decimal CalculateTurnoverScore(decimal demandPressure, int acquisitionCostCopper) {
        var pressureScore = Clamp(demandPressure / 1.15m, 0.20m, 1.35m);
        var affordabilityScore = acquisitionCostCopper <= 0
            ? 1m
            : Clamp(200000m / Math.Max(5000m, acquisitionCostCopper), 0.15m, 1.00m);

        return Math.Round(pressureScore * affordabilityScore, 2);
    }

    private static decimal CalculateFastFlipScore(FlipCandidate candidate) {
        return Math.Round(candidate.EstimatedProfit * candidate.VolumeScore * candidate.TurnoverScore * candidate.LiquidityScore * candidate.StabilityScore, 2);
    }

    private static decimal CalculateConfidenceScore(decimal liquidityScore, decimal stabilityScore, decimal volumeScore, decimal turnoverScore, decimal historyConfidence) {
        var turnoverNormalized = Clamp(turnoverScore / 1.35m, 0.15m, 1.00m);
        var blended =
            (liquidityScore * 0.24m) +
            (stabilityScore * 0.24m) +
            (volumeScore * 0.22m) +
            (turnoverNormalized * 0.20m) +
            (historyConfidence * 0.10m);
        return Math.Round(Clamp(blended, 0.10m, 1.00m) * 100m, 1);
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) {
        if (value < minimum) {
            return minimum;
        }

        if (value > maximum) {
            return maximum;
        }

        return value;
    }

    private static int ReadPageTotal(HttpResponseHeaders headers) {
        if (!headers.TryGetValues("X-Page-Total", out var values)) {
            return 1;
        }

        var rawValue = values.FirstOrDefault();
        return int.TryParse(rawValue, out var pageTotal)
            ? Math.Max(1, pageTotal)
            : 1;
    }

    private static async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response) where T : new() {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var parsed = JsonConvert.DeserializeObject<T>(content);
        return parsed ?? new T();
    }

    private async Task<List<CommercePriceDto>> FetchPricePageAsync(int page, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        var pageResponse = await _httpClient.GetAsync($"commerce/prices?page={page}&page_size={PageSize}", cancellationToken).ConfigureAwait(false);
        pageResponse.EnsureSuccessStatusCode();

        return await DeserializeResponseAsync<List<CommercePriceDto>>(pageResponse).ConfigureAwait(false);
    }

    private static IEnumerable<int[]> Chunk(int[] values, int chunkSize) {
        for (var i = 0; i < values.Length; i += chunkSize) {
            var length = Math.Min(chunkSize, values.Length - i);
            var chunk = new int[length];
            Array.Copy(values, i, chunk, 0, length);
            yield return chunk;
        }
    }

    private static string BuildSourceName(OpportunityMode opportunityMode, ScanExecutionMode scanMode) {
        var scanLabel = scanMode == ScanExecutionMode.Full ? "full" : "quick";

        switch (opportunityMode) {
            case OpportunityMode.Craft:
                return $"{scanLabel} craft scan";
            case OpportunityMode.Cooldown:
                return $"{scanLabel} cooldown scan";
            case OpportunityMode.Investment:
                return $"{scanLabel} investment scan";
            case OpportunityMode.Value:
                return $"{scanLabel} value scan";
            default:
                return $"{scanLabel} commerce scan";
        }
    }

    private static string GetModeLabel(OpportunityMode opportunityMode) {
        switch (opportunityMode) {
            case OpportunityMode.Craft:
                return "craft gains";
            case OpportunityMode.Cooldown:
                return "cooldown crafts";
            case OpportunityMode.Investment:
                return "investment watch";
            case OpportunityMode.Value:
                return "value deals";
            default:
                return "volume flips";
        }
    }

    private sealed class CommercePriceDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("buys")]
        public CommercePriceSideDto Buys { get; set; }

        [JsonProperty("sells")]
        public CommercePriceSideDto Sells { get; set; }
    }

    private sealed class CommercePriceSideDto {
        [JsonProperty("unit_price")]
        public int UnitPrice { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }

    private sealed class ItemDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("rarity")]
        public string Rarity { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }
}
