using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2FlipOverlay.Models;
using Gw2FlipOverlay.Services;
using Gw2FlipOverlay.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay;

[Export(typeof(Module))]
public sealed class FlipOverlayModule : Module {

    private static readonly Logger Logger = Logger.GetLogger<FlipOverlayModule>();

    private static readonly HashSet<string> PracticalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "CraftingMaterial",
        "Consumable",
        "UpgradeComponent",
        "Trophy",
        "Container"
    };

    private const int StaleBuyOrderDays = 3;

    private readonly ModuleSettings _settings = new ModuleSettings();
    private readonly FlipScoringService _scoringService = new FlipScoringService();
    private readonly PriceHistoryStore _historyStore = new PriceHistoryStore();
    private readonly LastScanCacheStore _lastScanCacheStore = new LastScanCacheStore();
    private readonly WatchlistStore _watchlistStore = new WatchlistStore();
    private readonly ScanPresetStore _presetStore = new ScanPresetStore();
    private readonly AccountSnapshotStore _accountSnapshotStore = new AccountSnapshotStore();
    private readonly CandidateInsightService _candidateInsightService = new CandidateInsightService();
    private readonly LedgerService _ledgerService = new LedgerService();
    private readonly AdvisorService _advisorService = new AdvisorService();
    private readonly AdvisorBriefingStore _advisorBriefingStore = new AdvisorBriefingStore();
    private readonly PortfolioService _portfolioService = new PortfolioService();
    private readonly PortfolioSnapshotStore _portfolioSnapshotStore = new PortfolioSnapshotStore();
    private readonly Dictionary<OpportunityMode, MarketScanResult> _scanResultsByMode = new Dictionary<OpportunityMode, MarketScanResult>();
    private readonly HashSet<int> _watchlistItemIds = new HashSet<int>();
    private readonly List<ScanPreset> _presets = new List<ScanPreset>();

    private CornerIcon _cornerIcon;
    private FlipOverlayWindow _overlayWindow;
    private IMarketDataProvider _liveProvider;
    private IMarketDataProvider _mockProvider;
    private IAccountDataProvider _liveAccountProvider;
    private IAccountDataProvider _mockAccountProvider;
    private CancellationTokenSource _refreshCts;
    private bool _isRefreshing;
    private AccountSnapshot _accountSnapshot = new AccountSnapshot();
    private AccountSnapshot _previousAccountSnapshot = new AccountSnapshot();
    private IReadOnlyList<TransactionLedgerEntry> _ledgerEntries = Array.Empty<TransactionLedgerEntry>();
    private OverlayViewMode _viewMode = OverlayViewMode.Market;
    private OverlayViewMode _lastScannerViewMode = OverlayViewMode.Market;
    private AdvisorBriefing _advisorBriefing = new AdvisorBriefing();
    private AdvisorBriefing _previousAdvisorBriefing;
    private PortfolioSnapshot _portfolioSnapshot = new PortfolioSnapshot();
    private IReadOnlyList<PortfolioSnapshot> _portfolioHistory = Array.Empty<PortfolioSnapshot>();
    private IReadOnlyList<MoneyActionRow> _autoFlipPlanRows = Array.Empty<MoneyActionRow>();
    private DateTime _autoFlipPlanGeneratedAt = DateTime.Now;
    private DateTime _nextAutoRefreshUtc = DateTime.MinValue;

    [ImportingConstructor]
    public FlipOverlayModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
    }

    protected override void DefineSettings(SettingCollection settings) {
        _settings.Define(settings);
    }

    protected override void Initialize() {
        _cornerIcon = new CornerIcon() {
            IconName = "Mystic Margin",
            Icon = AsyncTexture2D.FromAssetId(156022),
            Priority = "Trading Post".GetHashCode()
        };

        _overlayWindow = new FlipOverlayWindow();
        _overlayWindow.QuickScanRequested += async () => await RunScanAsync(ScanExecutionMode.Quick);
        _overlayWindow.FullScanRequested += async () => await RunScanAsync(ScanExecutionMode.Full);
        _overlayWindow.ScannerTabRequested += async () => await ShowScannerTabAsync();
        _overlayWindow.PortfolioTabRequested += async () => await ShowPortfolioTabAsync();
        _overlayWindow.SnipeTabRequested += async () => await ShowSnipeTabAsync();
        _overlayWindow.PlanTabRequested += async () => await ShowAutoPlanTabAsync(false);
        _overlayWindow.OrdersTabRequested += async () => await ShowOrdersTabAsync();
        _overlayWindow.CraftTabRequested += async () => await ShowCraftTabAsync();
        _overlayWindow.InventoryTabRequested += async () => await ShowInventoryTabAsync();
        _overlayWindow.SortModeCycleRequested += async () => await CycleSortModeAsync();
        _overlayWindow.ModeCycleRequested += async () => await CycleOpportunityModeAsync();
        _overlayWindow.RowCountCycleRequested += async () => await CycleRowCountAsync();
        _overlayWindow.CapCycleRequested += async () => await CycleCapitalCapAsync();
        _overlayWindow.DepthCycleRequested += async () => await CycleDepthFloorAsync();
        _overlayWindow.DiscountCycleRequested += async () => await CycleDiscountFloorAsync();
        _overlayWindow.RoiCycleRequested += async () => await CycleRoiFloorAsync();
        _overlayWindow.OwnedCycleRequested += async () => await CycleOwnedCapAsync();
        _overlayWindow.OpenSellCycleRequested += async () => await CycleOpenSellCapAsync();
        _overlayWindow.VolatilityCycleRequested += async () => await CycleVolatilityCapAsync();
        _overlayWindow.AutoFlipQuantityCycleRequested += async () => await CycleAutoFlipQuantityAsync();
        _overlayWindow.AutoFlipPlanRequested += async () => await ShowAutoPlanTabAsync(true);
        _overlayWindow.PresetCycleRequested += async () => await CyclePresetAsync();
        _overlayWindow.SavePresetRequested += async () => await SavePresetAsync();
        _overlayWindow.ViewCycleRequested += async () => await CycleViewModeAsync();
        _overlayWindow.MinimumProfitAdjusted += async delta => await AdjustMinimumProfitAsync(delta);
        _overlayWindow.PracticalOnlyToggled += async isEnabled => await SetPracticalOnlyAsync(isEnabled);
        _overlayWindow.WatchlistToggleRequested += async itemId => await ToggleWatchlistAsync(itemId);
        _overlayWindow.SetWatchlist(_watchlistItemIds);
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
        _overlayWindow.SetStatus(BuildAutoRefreshStatusMessage());

        _cornerIcon.Click += async delegate {
            _overlayWindow.Toggle();

            if (!_overlayWindow.IsVisible) {
                return;
            }

            if (_viewMode == OverlayViewMode.Advisor && _scanResultsByMode.Count > 0) {
                await RebuildAdvisorBriefingAsync(false);
                _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
                _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
                _overlayWindow.RenderAdvisor(_advisorBriefing, _advisorBriefing.GeneratedAtUtc.LocalDateTime, _accountSnapshot, _portfolioSnapshot);
                _overlayWindow.SetStatus(BuildAdvisorStatusMessage());
                return;
            }

            if (_viewMode == OverlayViewMode.Portfolio) {
                RebuildPortfolioSnapshot();
                _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
                _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
                _overlayWindow.RenderPortfolio(_portfolioSnapshot, _accountSnapshot);
                _overlayWindow.SetStatus(BuildPortfolioStatusMessage());
                return;
            }

            if (IsMoneyActionView(_viewMode)) {
                ApplyMoneyActionView();
                return;
            }

            if (_viewMode == OverlayViewMode.Snipe && _scanResultsByMode.Count > 0) {
                ApplySnipeView(BuildSnipeStatusMessage());
                return;
            }

            if (TryGetCurrentUniverse(out var cachedUniverse)) {
                ApplyCurrentView(cachedUniverse, BuildCachedStatusMessage(cachedUniverse));
                return;
            }

            await TryShowCachedResultsForCurrentModeAsync();
        };
    }

    protected override async Task LoadAsync() {
        _liveProvider = new Gw2CommerceDataProvider(_scoringService, _historyStore);
        _mockProvider = new MockMarketDataProvider(_scoringService);
        _liveAccountProvider = new Gw2AccountDataProvider();
        _mockAccountProvider = new MockAccountDataProvider();
        await LoadPresetsAsync();
        ApplyActivePresetFromSettings();
        await LoadWatchlistAsync();
        _accountSnapshot = await _accountSnapshotStore.TryLoadAsync(CancellationToken.None);
        _previousAdvisorBriefing = await _advisorBriefingStore.TryLoadAsync(CancellationToken.None);
        _portfolioHistory = await _portfolioSnapshotStore.LoadAsync(CancellationToken.None);
        await LoadCachedUniversesAsync();
        BuildLedgerEntries();
        RebuildPortfolioSnapshot();
        await RebuildAdvisorBriefingAsync(false);
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);

        if (_settings.OpenOnLoad.Value) {
            _overlayWindow.Show();
            await ShowCachedResultsAsync();
        }
    }

    protected override void Update(GameTime gameTime) {
        _overlayWindow?.SetGameFocusState(
            GameService.GameIntegration.Gw2Instance.Gw2HasFocus,
            _settings.HideWhenGw2Unfocused.Value);
        _overlayWindow?.UpdateInteraction();
        UpdateAutoRefresh();
    }

    protected override void Unload() {
        _refreshCts?.Cancel();
        (_liveProvider as IDisposable)?.Dispose();
        (_mockProvider as IDisposable)?.Dispose();
        (_liveAccountProvider as IDisposable)?.Dispose();
        (_mockAccountProvider as IDisposable)?.Dispose();
        _overlayWindow?.Dispose();
        _cornerIcon?.Dispose();
    }

    private async Task CycleSortModeAsync() {
        var current = ClampSortMode(_settings.SortMode.Value);
        var next = (FlipSortMode)((((int)current) + 1) % 5);
        _settings.SortMode.Value = (int)next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleOpportunityModeAsync() {
        var current = ClampOpportunityMode(_settings.OpportunityMode.Value);
        var next = current switch {
            OpportunityMode.Flip => OpportunityMode.Craft,
            OpportunityMode.Craft => OpportunityMode.Value,
            OpportunityMode.Value => OpportunityMode.Cooldown,
            OpportunityMode.Cooldown => OpportunityMode.Investment,
            _ => OpportunityMode.Flip
        };
        _settings.OpportunityMode.Value = (int)next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleRowCountAsync() {
        var next = _settings.TopCount.Value switch {
            15 => 30,
            30 => 60,
            60 => 100,
            _ => 15
        };

        _settings.TopCount.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleCapitalCapAsync() {
        var next = _settings.MaxAcquireCostCopper.Value switch {
            50000 => 100000,
            100000 => 200000,
            200000 => 500000,
            500000 => 1000000,
            1000000 => 0,
            _ => 50000
        };

        _settings.MaxAcquireCostCopper.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleDepthFloorAsync() {
        var next = _settings.MinimumMarketDepth.Value switch {
            500 => 1000,
            1000 => 2000,
            2000 => 3000,
            3000 => 5000,
            5000 => 10000,
            10000 => 15000,
            15000 => 0,
            _ => 1000
        };

        _settings.MinimumMarketDepth.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleDiscountFloorAsync() {
        var next = _settings.MinimumDiscountPercent.Value switch {
            0 => 5,
            5 => 10,
            10 => 15,
            15 => 20,
            20 => 25,
            _ => 0
        };

        _settings.MinimumDiscountPercent.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleRoiFloorAsync() {
        var next = _settings.MinimumRoiPercent.Value switch {
            0 => 4,
            4 => 5,
            5 => 8,
            8 => 10,
            10 => 15,
            15 => 25,
            _ => 0
        };

        _settings.MinimumRoiPercent.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleOwnedCapAsync() {
        var next = _settings.MaxOwnedQuantity.Value switch {
            25 => 50,
            50 => 100,
            100 => 250,
            250 => 500,
            500 => 0,
            _ => 25
        };

        _settings.MaxOwnedQuantity.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleOpenSellCapAsync() {
        var next = _settings.MaxOpenSellQuantity.Value switch {
            10 => 25,
            25 => 50,
            50 => 100,
            100 => 250,
            250 => 0,
            _ => 10
        };

        _settings.MaxOpenSellQuantity.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleVolatilityCapAsync() {
        var next = _settings.MaxVolatilityPercent.Value switch {
            10 => 15,
            15 => 20,
            20 => 25,
            25 => 35,
            35 => 0,
            _ => 10
        };

        _settings.MaxVolatilityPercent.Value = next;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleAutoFlipQuantityAsync() {
        var next = _settings.AutoFlipQuantity.Value switch {
            1 => 5,
            5 => 10,
            10 => 25,
            25 => 50,
            50 => 100,
            100 => 250,
            _ => 1
        };

        _settings.AutoFlipQuantity.Value = next;
        _autoFlipPlanRows = Array.Empty<MoneyActionRow>();
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.SetStatus($"Auto flip plan quantity set to {next:N0} per item.");
        await Task.CompletedTask;
    }

    private async Task CyclePresetAsync() {
        if (_presets.Count == 0) {
            return;
        }

        var currentIndex = _presets.FindIndex(preset => string.Equals(preset.Id, _settings.ActivePresetId.Value, StringComparison.OrdinalIgnoreCase));
        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % _presets.Count;
        var nextPreset = _presets[nextIndex];
        _settings.ActivePresetId.Value = nextPreset.Id;
        ApplyPreset(nextPreset);
        _overlayWindow.SetStatus($"Active preset: {nextPreset.Name}");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task SavePresetAsync() {
        var activePreset = GetActivePresetOrDefault();

        if (activePreset == null) {
            return;
        }

        CopySettingsIntoPreset(activePreset);
        await _presetStore.SaveAsync(_presets, CancellationToken.None);
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, activePreset.Name);
        _overlayWindow.SetStatus($"Saved current filters into preset: {activePreset.Name}");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task CycleViewModeAsync() {
        if (_viewMode == OverlayViewMode.Portfolio || _viewMode == OverlayViewMode.Snipe || IsMoneyActionView(_viewMode)) {
            _viewMode = _lastScannerViewMode;
        }

        _viewMode = _viewMode switch {
            OverlayViewMode.Market => OverlayViewMode.Watchlist,
            OverlayViewMode.Watchlist => OverlayViewMode.Ledger,
            OverlayViewMode.Ledger => OverlayViewMode.Advisor,
            OverlayViewMode.Advisor => OverlayViewMode.Snipe,
            OverlayViewMode.Snipe => OverlayViewMode.AutoPlan,
            OverlayViewMode.AutoPlan => OverlayViewMode.Orders,
            OverlayViewMode.Orders => OverlayViewMode.CraftBoard,
            OverlayViewMode.CraftBoard => OverlayViewMode.Inventory,
            OverlayViewMode.Inventory => OverlayViewMode.Market,
            _ => OverlayViewMode.Market
        };
        _lastScannerViewMode = _viewMode;

        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task ShowScannerTabAsync() {
        _viewMode = OverlayViewMode.Market;
        _lastScannerViewMode = _viewMode;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
    }

    private async Task ShowSnipeTabAsync() {
        if (_viewMode != OverlayViewMode.Portfolio && _viewMode != OverlayViewMode.Snipe && !IsMoneyActionView(_viewMode)) {
            _lastScannerViewMode = _viewMode;
        }

        _viewMode = OverlayViewMode.Snipe;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
    }

    private async Task ShowAutoPlanTabAsync(bool rebuildPlan) {
        if (_viewMode != OverlayViewMode.Portfolio && _viewMode != OverlayViewMode.Snipe && !IsMoneyActionView(_viewMode)) {
            _lastScannerViewMode = _viewMode;
        }

        if (rebuildPlan || _autoFlipPlanRows.Count == 0) {
            _autoFlipPlanRows = BuildAutoFlipPlanRows().ToList();
            _autoFlipPlanGeneratedAt = DateTime.Now;
        }

        _viewMode = OverlayViewMode.AutoPlan;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
    }

    private async Task ShowOrdersTabAsync() {
        await ShowMoneyActionTabAsync(OverlayViewMode.Orders);
    }

    private async Task ShowCraftTabAsync() {
        await ShowMoneyActionTabAsync(OverlayViewMode.CraftBoard);
    }

    private async Task ShowInventoryTabAsync() {
        await ShowMoneyActionTabAsync(OverlayViewMode.Inventory);
    }

    private async Task ShowMoneyActionTabAsync(OverlayViewMode viewMode) {
        if (_viewMode != OverlayViewMode.Portfolio && _viewMode != OverlayViewMode.Snipe && !IsMoneyActionView(_viewMode)) {
            _lastScannerViewMode = _viewMode;
        }

        _viewMode = viewMode;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
    }

    private async Task ShowPortfolioTabAsync() {
        if (_viewMode != OverlayViewMode.Portfolio) {
            _lastScannerViewMode = _viewMode;
        }

        _viewMode = OverlayViewMode.Portfolio;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
    }

    private async Task AdjustMinimumProfitAsync(int deltaCopper) {
        var updatedValue = Math.Max(1, _settings.MinimumProfitCopper.Value + deltaCopper);
        _settings.MinimumProfitCopper.Value = updatedValue;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task SetPracticalOnlyAsync(bool isEnabled) {
        _settings.PracticalOnly.Value = isEnabled;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        await Task.CompletedTask;
    }

    private async Task ToggleWatchlistAsync(int itemId) {
        if (itemId <= 0) {
            return;
        }

        var added = _watchlistItemIds.Add(itemId);

        if (!added) {
            _watchlistItemIds.Remove(itemId);
        }

        _overlayWindow.SetWatchlist(_watchlistItemIds);
        await _watchlistStore.SaveAsync(_watchlistItemIds, CancellationToken.None);
        foreach (var result in _scanResultsByMode.Values) {
            _candidateInsightService.EnrichCandidates(result, BuildQueryOptions(), _accountSnapshot, null, _previousAccountSnapshot, _watchlistItemIds);
        }
        RebuildPortfolioSnapshot();
        await RebuildAdvisorBriefingAsync();
        await ApplyCurrentViewIfAvailableOrPromptAsync();
        _overlayWindow.SetStatus(added
            ? $"Added item {itemId} to your watchlist."
            : $"Removed item {itemId} from your watchlist.");
    }

    private FlipQueryOptions BuildQueryOptions() {
        var activePreset = GetActivePresetOrDefault();
        return new FlipQueryOptions() {
            TopCount = _settings.TopCount.Value,
            MinimumProfitCopper = _settings.MinimumProfitCopper.Value,
            SortMode = ClampSortMode(_settings.SortMode.Value),
            PracticalOnly = _settings.PracticalOnly.Value,
            HistoryRetentionDays = _settings.HistoryRetentionDays.Value,
            OpportunityMode = ClampOpportunityMode(_settings.OpportunityMode.Value),
            MaxAcquireCostCopper = _settings.MaxAcquireCostCopper.Value,
            MinimumMarketDepth = _settings.MinimumMarketDepth.Value,
            MinimumDiscountPercent = _settings.MinimumDiscountPercent.Value,
            MinimumRoiPercent = _settings.MinimumRoiPercent.Value,
            WatchlistOnly = _settings.WatchlistOnly.Value,
            MaxOwnedQuantity = _settings.MaxOwnedQuantity.Value,
            MaxOpenSellQuantity = _settings.MaxOpenSellQuantity.Value,
            MaxVolatilityPercent = _settings.MaxVolatilityPercent.Value,
            AutoFlipQuantity = _settings.AutoFlipQuantity.Value,
            ActivePresetName = activePreset?.Name ?? string.Empty,
            AlertRules = activePreset?.AlertRules ?? (IReadOnlyList<AlertRule>) Array.Empty<AlertRule>()
        };
    }

    private static FlipQueryOptions BuildUniverseQueryOptions(FlipQueryOptions viewOptions) {
        return new FlipQueryOptions() {
            TopCount = 800,
            MinimumProfitCopper = 100,
            SortMode = FlipSortMode.Score,
            PracticalOnly = false,
            HistoryRetentionDays = viewOptions.HistoryRetentionDays,
            OpportunityMode = viewOptions.OpportunityMode,
            MaxAcquireCostCopper = 0,
            MinimumMarketDepth = 250,
            MinimumDiscountPercent = 0,
            MinimumRoiPercent = 0,
            WatchlistOnly = false,
            MaxOwnedQuantity = 0,
            MaxOpenSellQuantity = 0,
            MaxVolatilityPercent = 0,
            AutoFlipQuantity = viewOptions.AutoFlipQuantity,
            ActivePresetName = viewOptions.ActivePresetName,
            AlertRules = viewOptions.AlertRules
        };
    }

    private async Task LoadPresetsAsync() {
        _presets.Clear();
        _presets.AddRange(await _presetStore.LoadAsync(CancellationToken.None));
    }

    private void ApplyActivePresetFromSettings() {
        ApplyPreset(GetActivePresetOrDefault());
    }

    private ScanPreset GetActivePresetOrDefault() {
        if (_presets.Count == 0) {
            return null;
        }

        var activePreset = _presets.FirstOrDefault(preset => string.Equals(preset.Id, _settings.ActivePresetId.Value, StringComparison.OrdinalIgnoreCase));

        if (activePreset != null) {
            return activePreset;
        }

        _settings.ActivePresetId.Value = _presets[0].Id;
        return _presets[0];
    }

    private void ApplyPreset(ScanPreset preset) {
        if (preset == null) {
            return;
        }

        _settings.TopCount.Value = preset.TopCount;
        _settings.MinimumProfitCopper.Value = preset.MinimumProfitCopper;
        _settings.SortMode.Value = (int)preset.SortMode;
        _settings.PracticalOnly.Value = preset.PracticalOnly;
        _settings.OpportunityMode.Value = (int)preset.OpportunityMode;
        _settings.MaxAcquireCostCopper.Value = preset.MaxAcquireCostCopper;
        _settings.MinimumMarketDepth.Value = preset.MinimumMarketDepth;
        _settings.MinimumDiscountPercent.Value = preset.MinimumDiscountPercent;
        _settings.MinimumRoiPercent.Value = preset.MinimumRoiPercent;
        _settings.WatchlistOnly.Value = preset.WatchlistOnly;
        _settings.MaxOwnedQuantity.Value = preset.MaxOwnedQuantity;
        _settings.MaxOpenSellQuantity.Value = preset.MaxOpenSellQuantity;
        _settings.MaxVolatilityPercent.Value = preset.MaxVolatilityPercent;
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, preset.Name);
    }

    private void CopySettingsIntoPreset(ScanPreset preset) {
        if (preset == null) {
            return;
        }

        preset.TopCount = _settings.TopCount.Value;
        preset.MinimumProfitCopper = _settings.MinimumProfitCopper.Value;
        preset.SortMode = ClampSortMode(_settings.SortMode.Value);
        preset.PracticalOnly = _settings.PracticalOnly.Value;
        preset.OpportunityMode = ClampOpportunityMode(_settings.OpportunityMode.Value);
        preset.MaxAcquireCostCopper = _settings.MaxAcquireCostCopper.Value;
        preset.MinimumMarketDepth = _settings.MinimumMarketDepth.Value;
        preset.MinimumDiscountPercent = _settings.MinimumDiscountPercent.Value;
        preset.MinimumRoiPercent = _settings.MinimumRoiPercent.Value;
        preset.WatchlistOnly = _settings.WatchlistOnly.Value;
        preset.MaxOwnedQuantity = _settings.MaxOwnedQuantity.Value;
        preset.MaxOpenSellQuantity = _settings.MaxOpenSellQuantity.Value;
        preset.MaxVolatilityPercent = _settings.MaxVolatilityPercent.Value;
    }

    private async Task ApplyCurrentViewIfAvailableOrPromptAsync() {
        if (_viewMode == OverlayViewMode.Portfolio) {
            RebuildPortfolioSnapshot();
            _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
            _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
            _overlayWindow.RenderPortfolio(_portfolioSnapshot, _accountSnapshot);
            _overlayWindow.SetStatus(BuildPortfolioStatusMessage());
            return;
        }

        if (_viewMode == OverlayViewMode.Snipe) {
            ApplySnipeView();
            return;
        }

        if (IsMoneyActionView(_viewMode)) {
            ApplyMoneyActionView();
            return;
        }

        _lastScannerViewMode = _viewMode;

        if (_viewMode == OverlayViewMode.Advisor) {
            await RebuildAdvisorBriefingAsync();
            _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
            _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
            _overlayWindow.RenderAdvisor(_advisorBriefing, _advisorBriefing.GeneratedAtUtc.LocalDateTime, _accountSnapshot, _portfolioSnapshot);
            _overlayWindow.SetStatus(BuildAdvisorStatusMessage());
            return;
        }

        if (_viewMode == OverlayViewMode.Ledger) {
            BuildLedgerEntries();
            _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
            _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
            _overlayWindow.RenderLedgerRows(_ledgerEntries, _accountSnapshot.CapturedAtUtc.LocalDateTime, _accountSnapshot);
            _overlayWindow.SetStatus(BuildLedgerStatusMessage());
            return;
        }

        if (TryGetCurrentUniverse(out var currentUniverse)) {
            ApplyCurrentView(currentUniverse);
            return;
        }

        await TryShowCachedResultsForCurrentModeAsync();
    }

    private bool TryGetCurrentUniverse(out MarketScanResult currentUniverse) {
        var currentMode = ClampOpportunityMode(_settings.OpportunityMode.Value);
        return _scanResultsByMode.TryGetValue(currentMode, out currentUniverse);
    }

    private void ApplyCurrentView(MarketScanResult universeResult, string statusOverride = null) {
        var viewOptions = BuildQueryOptions();

        if (_viewMode == OverlayViewMode.Portfolio) {
            _overlayWindow.SetQueryState(viewOptions, _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
            _overlayWindow.SetWatchlist(_watchlistItemIds);
            _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
            _overlayWindow.RenderPortfolio(_portfolioSnapshot, _accountSnapshot);
            _overlayWindow.SetStatus(statusOverride ?? BuildPortfolioStatusMessage());
            return;
        }

        if (_viewMode == OverlayViewMode.Snipe) {
            ApplySnipeView(statusOverride);
            return;
        }

        if (IsMoneyActionView(_viewMode)) {
            ApplyMoneyActionView(statusOverride);
            return;
        }

        _lastScannerViewMode = _viewMode;

        if (_viewMode == OverlayViewMode.Advisor) {
            _overlayWindow.SetQueryState(viewOptions, _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
            _overlayWindow.SetWatchlist(_watchlistItemIds);
            _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
            _overlayWindow.RenderAdvisor(_advisorBriefing, _advisorBriefing.GeneratedAtUtc.LocalDateTime, _accountSnapshot, _portfolioSnapshot);
            _overlayWindow.SetStatus(statusOverride ?? BuildAdvisorStatusMessage());
            return;
        }

        if (_viewMode != OverlayViewMode.Ledger && universeResult.OpportunityMode != viewOptions.OpportunityMode) {
            return;
        }

        _overlayWindow.SetQueryState(viewOptions, _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.SetWatchlist(_watchlistItemIds);
        _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);

        if (_viewMode == OverlayViewMode.Ledger) {
            BuildLedgerEntries();
            _overlayWindow.RenderLedgerRows(_ledgerEntries, _accountSnapshot.CapturedAtUtc.LocalDateTime, _accountSnapshot);
            _overlayWindow.SetStatus(statusOverride ?? BuildLedgerStatusMessage());
            return;
        }

        var filteredCandidates = universeResult.Candidates
            .Where(candidate => PassesViewFilters(candidate, viewOptions, _watchlistItemIds, _viewMode))
            .ToList();

        var visibleCandidates = (_viewMode == OverlayViewMode.Watchlist
                ? filteredCandidates.OrderByDescending(candidate => candidate.AlertScore).ThenByDescending(candidate => Math.Abs(candidate.SellDeltaCopper))
                : ApplySort(filteredCandidates, viewOptions.SortMode))
            .Take(viewOptions.TopCount)
            .ToList();

        var viewResult = new MarketScanResult() {
            Candidates = visibleCandidates,
            TotalPriceRows = universeResult.TotalPriceRows,
            SavedSnapshotCount = universeResult.SavedSnapshotCount,
            SnapshotRootPath = universeResult.SnapshotRootPath,
            SourceName = universeResult.SourceName,
            GeneratedAtUtc = universeResult.GeneratedAtUtc,
            OpportunityMode = universeResult.OpportunityMode,
            UniverseCandidateCount = universeResult.Candidates.Count,
            FilteredCandidateCount = filteredCandidates.Count,
            ActivePresetName = universeResult.ActivePresetName
        };

        _overlayWindow.RenderRows(viewResult, universeResult.GeneratedAtUtc.LocalDateTime, _viewMode, _accountSnapshot);
        _overlayWindow.SetStatus(statusOverride ?? BuildStatusMessage(viewResult, _viewMode, _accountSnapshot));
    }

    private void ApplySnipeView(string statusOverride = null) {
        var viewOptions = BuildQueryOptions();
        var snipeCandidates = BuildSnipeCandidates(viewOptions).ToList();
        var latestGeneratedAt = _scanResultsByMode.Values
            .Select(result => result.GeneratedAtUtc)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max();
        var universeCount = _scanResultsByMode.Values.Sum(result => result.Candidates?.Count ?? 0);
        var viewResult = new MarketScanResult() {
            Candidates = snipeCandidates.Take(viewOptions.TopCount).ToList(),
            TotalPriceRows = _scanResultsByMode.Values.Sum(result => result.TotalPriceRows),
            SavedSnapshotCount = _scanResultsByMode.Values.Select(result => result.SavedSnapshotCount).DefaultIfEmpty(0).Max(),
            SnapshotRootPath = _scanResultsByMode.Values.Select(result => result.SnapshotRootPath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty,
            SourceName = "snipe board",
            GeneratedAtUtc = latestGeneratedAt,
            OpportunityMode = viewOptions.OpportunityMode,
            UniverseCandidateCount = universeCount,
            FilteredCandidateCount = snipeCandidates.Count,
            ActivePresetName = viewOptions.ActivePresetName
        };

        _overlayWindow.SetQueryState(viewOptions, _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.SetWatchlist(_watchlistItemIds);
        _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
        _overlayWindow.RenderRows(viewResult, latestGeneratedAt.LocalDateTime, _viewMode, _accountSnapshot);
        _overlayWindow.SetStatus(statusOverride ?? BuildSnipeStatusMessage(snipeCandidates.Count, universeCount));
    }

    private void ApplyMoneyActionView(string statusOverride = null) {
        var rows = BuildMoneyActionRows(_viewMode).ToList();
        _overlayWindow.SetQueryState(BuildQueryOptions(), _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.SetWatchlist(_watchlistItemIds);
        _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
        _overlayWindow.RenderMoneyActions(rows, DateTime.Now, _viewMode, _accountSnapshot);
        _overlayWindow.SetStatus(statusOverride ?? BuildMoneyActionStatusMessage(_viewMode, rows));
    }

    private async Task RunScanAsync(ScanExecutionMode scanMode) {
        if (_overlayWindow == null || _isRefreshing) {
            return;
        }

        _isRefreshing = true;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();

        var viewOptions = BuildQueryOptions();
        var scanOptions = BuildUniverseQueryOptions(viewOptions);
        var requestedMode = viewOptions.OpportunityMode;
        var previousScanResult = _scanResultsByMode.TryGetValue(requestedMode, out var existingUniverse)
            ? existingUniverse
            : null;

        _overlayWindow.SetQueryState(viewOptions, _viewMode, GetActivePresetOrDefault()?.Name ?? "Preset");
        _overlayWindow.SetBusy(true);
        _overlayWindow.SetStatus(scanMode == ScanExecutionMode.Full
            ? $"Starting full scan for {GetModeLabel(requestedMode)}..."
            : $"Rebuilding cached scan for {GetModeLabel(requestedMode)}...");

        try {
            var provider = _settings.UseMockData.Value ? _mockProvider : _liveProvider;
            var progress = new Progress<ScanProgressUpdate>(update => {
                if (update == null) {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(update.StatusMessage)) {
                    _overlayWindow.SetStatus(update.StatusMessage);
                }

                if (update.PartialResult == null) {
                    return;
                }

                _scanResultsByMode[update.PartialResult.OpportunityMode] = update.PartialResult;

                if (BuildQueryOptions().OpportunityMode == update.PartialResult.OpportunityMode) {
                    ApplyCurrentView(update.PartialResult, update.StatusMessage);
                }
            });

            var scanResult = await Task.Run(
                async () => await provider.GetScanAsync(scanOptions, scanMode, _refreshCts.Token, progress),
                _refreshCts.Token);

            _previousAccountSnapshot = _accountSnapshot;
            _accountSnapshot = await RefreshAccountSnapshotAsync(_refreshCts.Token);
            _candidateInsightService.EnrichCandidates(scanResult, viewOptions, _accountSnapshot, previousScanResult, _previousAccountSnapshot, _watchlistItemIds);
            _scanResultsByMode[scanResult.OpportunityMode] = scanResult;
            await _lastScanCacheStore.SaveAsync(scanResult.OpportunityMode, scanResult, CancellationToken.None);
            RebuildOtherScanInsights(scanResult.OpportunityMode);
            BuildLedgerEntries();
            RebuildPortfolioSnapshot();
            await SavePortfolioSnapshotAsync();
            await RebuildAdvisorBriefingAsync();

            if (BuildQueryOptions().OpportunityMode == scanResult.OpportunityMode) {
                ApplyCurrentView(scanResult, _accountSnapshot.StatusMessage);
            } else if (_viewMode == OverlayViewMode.Advisor) {
                _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
                _overlayWindow.RenderAdvisor(_advisorBriefing, _advisorBriefing.GeneratedAtUtc.LocalDateTime, _accountSnapshot, _portfolioSnapshot);
                _overlayWindow.SetStatus(BuildAdvisorStatusMessage());
            } else if (_viewMode == OverlayViewMode.Portfolio) {
                _overlayWindow.UpdatePortfolioSummary(_portfolioSnapshot);
                _overlayWindow.RenderPortfolio(_portfolioSnapshot, _accountSnapshot);
                _overlayWindow.SetStatus(BuildPortfolioStatusMessage());
            }
        } catch (Exception ex) when (!(ex is OperationCanceledException)) {
            Logger.Error(ex, "Manual scan failed.");

            if (_settings.UseMockData.Value) {
                _overlayWindow.SetStatus($"Mock scan failed. {ex.Message}");
            } else {
                _overlayWindow.SetStatus($"Scan failed. {ex.Message}");
            }
        } finally {
            _overlayWindow.SetBusy(false);
            _isRefreshing = false;
            ScheduleNextAutoRefresh();
        }
    }

    private async Task ShowCachedResultsAsync() {
        try {
            await TryShowCachedResultsForCurrentModeAsync();
        } catch (Exception ex) {
            Logger.Warn(ex, "Failed to load cached scan results.");
            _overlayWindow.SetStatus("No cached scan available. Press Full Scan for fresh prices.");
        }
    }

    private async Task TryShowCachedResultsForCurrentModeAsync() {
        var currentMode = ClampOpportunityMode(_settings.OpportunityMode.Value);

        if (_viewMode == OverlayViewMode.Snipe && _scanResultsByMode.Count > 0) {
            ApplySnipeView(BuildSnipeStatusMessage());
            return;
        }

        try {
            var cachedResult = await _lastScanCacheStore.TryLoadAsync(currentMode, CancellationToken.None);

            if (cachedResult == null || cachedResult.Candidates.Count == 0) {
                _overlayWindow.SetStatus("No cached results for this mode yet. Press Full Scan for fresh prices.");
                return;
            }

            _scanResultsByMode[currentMode] = cachedResult;
            _candidateInsightService.EnrichCandidates(cachedResult, BuildQueryOptions(), _accountSnapshot, null, _previousAccountSnapshot, _watchlistItemIds);
            BuildLedgerEntries();
            RebuildPortfolioSnapshot();
            await RebuildAdvisorBriefingAsync(false);
            ApplyCurrentView(cachedResult, BuildCachedStatusMessage(cachedResult));
        } catch (Exception ex) {
            Logger.Warn(ex, $"Failed to load cached {currentMode} scan results.");
            _overlayWindow.SetStatus("No cached results for this mode yet. Press Full Scan for fresh prices.");
        }
    }

    private async Task LoadWatchlistAsync() {
        try {
            var watchlistIds = await _watchlistStore.TryLoadAsync(CancellationToken.None);
            _watchlistItemIds.Clear();

            foreach (var itemId in watchlistIds) {
                _watchlistItemIds.Add(itemId);
            }

            _overlayWindow.SetWatchlist(_watchlistItemIds);
            foreach (var result in _scanResultsByMode.Values) {
                _candidateInsightService.EnrichCandidates(result, BuildQueryOptions(), _accountSnapshot, null, _previousAccountSnapshot, _watchlistItemIds);
            }
            RebuildPortfolioSnapshot();
            await RebuildAdvisorBriefingAsync();
        } catch (Exception ex) {
            Logger.Warn(ex, "Failed to load watchlist.");
        }
    }

    private async Task<AccountSnapshot> RefreshAccountSnapshotAsync(CancellationToken cancellationToken) {
        var provider = _settings.UseMockData.Value ? _mockAccountProvider : _liveAccountProvider;
        var snapshot = await provider.GetSnapshotAsync(_settings.ApiKey.Value, cancellationToken);

        if (snapshot.HasApiKey && !snapshot.IsAuthenticated && _accountSnapshot?.IsAuthenticated == true) {
            var failedStatus = snapshot.StatusMessage;
            snapshot = _accountSnapshot;
            snapshot.StatusMessage = $"{failedStatus} Showing last successful account sync from {snapshot.CapturedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}.";
            return snapshot;
        }

        await _accountSnapshotStore.SaveAsync(snapshot, CancellationToken.None);
        return snapshot;
    }

    private void UpdateAutoRefresh() {
        if (_overlayWindow == null || !_overlayWindow.IsVisible) {
            _nextAutoRefreshUtc = DateTime.MinValue;
            return;
        }

        if (_isRefreshing) {
            return;
        }

        if (_nextAutoRefreshUtc == DateTime.MinValue) {
            ScheduleNextAutoRefresh();
            return;
        }

        if (DateTime.UtcNow < _nextAutoRefreshUtc) {
            return;
        }

        ScheduleNextAutoRefresh();
        _ = RunScanAsync(ScanExecutionMode.Full);
    }

    private void ScheduleNextAutoRefresh() {
        _nextAutoRefreshUtc = DateTime.UtcNow.AddSeconds(Math.Max(30, _settings.RefreshIntervalSeconds.Value));
    }

    private void RebuildOtherScanInsights(OpportunityMode updatedMode) {
        var currentOptions = BuildQueryOptions();

        foreach (var pair in _scanResultsByMode.ToList()) {
            if (pair.Key == updatedMode) {
                continue;
            }

            _candidateInsightService.EnrichCandidates(pair.Value, currentOptions, _accountSnapshot, null, _previousAccountSnapshot, _watchlistItemIds);
        }
    }

    private async Task LoadCachedUniversesAsync() {
        foreach (OpportunityMode mode in Enum.GetValues(typeof(OpportunityMode))) {
            try {
                var cachedResult = await _lastScanCacheStore.TryLoadAsync(mode, CancellationToken.None);

                if (cachedResult == null || cachedResult.Candidates.Count == 0) {
                    continue;
                }

                _scanResultsByMode[mode] = cachedResult;
                _candidateInsightService.EnrichCandidates(cachedResult, BuildQueryOptions(), _accountSnapshot, null, _previousAccountSnapshot, _watchlistItemIds);
            } catch (Exception ex) {
                Logger.Debug(ex, $"Failed to preload cached scan for {mode}.");
            }
        }
    }

    private async Task RebuildAdvisorBriefingAsync(bool save = true) {
        _advisorBriefing = _advisorService.BuildBriefing(_scanResultsByMode, _accountSnapshot, _portfolioSnapshot.Summary, _previousAdvisorBriefing, DateTimeOffset.UtcNow);

        if (save) {
            await _advisorBriefingStore.SaveAsync(_advisorBriefing, CancellationToken.None);
            _previousAdvisorBriefing = _advisorBriefing;
        }
    }

    private void BuildLedgerEntries() {
        _ledgerEntries = _ledgerService.BuildEntries(_accountSnapshot, BuildCandidateMap());
    }

    private IReadOnlyDictionary<int, FlipCandidate> BuildCandidateMap() {
        var map = _scanResultsByMode.Values
            .SelectMany(result => result.Candidates)
            .GroupBy(candidate => candidate.ItemId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var pair in _accountSnapshot?.ItemNames ?? new Dictionary<int, string>()) {
            if (pair.Key <= 0 || string.IsNullOrWhiteSpace(pair.Value) || map.ContainsKey(pair.Key)) {
                continue;
            }

            map[pair.Key] = new FlipCandidate() {
                ItemId = pair.Key,
                ItemName = pair.Value
            };
        }

        return map;
    }

    private void RebuildPortfolioSnapshot() {
        _portfolioSnapshot = _portfolioService.BuildSnapshot(_accountSnapshot, BuildCandidateMap(), _ledgerEntries, _portfolioHistory, DateTimeOffset.UtcNow);
    }

    private async Task SavePortfolioSnapshotAsync() {
        await _portfolioSnapshotStore.SaveSnapshotAsync(_portfolioSnapshot, _settings.HistoryRetentionDays.Value, CancellationToken.None);
        _portfolioHistory = await _portfolioSnapshotStore.LoadAsync(CancellationToken.None);
        _portfolioSnapshot = _portfolioService.BuildSnapshot(_accountSnapshot, BuildCandidateMap(), _ledgerEntries, _portfolioHistory, _portfolioSnapshot.CapturedAtUtc);
    }

    private static bool PassesViewFilters(FlipCandidate candidate, FlipQueryOptions queryOptions, IReadOnlyCollection<int> watchlistIds, OverlayViewMode viewMode) {
        if (candidate == null) {
            return false;
        }

        if (candidate.EstimatedProfit < queryOptions.MinimumProfitCopper) {
            return false;
        }

        if (queryOptions.MaxAcquireCostCopper > 0 && candidate.AcquisitionCostCopper > queryOptions.MaxAcquireCostCopper) {
            return false;
        }

        if (queryOptions.MinimumMarketDepth > 0 && candidate.MarketDepth < queryOptions.MinimumMarketDepth) {
            return false;
        }

        if (queryOptions.OpportunityMode == OpportunityMode.Value &&
            queryOptions.MinimumDiscountPercent > 0 &&
            candidate.DiscountPercent < queryOptions.MinimumDiscountPercent) {
            return false;
        }

        if (queryOptions.MinimumRoiPercent > 0 && candidate.SpreadPercent < queryOptions.MinimumRoiPercent) {
            return false;
        }

        if (queryOptions.MaxOwnedQuantity > 0 && candidate.OwnedQuantity > queryOptions.MaxOwnedQuantity) {
            return false;
        }

        if (queryOptions.MaxOpenSellQuantity > 0 && candidate.CurrentSellOrderQuantity > queryOptions.MaxOpenSellQuantity) {
            return false;
        }

        if (queryOptions.MaxVolatilityPercent > 0 && candidate.VolatilityPercent > queryOptions.MaxVolatilityPercent) {
            return false;
        }

        if ((queryOptions.WatchlistOnly || viewMode == OverlayViewMode.Watchlist) &&
            !(watchlistIds?.Contains(candidate.ItemId) ?? false)) {
            return false;
        }

        if (!queryOptions.PracticalOnly) {
            return true;
        }

        return IsPracticalType(candidate.ItemType);
    }

    private IEnumerable<FlipCandidate> BuildSnipeCandidates(FlipQueryOptions queryOptions) {
        var candidates = _scanResultsByMode.Values
            .SelectMany(result => result.Candidates ?? Array.Empty<FlipCandidate>())
            .Where(candidate => candidate != null)
            .Where(candidate => PassesSnipeFilters(candidate, queryOptions))
            .GroupBy(candidate => candidate.ItemId)
            .Select(group => group
                .OrderByDescending(CalculateSnipeScore)
                .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
                .ThenByDescending(candidate => candidate.EstimatedProfit)
                .First())
            .OrderByDescending(CalculateSnipeScore)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .ThenByDescending(candidate => candidate.EstimatedProfit);

        return candidates;
    }

    private bool PassesSnipeFilters(FlipCandidate candidate, FlipQueryOptions queryOptions) {
        if (candidate.EstimatedProfit < Math.Max(queryOptions.MinimumProfitCopper, 100)) {
            return false;
        }

        if (candidate.AcquisitionCostCopper <= 0 || candidate.LowestSell <= 0) {
            return false;
        }

        if (queryOptions.MaxAcquireCostCopper > 0 && candidate.AcquisitionCostCopper > queryOptions.MaxAcquireCostCopper) {
            return false;
        }

        if (_accountSnapshot.AvailableCopper > 0 && candidate.AcquisitionCostCopper > _accountSnapshot.AvailableCopper) {
            return false;
        }

        if (candidate.MarketDepth < Math.Max(100, queryOptions.MinimumMarketDepth / 3)) {
            return false;
        }

        if (candidate.SpreadPercent < Math.Max(4, queryOptions.MinimumRoiPercent)) {
            return false;
        }

        if (queryOptions.PracticalOnly && !IsPracticalType(candidate.ItemType)) {
            return false;
        }

        if (queryOptions.MaxOwnedQuantity > 0 && candidate.OwnedQuantity >= queryOptions.MaxOwnedQuantity) {
            return false;
        }

        if (queryOptions.MaxOpenSellQuantity > 0 && candidate.CurrentSellOrderQuantity >= queryOptions.MaxOpenSellQuantity) {
            return false;
        }

        if (queryOptions.MaxVolatilityPercent > 0 && candidate.VolatilityPercent > Math.Max(queryOptions.MaxVolatilityPercent, 35)) {
            return false;
        }

        if (candidate.RecommendationState == RecommendationState.Skip) {
            return false;
        }

        var isUnderFairValue = candidate.MarketValuePercent > 0m && candidate.MarketValuePercent <= 105m;
        var hasDiscount = candidate.DiscountPercent >= Math.Max(4, queryOptions.MinimumDiscountPercent);
        var hasAlert = candidate.AlertScore > 0m;
        var hasFastFlipEdge = candidate.SpreadPercent >= 8m && candidate.EstimatedProfit >= Math.Max(250, queryOptions.MinimumProfitCopper);
        var hasTurnover = candidate.TurnoverScore >= 0.35m || candidate.ExpectedFillsPerDay >= 0.75m;
        return isUnderFairValue || hasDiscount || hasAlert || (hasFastFlipEdge && hasTurnover);
    }

    private static decimal CalculateSnipeScore(FlipCandidate candidate) {
        var cheapEdge = candidate.MarketValuePercent > 0m
            ? Math.Max(0m, 100m - candidate.MarketValuePercent) * 4m
            : 0m;
        var roiEdge = Math.Max(0m, candidate.SpreadPercent) * 1.5m;
        var liquidityEdge = Math.Min(40m, candidate.TurnoverScore * 30m + candidate.ExpectedFillsPerDay * 8m);
        var alertEdge = candidate.AlertScore * 20m;
        var exposurePenalty = candidate.CurrentSellOrderQuantity > 0 || candidate.OwnedQuantity > 0 ? 0.82m : 1m;
        return Math.Round((cheapEdge + roiEdge + liquidityEdge + alertEdge + candidate.ConfidenceScore / 3m) * exposurePenalty, 2);
    }

    private IEnumerable<MoneyActionRow> BuildMoneyActionRows(OverlayViewMode viewMode) {
        switch (viewMode) {
            case OverlayViewMode.AutoPlan:
                return _autoFlipPlanRows;
            case OverlayViewMode.Orders:
                return BuildOrderActionRows();
            case OverlayViewMode.CraftBoard:
                return BuildCraftActionRows();
            case OverlayViewMode.Inventory:
                return BuildInventoryActionRows();
            default:
                return Array.Empty<MoneyActionRow>();
        }
    }

    private IEnumerable<MoneyActionRow> BuildAutoFlipPlanRows() {
        var viewOptions = BuildQueryOptions();
        var quantity = Math.Max(1, viewOptions.AutoFlipQuantity);
        IEnumerable<FlipCandidate> candidates;

        if (_viewMode == OverlayViewMode.Snipe) {
            candidates = BuildSnipeCandidates(viewOptions);
        } else if (TryGetCurrentUniverse(out var currentUniverse)) {
            var filteredCandidates = currentUniverse.Candidates
                .Where(candidate => PassesViewFilters(candidate, viewOptions, _watchlistItemIds, _viewMode));
            candidates = _viewMode == OverlayViewMode.Watchlist
                ? filteredCandidates.OrderByDescending(candidate => candidate.AlertScore).ThenByDescending(candidate => Math.Abs(candidate.SellDeltaCopper))
                : ApplySort(filteredCandidates, viewOptions.SortMode);
        } else {
            candidates = _scanResultsByMode.Values
                .OrderByDescending(result => result.GeneratedAtUtc)
                .SelectMany(result => result.Candidates ?? Array.Empty<FlipCandidate>())
                .Where(candidate => PassesViewFilters(candidate, viewOptions, _watchlistItemIds, OverlayViewMode.Market));
            candidates = ApplySort(candidates, viewOptions.SortMode);
        }

        return candidates
            .Where(candidate => candidate != null)
            .Take(10)
            .Select(candidate => BuildAutoFlipPlanRow(candidate, quantity))
            .Where(row => row != null)
            .ToList();
    }

    private static MoneyActionRow BuildAutoFlipPlanRow(FlipCandidate candidate, int quantity) {
        var currentBid = candidate.HighestBuy > 0
            ? candidate.HighestBuy
            : candidate.AcquisitionCostCopper;

        if (currentBid <= 0 || candidate.NetResaleValue <= 0) {
            return null;
        }

        var targetBid = currentBid + 1;
        var bidRead = "top bid + 1c";

        if (candidate.LowestSell > 0 && targetBid >= candidate.LowestSell) {
            targetBid = currentBid;
            bidRead = "do not cross sell floor";
        }

        var unitProfit = candidate.NetResaleValue - targetBid;

        if (unitProfit <= 0) {
            return null;
        }

        var capital = (long)targetBid * quantity;
        var estimatedProfit = (long)unitProfit * quantity;
        var roi = targetBid <= 0 ? 0m : unitProfit / (decimal)targetBid * 100m;

        return new MoneyActionRow() {
            ItemId = candidate.ItemId,
            ItemName = candidate.ItemName,
            Lane = "Auto flip plan",
            Action = "Bid",
            Quantity = quantity,
            CapitalCopper = ClampCopper(capital),
            TargetCopper = targetBid,
            EdgeCopper = ClampCopper(estimatedProfit),
            ConfidenceScore = candidate.ConfidenceScore,
            Notes = $"{bidRead}; net sell {FormatCoin(candidate.NetResaleValue)}, ROI {roi:N1}%, depth {candidate.MarketDepth:N0}"
        };
    }

    private IEnumerable<MoneyActionRow> BuildOrderActionRows() {
        var candidates = BuildCandidateMap();
        var rows = new List<MoneyActionRow>();

        foreach (var pair in (_accountSnapshot?.OrderByItemId ?? new Dictionary<int, ItemOrderSnapshot>()).OrderBy(entry => entry.Key)) {
            var order = pair.Value;
            candidates.TryGetValue(pair.Key, out var candidate);
            var itemName = candidate?.ItemName ?? $"Item {pair.Key}";

            if (order.CurrentSellQuantity > 0 && order.CurrentSellUnitPrice > 0) {
                var currentFloor = candidate?.LowestSell ?? 0;
                var undercutCopper = currentFloor > 0 ? order.CurrentSellUnitPrice - currentFloor : 0;
                var netAtFloor = currentFloor > 0 ? (int)Math.Floor(currentFloor * 0.85m) * order.CurrentSellQuantity : 0;
                var netListed = (int)Math.Floor(order.CurrentSellUnitPrice * 0.85m) * order.CurrentSellQuantity;
                var action = undercutCopper > 0 ? "Reprice" : "Hold";
                rows.Add(new MoneyActionRow() {
                    ItemId = pair.Key,
                    ItemName = itemName,
                    Lane = "Sell order",
                    Action = action,
                    Quantity = order.CurrentSellQuantity,
                    CapitalCopper = netListed,
                    TargetCopper = currentFloor > 0 ? currentFloor : order.CurrentSellUnitPrice,
                    EdgeCopper = netAtFloor - netListed,
                    ConfidenceScore = candidate?.ConfidenceScore ?? 35m,
                    Notes = undercutCopper > 0
                        ? $"Your listing is {FormatCoin(undercutCopper)} above current floor; check whether relisting is worth the fee drag."
                        : "Listing is near the current floor; usually leave it alone unless turnover is dead."
                });
            }

            if (order.CurrentBuyQuantity > 0 && order.CurrentBuyUnitPrice > 0) {
                var topBuy = candidate?.HighestBuy ?? 0;
                var netResale = candidate?.NetResaleValue ?? 0;
                var spreadAtOrder = netResale - order.CurrentBuyUnitPrice;
                var outbid = topBuy > order.CurrentBuyUnitPrice;
                var buyAgeDays = order.CurrentBuyOldestCreatedUtc.HasValue
                    ? Math.Max(0, (DateTimeOffset.UtcNow - order.CurrentBuyOldestCreatedUtc.Value).TotalDays)
                    : 0;
                var stale = buyAgeDays >= StaleBuyOrderDays;
                var collapsed = netResale > 0 && spreadAtOrder < Math.Max(250, _settings.MinimumProfitCopper.Value);
                rows.Add(new MoneyActionRow() {
                    ItemId = pair.Key,
                    ItemName = itemName,
                    Lane = "Buy order",
                    Action = collapsed || stale ? "Cancel" : (outbid ? "Raise" : "Hold"),
                    Quantity = order.CurrentBuyQuantity,
                    CapitalCopper = (int)Math.Min(int.MaxValue, order.CurrentBuyTotalCopper > 0 ? order.CurrentBuyTotalCopper : order.CurrentBuyQuantity * order.CurrentBuyUnitPrice),
                    TargetCopper = topBuy > 0 ? topBuy : order.CurrentBuyUnitPrice,
                    EdgeCopper = spreadAtOrder * order.CurrentBuyQuantity,
                    ConfidenceScore = candidate?.ConfidenceScore ?? 35m,
                    Notes = collapsed
                        ? "Spread collapsed versus current sell floor; free this capital for better short-cycle flips."
                        : stale
                            ? $"Buy order is {buyAgeDays:N1} days old; cancel stale capital and rerun the daily plan."
                            : (outbid ? "Your buy is no longer top bid; raise only if the post-fee spread still clears your target." : "Buy is competitive and still has a workable spread.")
                });
            }
        }

        return rows
            .OrderByDescending(row => row.Action == "Cancel" || row.Action == "Reprice")
            .ThenByDescending(row => Math.Abs(row.EdgeCopper))
            .Take(60);
    }

    private IEnumerable<MoneyActionRow> BuildCraftActionRows() {
        var craftModes = new[] { OpportunityMode.Craft, OpportunityMode.Cooldown };
        return _scanResultsByMode
            .Where(pair => craftModes.Contains(pair.Key))
            .SelectMany(pair => pair.Value.Candidates ?? Array.Empty<FlipCandidate>())
            .Where(candidate => candidate.EstimatedProfit > 0)
            .OrderByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .ThenByDescending(candidate => candidate.EstimatedProfit)
            .Take(60)
            .Select(candidate => new MoneyActionRow() {
                ItemId = candidate.ItemId,
                ItemName = candidate.ItemName,
                Lane = candidate.OpportunityMode == OpportunityMode.Cooldown ? "Cooldown craft" : "Craft margin",
                Action = candidate.CraftFromOwnedCount > 0 ? "Craft" : "Buy mats",
                Quantity = Math.Max(1, candidate.CraftFromOwnedCount),
                CapitalCopper = candidate.AcquisitionCostCopper,
                TargetCopper = candidate.LowestSell,
                EdgeCopper = candidate.EstimatedProfit,
                ConfidenceScore = candidate.ConfidenceScore,
                Notes = candidate.CraftFromOwnedCount > 0
                    ? $"Owned materials cover at least {candidate.CraftFromOwnedCount:N0}; craft from stock before buying mats."
                    : $"Short-cycle craft margin with {candidate.SpreadPercent:N1}% ROI and {candidate.ExpectedFillsPerDay:N1} expected fills/day."
            });
    }

    private IEnumerable<MoneyActionRow> BuildInventoryActionRows() {
        var candidates = BuildCandidateMap();
        var rows = new List<MoneyActionRow>();

        foreach (var pair in (_accountSnapshot?.OwnedCounts ?? new Dictionary<int, int>()).Where(entry => entry.Value > 0)) {
            candidates.TryGetValue(pair.Key, out var candidate);
            var itemName = candidate?.ItemName ?? $"Item {pair.Key}";
            var unitNet = candidate?.LowestSell > 0 ? (int)Math.Floor(candidate.LowestSell * 0.85m) : 0;
            var fairNet = candidate?.FairValueWeightedCopper > 0 ? (int)Math.Floor(candidate.FairValueWeightedCopper * 0.85m) : unitNet;
            var edge = (unitNet - fairNet) * pair.Value;
            var action = candidate == null
                ? "Review"
                : candidate.MarketValuePercent >= 104m || candidate.RecommendationState == RecommendationState.SellExisting
                    ? "Sell"
                    : candidate.MarketValuePercent <= 92m
                        ? "Hold"
                        : "List";

            rows.Add(new MoneyActionRow() {
                ItemId = pair.Key,
                ItemName = itemName,
                Lane = "Filled stock",
                Action = action,
                Quantity = pair.Value,
                CapitalCopper = unitNet * pair.Value,
                TargetCopper = candidate?.LowestSell ?? 0,
                EdgeCopper = edge,
                ConfidenceScore = candidate?.ConfidenceScore ?? 25m,
                Notes = candidate == null
                    ? "No market scan candidate matched this item; manually review before listing."
                    : action == "Sell"
                        ? $"Current value is {candidate.MarketValuePercent:N0}% of fair; this is a short-cycle exit candidate."
                        : action == "Hold"
                            ? $"Current value is only {candidate.MarketValuePercent:N0}% of fair; avoid selling into a dip."
                            : $"Neutral value at {candidate.MarketValuePercent:N0}% of fair; list if you want liquidity."
            });
        }

        return rows
            .OrderByDescending(row => row.Action == "Sell")
            .ThenByDescending(row => row.CapitalCopper)
            .Take(60);
    }

    private static IEnumerable<FlipCandidate> ApplySort(IEnumerable<FlipCandidate> candidates, FlipSortMode sortMode) {
        switch (sortMode) {
            case FlipSortMode.EstimatedProfit:
                return candidates.OrderByDescending(candidate => candidate.EstimatedProfit).ThenByDescending(candidate => candidate.AdvisorScore);
            case FlipSortMode.SpreadPercent:
                return candidates.OrderByDescending(candidate => candidate.SpreadPercent).ThenByDescending(candidate => candidate.AdvisorScore);
            case FlipSortMode.MarketValueCheap:
                return candidates.OrderBy(candidate => candidate.MarketValuePercent).ThenByDescending(candidate => candidate.AdvisorScore);
            case FlipSortMode.MarketValueHot:
                return candidates.OrderByDescending(candidate => candidate.MarketValuePercent).ThenByDescending(candidate => candidate.AdvisorScore);
            default:
                return candidates.OrderByDescending(candidate => candidate.AdvisorScore).ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper);
        }
    }

    private static string BuildStatusMessage(MarketScanResult scanResult, OverlayViewMode viewMode, AccountSnapshot accountSnapshot) {
        var walletText = accountSnapshot == null || accountSnapshot.AvailableCopper <= 0
            ? "wallet unavailable"
            : $"wallet {FormatCoin(accountSnapshot.AvailableCopper)}";
        var viewLabel = viewMode == OverlayViewMode.Watchlist
            ? "watchlist"
            : (viewMode == OverlayViewMode.Snipe ? "snipe" : "market");
        return $"Loaded {scanResult.Candidates.Count} of {scanResult.FilteredCandidateCount} matching {viewLabel} rows from a {scanResult.UniverseCandidateCount}-item universe with {walletText}.";
    }

    private string BuildLedgerStatusMessage() {
        var realized = _ledgerEntries.Sum(entry => entry.RealizedProfitCopper);
        var unrealized = _ledgerEntries.Sum(entry => entry.UnrealizedProfitCopper);
        return $"Ledger shows {_ledgerEntries.Count} tracked items | Realized {FormatCoin(realized)} | Unrealized {FormatCoin(unrealized)} | Wallet {FormatCoin(_accountSnapshot.AvailableCopper)}.";
    }

    private string BuildAdvisorStatusMessage() {
        var digest = _advisorBriefing?.DigestLines?.FirstOrDefault() ?? "Advisor ready.";
        return $"{_advisorBriefing?.Summary ?? "Advisor briefing unavailable."} {digest}";
    }

    private string BuildPortfolioStatusMessage() {
        var summary = _portfolioSnapshot?.Summary ?? new PortfolioSummary();
        return $"{summary.StatusMessage} Net worth {FormatCoin(summary.NetWorthCopper)} | Wallet {FormatCoin(summary.WalletCopper)} | Buys {FormatCoin(summary.OutstandingBuyCopper)} | Sells {FormatCoin(summary.OutstandingSellNetCopper)} | Holdings {FormatCoin(summary.HoldingsValueCopper)}.";
    }

    private string BuildSnipeStatusMessage() {
        return BuildSnipeStatusMessage(BuildSnipeCandidates(BuildQueryOptions()).Count(), _scanResultsByMode.Values.Sum(result => result.Candidates?.Count ?? 0));
    }

    private string BuildSnipeStatusMessage(int snipeCount, int universeCount) {
        var walletText = _accountSnapshot == null || _accountSnapshot.AvailableCopper <= 0
            ? "wallet unavailable"
            : $"wallet {FormatCoin(_accountSnapshot.AvailableCopper)}";
        return $"Snipe board found {snipeCount} urgent deals from {universeCount} cached candidates with {walletText}. Run Full for fresh prices after changing modes.";
    }

    private string BuildAutoRefreshStatusMessage() {
        return $"Press Full for fresh prices, or Cached to reuse your last full scan. Auto-refresh runs every {Math.Max(30, _settings.RefreshIntervalSeconds.Value):N0}s while this overlay is open.";
    }

    private string BuildMoneyActionStatusMessage(OverlayViewMode viewMode, IReadOnlyList<MoneyActionRow> rows) {
        var totalEdge = rows?.Sum(row => row.EdgeCopper) ?? 0;
        var totalCapital = rows?.Sum(row => Math.Max(0, row.CapitalCopper)) ?? 0;
        if (viewMode == OverlayViewMode.AutoPlan) {
            return rows == null || rows.Count == 0
                ? "Buy plan is empty. Open Daily Scan, then press Plan Top 10."
                : $"Plan holds {rows.Count} staged buy orders from {_autoFlipPlanGeneratedAt:HH:mm:ss} | Capital {FormatCoin(totalCapital)} | Est profit {FormatCoin(totalEdge)} | Wallet {FormatCoin(_accountSnapshot.AvailableCopper)}.";
        }

        return $"{GetViewModeLabel(viewMode)} shows {rows?.Count ?? 0} daily actions | Edge {FormatCoin(totalEdge)} | Capital/value {FormatCoin(totalCapital)} | Wallet {FormatCoin(_accountSnapshot.AvailableCopper)}.";
    }

    private static bool IsMoneyActionView(OverlayViewMode viewMode) {
        return viewMode == OverlayViewMode.Orders ||
               viewMode == OverlayViewMode.CraftBoard ||
               viewMode == OverlayViewMode.Inventory ||
               viewMode == OverlayViewMode.AutoPlan;
    }

    private static string BuildCachedStatusMessage(MarketScanResult scanResult) {
        return $"Showing cached {scanResult.OpportunityMode} results from {scanResult.GeneratedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}. Press Full Scan for fresh prices.";
    }

    private static bool IsPracticalType(string itemType) {
        return !string.IsNullOrWhiteSpace(itemType) && PracticalTypes.Contains(itemType);
    }

    private static FlipSortMode ClampSortMode(int rawValue) {
        if (rawValue < 0 || rawValue > 4) {
            return FlipSortMode.Score;
        }

        return (FlipSortMode)rawValue;
    }

    private static OpportunityMode ClampOpportunityMode(int rawValue) {
        if (rawValue < 0 || rawValue > 4) {
            return OpportunityMode.Flip;
        }

        return (OpportunityMode)rawValue;
    }

    private static string GetModeLabel(OpportunityMode opportunityMode) {
        return opportunityMode switch {
            OpportunityMode.Craft => "craft gains",
            OpportunityMode.Cooldown => "cooldown crafts",
            OpportunityMode.Investment => "investment watch",
            OpportunityMode.Value => "value deals",
            _ => "volume flips"
        };
    }

    private static string GetViewModeLabel(OverlayViewMode viewMode) {
        return viewMode switch {
            OverlayViewMode.Orders => "Clean Orders",
            OverlayViewMode.CraftBoard => "Craft",
            OverlayViewMode.Inventory => "Sell Filled",
            OverlayViewMode.AutoPlan => "Buy Plan",
            OverlayViewMode.Snipe => "Snipe",
            OverlayViewMode.Portfolio => "Portfolio",
            OverlayViewMode.Advisor => "Advisor",
            OverlayViewMode.Ledger => "Ledger",
            OverlayViewMode.Watchlist => "Watchlist",
            _ => "Daily Scan"
        };
    }

    private static string FormatCoin(int copper) {
        var absValue = Math.Abs(copper);
        var gold = absValue / 10000;
        var silver = absValue / 100 % 100;
        var bronze = absValue % 100;
        var prefix = copper < 0 ? "-" : string.Empty;
        return $"{prefix}{gold}g {silver:D2}s {bronze:D2}c";
    }

    private static int ClampCopper(long copper) {
        if (copper > int.MaxValue) {
            return int.MaxValue;
        }

        if (copper < int.MinValue) {
            return int.MinValue;
        }

        return (int)copper;
    }
}
