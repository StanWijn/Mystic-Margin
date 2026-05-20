using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Gw2FlipOverlay.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Gw2FlipOverlay.UI;

public sealed class FlipOverlayWindow : IDisposable {

    private const string BuildVersion = "0.1.8-defaults-tuned";
    private const int MinPanelWidth = 520;
    private const int MinPanelHeight = 320;
    private const int MinimizedPanelWidth = 280;
    private const int MinimizedPanelHeight = 36;
    private const int ResizeHandleSize = 36;

    private readonly OverlayNativeWindow _panel;
    private readonly Panel _miniPanel;
    private readonly Panel _toolbarPanel;
    private readonly Panel _filterPanel;
    private readonly Panel _footerPanel;
    private readonly Label _miniTitleLabel;
    private readonly StandardButton _miniRestoreButton;
    private readonly Panel _listPanel;
    private readonly Scrollbar _listScrollbar;
    private readonly Panel _detailPanel;
    private readonly Scrollbar _detailScrollbar;
    private readonly Panel _resizeHandle;
    private readonly Label _subtitleLabel;
    private readonly Label _summaryLabel;
    private readonly StandardButton _scannerTabButton;
    private readonly StandardButton _portfolioTabButton;
    private readonly StandardButton _snipeTabButton;
    private readonly StandardButton _ordersTabButton;
    private readonly StandardButton _craftTabButton;
    private readonly StandardButton _inventoryTabButton;
    private readonly StandardButton _modeButton;
    private readonly StandardButton _viewButton;
    private readonly StandardButton _presetButton;
    private readonly StandardButton _savePresetButton;
    private readonly StandardButton _sortButton;
    private readonly StandardButton _rowsButton;
    private readonly StandardButton _capButton;
    private readonly StandardButton _depthButton;
    private readonly StandardButton _discountButton;
    private readonly StandardButton _roiButton;
    private readonly StandardButton _ownedButton;
    private readonly StandardButton _openSellButton;
    private readonly StandardButton _volatilityButton;
    private readonly StandardButton _autoQuantityButton;
    private readonly StandardButton _autoPlanButton;
    private readonly StandardButton _profitDownButton;
    private readonly StandardButton _profitUpButton;
    private readonly StandardButton _practicalButton;
    private readonly StandardButton _quickScanButton;
    private readonly StandardButton _fullScanButton;
    private readonly StandardButton _minimizeButton;
    private readonly StandardButton _closeButton;
    private readonly StandardButton _copyNameButton;
    private readonly StandardButton _copyWikiButton;
    private readonly StandardButton _openWikiButton;
    private readonly StandardButton _watchButton;
    private readonly Label _rankHeaderLabel;
    private readonly Label _itemHeaderLabel;
    private readonly Label _profitLabel;
    private readonly Label _statusLabel;
    private readonly Label _portfolioStripLabel;
    private readonly Label _updatedLabel;
    private readonly Label _costHeaderLabel;
    private readonly Label _sellHeaderLabel;
    private readonly Label _profitHeaderLabel;
    private readonly Label _roiHeaderLabel;
    private readonly Label _depthHeaderLabel;
    private readonly Label _pressHeaderLabel;
    private readonly Label _turnHeaderLabel;
    private readonly Label _scoreHeaderLabel;
    private readonly Label _detailTitleLabel;
    private readonly Label _detailSubtitleLabel;
    private readonly Label _detailHintLabel;
    private readonly Label _detailStatsLabel;
    private readonly Label _detailTrendLabel;
    private readonly Label _resizeGlyphLabel;
    private readonly List<Control> _dynamicControls = new List<Control>();
    private readonly List<Control> _detailGraphControls = new List<Control>();
    private readonly HashSet<int> _watchlistItemIds = new HashSet<int>();

    private MarketScanResult _lastRenderedResult;
    private IReadOnlyList<TransactionLedgerEntry> _lastRenderedLedgerEntries = Array.Empty<TransactionLedgerEntry>();
    private IReadOnlyList<MoneyActionRow> _lastRenderedMoneyActionRows = Array.Empty<MoneyActionRow>();
    private AdvisorBriefing _lastRenderedAdvisorBriefing = new AdvisorBriefing();
    private IReadOnlyList<AdvisorSuggestion> _lastRenderedAdvisorSuggestions = Array.Empty<AdvisorSuggestion>();
    private PortfolioSnapshot _lastRenderedPortfolioSnapshot = new PortfolioSnapshot();
    private AccountSnapshot _lastAccountSnapshot = new AccountSnapshot();
    private int? _selectedItemId;
    private string _selectedPortfolioKey;
    private string _selectedMoneyActionKey;
    private int _autoFlipQuantity = 10;
    private PortfolioGrowthPeriod _portfolioGrowthPeriod = PortfolioGrowthPeriod.Day;
    private Point _expandedPanelSize;
    private bool _isMinimized;
    private bool _isDraggingMiniPanel;
    private Point _dragMiniMouseOrigin;
    private Point _dragMiniOrigin;
    private bool _suppressMiniRestoreClick;
    private bool _isHiddenForGameFocus;
    private bool _restorePanelAfterFocus;
    private bool _restoreMiniAfterFocus;
    private OverlayViewMode _currentViewMode = OverlayViewMode.Market;

    public event Action QuickScanRequested;
    public event Action FullScanRequested;
    public event Action ScannerTabRequested;
    public event Action PortfolioTabRequested;
    public event Action SnipeTabRequested;
    public event Action OrdersTabRequested;
    public event Action CraftTabRequested;
    public event Action InventoryTabRequested;
    public event Action ModeCycleRequested;
    public event Action ViewCycleRequested;
    public event Action PresetCycleRequested;
    public event Action SavePresetRequested;
    public event Action SortModeCycleRequested;
    public event Action RowCountCycleRequested;
    public event Action CapCycleRequested;
    public event Action DepthCycleRequested;
    public event Action DiscountCycleRequested;
    public event Action RoiCycleRequested;
    public event Action OwnedCycleRequested;
    public event Action OpenSellCycleRequested;
    public event Action VolatilityCycleRequested;
    public event Action AutoFlipQuantityCycleRequested;
    public event Action<int> MinimumProfitAdjusted;
    public event Action<bool> PracticalOnlyToggled;
    public event Action<int> WatchlistToggleRequested;

    public FlipOverlayWindow() {
        _panel = new OverlayNativeWindow(
            AsyncTexture2D.FromAssetId(155985),
            new Rectangle(40, 26, 913, 691),
            new Rectangle(70, 71, 839, 605),
            new Point(MinPanelWidth, MinPanelHeight)) {
            Parent = GameService.Graphics.SpriteScreen,
            Location = new Point(110, 90),
            Size = new Point(1260, 700),
            Title = $"TP Flip Scanner v{BuildVersion}",
            Subtitle = "Trading Post desk",
            Emblem = AsyncTexture2D.FromAssetId(156022),
            CanResize = true,
            SavesPosition = true,
            SavesSize = true,
            Id = "Gw2FlipOverlay.MainWindow",
            Visible = false,
            CanClose = true
        };
        _panel.Hidden += (_, __) => {
            _isDraggingMiniPanel = false;
            _suppressMiniRestoreClick = false;
        };
        _expandedPanelSize = _panel.Size;

        _toolbarPanel = new Panel() {
            Parent = _panel,
            Location = new Point(14, 44),
            Size = new Point(960, 38),
            BackgroundColor = new Color(22, 32, 46, 215),
            ShowBorder = true
        };

        _filterPanel = new Panel() {
            Parent = _panel,
            Location = new Point(14, 78),
            Size = new Point(960, 38),
            BackgroundColor = new Color(28, 38, 54, 220),
            ShowBorder = true
        };

        _footerPanel = new Panel() {
            Parent = _panel,
            Location = new Point(14, 648),
            Size = new Point(1228, 42),
            BackgroundColor = new Color(20, 30, 42, 215),
            ShowBorder = true
        };

        _miniPanel = new Panel() {
            Parent = GameService.Graphics.SpriteScreen,
            Location = _panel.Location,
            Size = new Point(MinimizedPanelWidth, MinimizedPanelHeight),
            BackgroundColor = new Color(10, 16, 24, 235),
            ShowBorder = true,
            ZIndex = 101,
            Visible = false
        };
        _miniPanel.Click += HandleMiniPanelClick;
        _miniPanel.LeftMouseButtonPressed += HandleMiniPanelMousePressed;
        _miniPanel.LeftMouseButtonReleased += HandleMiniPanelMouseReleased;
        _miniPanel.MouseMoved += HandleMiniPanelMouseMoved;

        _miniTitleLabel = new Label() {
            Parent = _miniPanel,
            Location = new Point(12, 8),
            Size = new Point(220, 20),
            Text = $"TP Flip Scanner v{BuildVersion}",
            ShowShadow = true,
            TextColor = Color.White
        };
        _miniTitleLabel.Click += HandleMiniPanelClick;
        _miniTitleLabel.LeftMouseButtonPressed += HandleMiniPanelMousePressed;
        _miniTitleLabel.LeftMouseButtonReleased += HandleMiniPanelMouseReleased;
        _miniTitleLabel.MouseMoved += HandleMiniPanelMouseMoved;

        _miniRestoreButton = new StandardButton() {
            Parent = _miniPanel,
            Size = new Point(40, 24),
            Text = "+"
        };
        _miniRestoreButton.Click += (_, __) => RestoreFromMiniPanel();

        _subtitleLabel = new Label() {
            Parent = _panel,
            Location = new Point(220, 12),
            Size = new Point(420, 20),
            Text = "Trading Post desk for flips, craft margins, value dips, and account-aware exits",
            TextColor = new Color(220, 226, 234),
            ShowShadow = true
        };

        _summaryLabel = new Label() {
            Parent = _panel,
            Location = new Point(220, 30),
            Size = new Point(520, 18),
            Text = "Preset-ready scans, watchlist alerts, ledger tracking, and manual TP workflow support",
            TextColor = new Color(147, 199, 171),
            ShowShadow = true
        };

        _scannerTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(16, 12),
            Size = new Point(92, 26),
            Text = "Scanner"
        };
        _scannerTabButton.Click += (_, __) => ScannerTabRequested?.Invoke();

        _portfolioTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(114, 12),
            Size = new Point(92, 26),
            Text = "Portfolio"
        };
        _portfolioTabButton.Click += (_, __) => PortfolioTabRequested?.Invoke();

        _snipeTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(212, 12),
            Size = new Point(92, 26),
            Text = "Snipe"
        };
        _snipeTabButton.Click += (_, __) => SnipeTabRequested?.Invoke();

        _ordersTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(312, 12),
            Size = new Point(82, 26),
            Text = "Orders"
        };
        _ordersTabButton.Click += (_, __) => OrdersTabRequested?.Invoke();

        _craftTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(402, 12),
            Size = new Point(74, 26),
            Text = "Craft"
        };
        _craftTabButton.Click += (_, __) => CraftTabRequested?.Invoke();

        _inventoryTabButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(484, 12),
            Size = new Point(92, 26),
            Text = "Inventory"
        };
        _inventoryTabButton.Click += (_, __) => InventoryTabRequested?.Invoke();

        _modeButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(16, 48),
            Size = new Point(90, 28),
            Text = "Mode: Flip"
        };
        _modeButton.Click += (_, __) => ModeCycleRequested?.Invoke();

        _viewButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(114, 48),
            Size = new Point(102, 28),
            Text = "View: Market"
        };
        _viewButton.Click += (_, __) => ViewCycleRequested?.Invoke();

        _presetButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(224, 48),
            Size = new Point(146, 28),
            Text = "Preset: Default"
        };
        _presetButton.Click += (_, __) => PresetCycleRequested?.Invoke();

        _savePresetButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(378, 48),
            Size = new Point(76, 28),
            Text = "Save"
        };
        _savePresetButton.Click += (_, __) => SavePresetRequested?.Invoke();

        _sortButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(462, 48),
            Size = new Point(110, 28),
            Text = "Sort: Fast"
        };
        _sortButton.Click += (_, __) => SortModeCycleRequested?.Invoke();

        _rowsButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(580, 48),
            Size = new Point(82, 28),
            Text = "Rows: 30"
        };
        _rowsButton.Click += (_, __) => RowCountCycleRequested?.Invoke();

        _capButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(670, 48),
            Size = new Point(110, 28),
            Text = "Cap: 20g"
        };
        _capButton.Click += (_, __) => CapCycleRequested?.Invoke();

        _depthButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(788, 48),
            Size = new Point(118, 28),
            Text = "Depth: 5k"
        };
        _depthButton.Click += (_, __) => DepthCycleRequested?.Invoke();

        _discountButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(16, 82),
            Size = new Point(120, 28),
            Text = "Discount: Any"
        };
        _discountButton.Click += (_, __) => DiscountCycleRequested?.Invoke();

        _roiButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(144, 82),
            Size = new Point(94, 28),
            Text = "ROI: Any"
        };
        _roiButton.Click += (_, __) => RoiCycleRequested?.Invoke();

        _ownedButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(246, 82),
            Size = new Point(116, 28),
            Text = "Owned: Any"
        };
        _ownedButton.Click += (_, __) => OwnedCycleRequested?.Invoke();

        _openSellButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(370, 82),
            Size = new Point(118, 28),
            Text = "OpenSell: Any"
        };
        _openSellButton.Click += (_, __) => OpenSellCycleRequested?.Invoke();

        _volatilityButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(496, 82),
            Size = new Point(110, 28),
            Text = "Vol: Any"
        };
        _volatilityButton.Click += (_, __) => VolatilityCycleRequested?.Invoke();

        _autoQuantityButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(976, 82),
            Size = new Point(110, 28),
            Text = "Auto: x10"
        };
        _autoQuantityButton.Click += (_, __) => AutoFlipQuantityCycleRequested?.Invoke();

        _autoPlanButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(1094, 82),
            Size = new Point(104, 28),
            Text = "Plan Top 10"
        };
        _autoPlanButton.Click += async (_, __) => await CopyAutoFlipPlanAsync();

        _profitDownButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(614, 82),
            Size = new Point(28, 28),
            Text = "-"
        };
        _profitDownButton.Click += (_, __) => MinimumProfitAdjusted?.Invoke(-500);

        _profitLabel = new Label() {
            Parent = _panel,
            Location = new Point(650, 86),
            Size = new Point(150, 20),
            Text = "Min profit: 0g 00s 00c",
            ShowShadow = true,
            TextColor = Color.White
        };

        _profitUpButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(802, 82),
            Size = new Point(28, 28),
            Text = "+"
        };
        _profitUpButton.Click += (_, __) => MinimumProfitAdjusted?.Invoke(500);

        _practicalButton = new StandardButton() {
            Parent = _panel,
            Location = new Point(838, 82),
            Size = new Point(130, 28),
            Text = "Practical: On"
        };
        _practicalButton.Click += (_, __) => PracticalOnlyToggled?.Invoke(!_practicalButton.Text.EndsWith("On", StringComparison.Ordinal));

        _quickScanButton = new StandardButton() {
            Parent = _panel,
            Size = new Point(88, 32),
            Text = "Quick"
        };
        _quickScanButton.Click += (_, __) => QuickScanRequested?.Invoke();

        _fullScanButton = new StandardButton() {
            Parent = _panel,
            Size = new Point(88, 32),
            Text = "Full"
        };
        _fullScanButton.Click += (_, __) => FullScanRequested?.Invoke();

        _minimizeButton = new StandardButton() {
            Parent = _panel,
            Size = new Point(24, 24),
            Text = "-"
        };
        _minimizeButton.Click += (_, __) => ToggleMinimized();

        _closeButton = new StandardButton() {
            Parent = _panel,
            Size = new Point(24, 24),
            Text = "X"
        };
        _closeButton.Click += (_, __) => HideWindow();

        _statusLabel = new Label() {
            Parent = _panel,
            Location = new Point(24, 122),
            Size = new Point(1220, 24),
            Text = "Waiting for first scan...",
            ShowShadow = true,
            TextColor = new Color(239, 242, 199)
        };

        _portfolioStripLabel = new Label() {
            Parent = _panel,
            Location = new Point(24, 142),
            Size = new Point(1220, 20),
            Text = "Portfolio strip updates after account syncs and scans.",
            ShowShadow = true,
            TextColor = new Color(152, 220, 186)
        };

        _rankHeaderLabel = CreateStaticCell(16, 168, 24, "#");
        _itemHeaderLabel = CreateStaticCell(46, 168, 228, "Item");
        _costHeaderLabel = CreateStaticCell(280, 168, 70, "Buy");
        _sellHeaderLabel = CreateStaticCell(356, 168, 70, "Sell");
        _profitHeaderLabel = CreateStaticCell(432, 168, 72, "Profit");
        _roiHeaderLabel = CreateStaticCell(510, 168, 54, "ROI");
        _depthHeaderLabel = CreateStaticCell(570, 168, 64, "Depth");
        _pressHeaderLabel = CreateStaticCell(640, 168, 56, "Press");
        _turnHeaderLabel = CreateStaticCell(702, 168, 60, "Turn");
        _scoreHeaderLabel = CreateStaticCell(768, 168, 70, "Fast");
        CreateHeader();

        _listPanel = new Panel() {
            Parent = _panel,
            Location = new Point(16, 194),
            Size = new Point(820, 470),
            BackgroundColor = new Color(10, 16, 24, 190),
            ShowBorder = true,
            CanScroll = true
        };

        _listScrollbar = new Scrollbar(_panel) {
            Location = new Point(820, 198),
            Size = new Point(16, 462),
            AssociatedContainer = _listPanel,
            ScrollDistance = 48f
        };

        _detailPanel = new Panel() {
            Parent = _panel,
            Location = new Point(874, 168),
            Size = new Point(360, 498),
            BackgroundColor = new Color(20, 28, 40, 235),
            ShowBorder = true,
            Title = "Inspect",
            CanScroll = true
        };

        _detailScrollbar = new Scrollbar(_panel) {
            Location = new Point(1218, 182),
            Size = new Point(16, 456),
            AssociatedContainer = _detailPanel,
            ScrollDistance = 48f
        };

        _detailTitleLabel = new Label() {
            Parent = _detailPanel,
            Location = new Point(12, 36),
            Size = new Point(330, 26),
            Text = "Select a row",
            ShowShadow = true,
            TextColor = Color.White
        };

        _detailSubtitleLabel = new Label() {
            Parent = _detailPanel,
            Location = new Point(12, 62),
            Size = new Point(330, 20),
            Text = "Quick TP workflow and market stats",
            ShowShadow = true,
            TextColor = Color.LightGray
        };

        _copyNameButton = new StandardButton() {
            Parent = _detailPanel,
            Location = new Point(12, 94),
            Size = new Point(102, 28),
            Text = "Copy Name",
            Enabled = false
        };
        _copyNameButton.Click += async (_, __) => await CopySelectedTextAsync(GetSelectedItemName(), "Copied item name to clipboard.");

        _copyWikiButton = new StandardButton() {
            Parent = _detailPanel,
            Location = new Point(122, 94),
            Size = new Point(96, 28),
            Text = "Copy /wiki",
            Enabled = false
        };
        _copyWikiButton.Click += async (_, __) => await CopySelectedTextAsync(BuildWikiCommand(GetSelectedCandidate(), GetSelectedAdvisorSuggestion(), GetSelectedPortfolioRow(), GetSelectedMoneyAction()), "Copied /wiki command to clipboard.");

        _openWikiButton = new StandardButton() {
            Parent = _detailPanel,
            Location = new Point(226, 94),
            Size = new Point(96, 28),
            Text = "Open Wiki",
            Enabled = false
        };
        _openWikiButton.Click += (_, __) => OpenSelectedWiki();

        _watchButton = new StandardButton() {
            Parent = _detailPanel,
            Location = new Point(12, 128),
            Size = new Point(96, 28),
            Text = "Watch",
            Enabled = false
        };
        _watchButton.Click += (_, __) => {
            var selectedCandidate = GetSelectedCandidate();
            var ledgerEntry = GetSelectedLedgerEntry();
            var advisorSuggestion = GetSelectedAdvisorSuggestion();
            var portfolioRow = GetSelectedPortfolioRow();
            var moneyAction = GetSelectedMoneyAction();

            if (selectedCandidate != null) {
                WatchlistToggleRequested?.Invoke(selectedCandidate.ItemId);
            } else if (ledgerEntry != null) {
                WatchlistToggleRequested?.Invoke(ledgerEntry.ItemId);
            } else if (advisorSuggestion != null) {
                WatchlistToggleRequested?.Invoke(advisorSuggestion.ItemId);
            } else if (portfolioRow != null) {
                WatchlistToggleRequested?.Invoke(portfolioRow.ItemId);
            } else if (moneyAction != null) {
                WatchlistToggleRequested?.Invoke(moneyAction.ItemId);
            }
        };

        _detailHintLabel = new Label() {
            Parent = _detailPanel,
            Location = new Point(12, 168),
            Size = new Point(330, 48),
            WrapText = true,
            ShowShadow = true,
            Text = "Manual TP inspect: copy the item name here, paste it into the in-game Trading Post search box, then decide whether the depth and spread still look healthy.",
            TextColor = new Color(196, 204, 216)
        };

        _detailStatsLabel = new Label() {
            Parent = _detailPanel,
            Location = new Point(12, 232),
            Size = new Point(330, 220),
            WrapText = true,
            ShowShadow = true,
            Text = "No item selected yet.",
            TextColor = Color.White
        };

        _detailTrendLabel = new Label() {
            Parent = _detailPanel,
            Location = new Point(12, 500),
            Size = new Point(314, 112),
            WrapText = true,
            ShowShadow = true,
            Text = "Trend data will appear once a row is selected.",
            TextColor = Color.LightGray
        };

        _updatedLabel = new Label() {
            Parent = _panel,
            Size = new Point(1220, 48),
            WrapText = true,
            ShowShadow = true,
            Text = "Last update: not yet",
            TextColor = new Color(198, 214, 226)
        };

        _resizeHandle = new Panel() {
            Parent = _panel,
            Size = new Point(ResizeHandleSize, ResizeHandleSize),
            BackgroundColor = new Color(178, 124, 52, 245),
            ShowBorder = true,
            Visible = false
        };

        _resizeGlyphLabel = new Label() {
            Parent = _resizeHandle,
            Location = new Point(8, 6),
            Size = new Point(20, 20),
            Text = "///",
            ShowShadow = true,
            TextColor = new Color(250, 247, 238),
            Visible = false
        };

        ApplyLayout();
        UpdateDetailPanel();
    }

    public bool IsVisible => _panel.Visible || _miniPanel.Visible;

    public void Show() {
        _isHiddenForGameFocus = false;
        _restorePanelAfterFocus = false;
        _restoreMiniAfterFocus = false;
        _miniPanel.Visible = false;
        _isMinimized = false;
        _isDraggingMiniPanel = false;
        _panel.Location = ClampLocationToScreen(_panel.Location, _expandedPanelSize.X, _expandedPanelSize.Y);
        _panel.Show();
        SetContentVisibility(true);
        _panel.Size = _expandedPanelSize;
        _minimizeButton.Text = "-";
        ApplyLayout();
    }

    public void Toggle() {
        if (_panel.Visible || _miniPanel.Visible || _isHiddenForGameFocus) {
            HideWindow();
            return;
        }

        Show();
    }

    public void SetQueryState(FlipQueryOptions queryOptions, OverlayViewMode viewMode, string presetName) {
        _currentViewMode = viewMode;
        _modeButton.Text = $"Mode: {GetModeLabel(queryOptions.OpportunityMode)}";
        _viewButton.Text = $"View: {GetViewModeLabel(viewMode)}";
        _presetButton.Text = $"Preset: {TrimLabel(presetName, 12)}";
        _sortButton.Text = $"Sort: {GetSortModeLabel(queryOptions.SortMode)}";
        _rowsButton.Text = $"Rows: {queryOptions.TopCount}";
        _capButton.Text = $"Cap: {FormatCap(queryOptions.MaxAcquireCostCopper)}";
        _depthButton.Text = $"Depth: {FormatDepth(queryOptions.MinimumMarketDepth)}";
        _discountButton.Text = queryOptions.OpportunityMode == OpportunityMode.Value
            ? $"Discount: {FormatDiscount(queryOptions.MinimumDiscountPercent)}"
            : "Discount: n/a";
        _roiButton.Text = $"ROI: {FormatPercentFilter(queryOptions.MinimumRoiPercent)}";
        _ownedButton.Text = $"Owned: {FormatQuantityFilter(queryOptions.MaxOwnedQuantity)}";
        _openSellButton.Text = $"OpenSell: {FormatQuantityFilter(queryOptions.MaxOpenSellQuantity)}";
        _volatilityButton.Text = $"Vol: {FormatPercentFilter(queryOptions.MaxVolatilityPercent)}";
        _autoFlipQuantity = Math.Max(1, queryOptions.AutoFlipQuantity);
        _autoQuantityButton.Text = $"Auto: x{_autoFlipQuantity:N0}";
        _discountButton.Enabled = queryOptions.OpportunityMode == OpportunityMode.Value;
        _profitLabel.Text = $"Min profit: {FormatCoin(queryOptions.MinimumProfitCopper)}";
        _practicalButton.Text = queryOptions.PracticalOnly ? "Practical: On" : "Practical: Off";
        _costHeaderLabel.Text = queryOptions.OpportunityMode == OpportunityMode.Craft ? "Cost" : "Buy";
        _summaryLabel.Text = BuildSummaryText(queryOptions, viewMode, presetName);
        _detailPanel.Title = viewMode == OverlayViewMode.Ledger
            ? "Position Drilldown"
            : (viewMode == OverlayViewMode.Watchlist
                ? "Watchlist Inspect"
                : (viewMode == OverlayViewMode.Advisor
                    ? "Advisor Inspect"
                    : (viewMode == OverlayViewMode.Portfolio
                        ? "Portfolio Drilldown"
                        : (viewMode == OverlayViewMode.Snipe
                            ? "Snipe Inspect"
                            : (IsMoneyActionView(viewMode) ? "Action Inspect" : "Inspect")))));
        ApplyChromeForView(viewMode);
        UpdateHeadersForView(queryOptions.OpportunityMode, viewMode);
    }

    public void UpdatePortfolioSummary(PortfolioSnapshot portfolioSnapshot) {
        _lastRenderedPortfolioSnapshot = portfolioSnapshot ?? new PortfolioSnapshot();
        UpdatePortfolioStripLabel(_lastRenderedPortfolioSnapshot.Summary);
    }

    public void SetWatchlist(IEnumerable<int> itemIds) {
        _watchlistItemIds.Clear();

        if (itemIds != null) {
            foreach (var itemId in itemIds) {
                _watchlistItemIds.Add(itemId);
            }
        }

        UpdateDetailPanel();
    }

    public void SetBusy(bool isBusy) {
        _quickScanButton.Enabled = !isBusy;
        _fullScanButton.Enabled = !isBusy;
        _quickScanButton.Text = isBusy ? "Scanning" : "Quick";
        _fullScanButton.Text = isBusy ? "Please..." : "Full";
    }

    public void SetStatus(string message) {
        _statusLabel.Text = message;
    }

    public void RenderRows(MarketScanResult scanResult, DateTime updatedAt, OverlayViewMode viewMode, AccountSnapshot accountSnapshot) {
        _currentViewMode = viewMode;
        _lastAccountSnapshot = accountSnapshot ?? new AccountSnapshot();
        _lastRenderedResult = scanResult;
        _lastRenderedLedgerEntries = Array.Empty<TransactionLedgerEntry>();
        _lastRenderedMoneyActionRows = Array.Empty<MoneyActionRow>();
        _lastRenderedAdvisorSuggestions = Array.Empty<AdvisorSuggestion>();
        var listScrollOffset = _listPanel.VerticalScrollOffset;
        ClearRows();

        if (scanResult.Candidates.Count > 0) {
            if (!_selectedItemId.HasValue || !ContainsCandidate(scanResult, _selectedItemId.Value)) {
                _selectedItemId = scanResult.Candidates[0].ItemId;
            }
        } else {
            _selectedItemId = null;
        }

        var rowY = 4;
        var rank = 1;

        foreach (var candidate in scanResult.Candidates) {
            CreateRow(rank, candidate, rowY);
            rowY += 28;
            rank++;
        }

        if (scanResult.Candidates.Count == 0) {
            CreateEmptyState(viewMode == OverlayViewMode.Snipe
                ? "No snipe deals matched yet. Run Quick or Full after cycling through Flip and Value modes, or loosen ROI/depth/capital filters."
                : "No candidates matched the current filters. Try lowering min profit, depth, or practical filtering.");
        }

        _listPanel.VerticalScrollOffset = listScrollOffset;
        var modeLabel = viewMode == OverlayViewMode.Snipe ? "All cached modes" : scanResult.OpportunityMode.ToString();
        _updatedLabel.Text = $"Last update: {updatedAt:yyyy-MM-dd HH:mm:ss} | View: {GetViewModeLabel(viewMode)} | Mode: {modeLabel} | Showing {scanResult.Candidates.Count}/{scanResult.FilteredCandidateCount} rows | Universe: {scanResult.UniverseCandidateCount} | Wallet: {FormatCoin(_lastAccountSnapshot.AvailableCopper)}";
        UpdatePortfolioStripLabel(_lastRenderedPortfolioSnapshot?.Summary);
        UpdateDetailPanel();
    }

    public void RenderLedgerRows(IReadOnlyList<TransactionLedgerEntry> entries, DateTime updatedAt, AccountSnapshot accountSnapshot) {
        _currentViewMode = OverlayViewMode.Ledger;
        _lastAccountSnapshot = accountSnapshot ?? new AccountSnapshot();
        _lastRenderedLedgerEntries = entries ?? Array.Empty<TransactionLedgerEntry>();
        _lastRenderedResult = null;
        _lastRenderedMoneyActionRows = Array.Empty<MoneyActionRow>();
        _lastRenderedAdvisorSuggestions = Array.Empty<AdvisorSuggestion>();
        var listScrollOffset = _listPanel.VerticalScrollOffset;
        ClearRows();

        if (_lastRenderedLedgerEntries.Count > 0) {
            if (!_selectedItemId.HasValue || !_lastRenderedLedgerEntries.Any(entry => entry.ItemId == _selectedItemId.Value)) {
                _selectedItemId = _lastRenderedLedgerEntries[0].ItemId;
            }
        } else {
            _selectedItemId = null;
        }

        var rowY = 4;
        var rank = 1;

        foreach (var entry in _lastRenderedLedgerEntries) {
            CreateLedgerRow(rank, entry, rowY);
            rowY += 28;
            rank++;
        }

        if (_lastRenderedLedgerEntries.Count == 0) {
            CreateEmptyState("No ledger items yet. Add an API key and run a scan to build realized and unrealized profit tracking.");
        }

        _listPanel.VerticalScrollOffset = listScrollOffset;
        _updatedLabel.Text = $"Last account sync: {updatedAt:yyyy-MM-dd HH:mm:ss} | View: Ledger | Items: {_lastRenderedLedgerEntries.Count} | Wallet: {FormatCoin(_lastAccountSnapshot.AvailableCopper)}";
        UpdatePortfolioStripLabel(_lastRenderedPortfolioSnapshot?.Summary);
        UpdateDetailPanel();
    }

    public void RenderAdvisor(AdvisorBriefing briefing, DateTime updatedAt, AccountSnapshot accountSnapshot, PortfolioSnapshot portfolioSnapshot) {
        _currentViewMode = OverlayViewMode.Advisor;
        _lastAccountSnapshot = accountSnapshot ?? new AccountSnapshot();
        _lastRenderedAdvisorBriefing = briefing ?? new AdvisorBriefing();
        _lastRenderedPortfolioSnapshot = portfolioSnapshot ?? new PortfolioSnapshot();
        _lastRenderedResult = null;
        _lastRenderedLedgerEntries = Array.Empty<TransactionLedgerEntry>();
        _lastRenderedMoneyActionRows = Array.Empty<MoneyActionRow>();
        _lastRenderedAdvisorSuggestions = BuildAdvisorRows(_lastRenderedAdvisorBriefing);
        var listScrollOffset = _listPanel.VerticalScrollOffset;
        ClearRows();

        if (_lastRenderedAdvisorSuggestions.Count > 0) {
            if (!_selectedItemId.HasValue || !_lastRenderedAdvisorSuggestions.Any(suggestion => suggestion.ItemId == _selectedItemId.Value)) {
                _selectedItemId = _lastRenderedAdvisorSuggestions[0].ItemId;
            }
        } else {
            _selectedItemId = null;
        }

        var rowY = 4;
        var rank = 1;

        foreach (var suggestion in _lastRenderedAdvisorSuggestions) {
            CreateAdvisorRow(rank, suggestion, rowY);
            rowY += 28;
            rank++;
        }

        if (_lastRenderedAdvisorSuggestions.Count == 0) {
            CreateEmptyState("Advisor is waiting for scan data. Run scans in flip, craft, cooldown, or investment modes to populate guided picks.");
        }

        _listPanel.VerticalScrollOffset = listScrollOffset;
        _updatedLabel.Text = $"Advisor update: {updatedAt:yyyy-MM-dd HH:mm:ss} | Picks: {_lastRenderedAdvisorSuggestions.Count} | Wallet: {FormatCoin(_lastAccountSnapshot.AvailableCopper)} | Net worth: {FormatCoin(_lastRenderedPortfolioSnapshot.Summary?.NetWorthCopper ?? 0)}";
        UpdatePortfolioStripLabel(_lastRenderedPortfolioSnapshot?.Summary);
        UpdateDetailPanel();
    }

    public void RenderPortfolio(PortfolioSnapshot snapshot, AccountSnapshot accountSnapshot) {
        _currentViewMode = OverlayViewMode.Portfolio;
        _lastAccountSnapshot = accountSnapshot ?? new AccountSnapshot();
        _lastRenderedPortfolioSnapshot = snapshot ?? new PortfolioSnapshot();
        _lastRenderedResult = null;
        _lastRenderedLedgerEntries = Array.Empty<TransactionLedgerEntry>();
        _lastRenderedMoneyActionRows = Array.Empty<MoneyActionRow>();
        _lastRenderedAdvisorSuggestions = Array.Empty<AdvisorSuggestion>();
        var listScrollOffset = _listPanel.VerticalScrollOffset;
        ClearRows();

        if (_lastRenderedPortfolioSnapshot.Rows.Count > 0) {
            if (string.IsNullOrWhiteSpace(_selectedPortfolioKey) || !_lastRenderedPortfolioSnapshot.Rows.Any(row => string.Equals(GetPortfolioKey(row), _selectedPortfolioKey, StringComparison.Ordinal))) {
                var firstRow = _lastRenderedPortfolioSnapshot.Rows[0];
                _selectedPortfolioKey = GetPortfolioKey(firstRow);
                _selectedItemId = firstRow.ItemId > 0 ? firstRow.ItemId : null;
            }
        } else {
            _selectedPortfolioKey = null;
            _selectedItemId = null;
        }

        var rowY = CreatePortfolioOverview(_lastRenderedPortfolioSnapshot);
        var rank = 1;
        foreach (var row in _lastRenderedPortfolioSnapshot.Rows) {
            CreatePortfolioRow(rank, row, rowY);
            rowY += 28;
            rank++;
        }

        if (_lastRenderedPortfolioSnapshot.Rows.Count == 0) {
            CreateEmptyState("Portfolio is waiting for authenticated account data or tracked inventory. Add an API key and run a scan to build wallet, order, and holdings worth.", rowY + 4);
        }

        _listPanel.VerticalScrollOffset = listScrollOffset;
        var summary = _lastRenderedPortfolioSnapshot.Summary ?? new PortfolioSummary();
        _updatedLabel.Text = $"Portfolio update: {_lastRenderedPortfolioSnapshot.CapturedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss} | Net worth: {FormatCoin(summary.NetWorthCopper)} | Daily: {FormatSignedCoin(summary.DailyDeltaCopper)} | Weekly: {FormatSignedCoin(summary.WeeklyDeltaCopper)} | 30d: {FormatSignedCoin(summary.MonthlyDeltaCopper)}";
        UpdatePortfolioStripLabel(summary);
        UpdateDetailPanel();
    }

    public void RenderMoneyActions(IReadOnlyList<MoneyActionRow> rows, DateTime updatedAt, OverlayViewMode viewMode, AccountSnapshot accountSnapshot) {
        _currentViewMode = viewMode;
        _lastAccountSnapshot = accountSnapshot ?? new AccountSnapshot();
        _lastRenderedMoneyActionRows = rows ?? Array.Empty<MoneyActionRow>();
        _lastRenderedResult = null;
        _lastRenderedLedgerEntries = Array.Empty<TransactionLedgerEntry>();
        _lastRenderedAdvisorSuggestions = Array.Empty<AdvisorSuggestion>();
        var listScrollOffset = _listPanel.VerticalScrollOffset;
        ClearRows();

        if (_lastRenderedMoneyActionRows.Count > 0) {
            if (string.IsNullOrWhiteSpace(_selectedMoneyActionKey) || !_lastRenderedMoneyActionRows.Any(row => string.Equals(GetMoneyActionKey(row), _selectedMoneyActionKey, StringComparison.Ordinal))) {
                var firstRow = _lastRenderedMoneyActionRows[0];
                _selectedMoneyActionKey = GetMoneyActionKey(firstRow);
                _selectedItemId = firstRow.ItemId > 0 ? firstRow.ItemId : null;
            }
        } else {
            _selectedMoneyActionKey = null;
            _selectedItemId = null;
        }

        var rowY = 4;
        var rank = 1;
        foreach (var row in _lastRenderedMoneyActionRows) {
            CreateMoneyActionRow(rank, row, rowY);
            rowY += 28;
            rank++;
        }

        if (_lastRenderedMoneyActionRows.Count == 0) {
            CreateEmptyState(GetMoneyActionEmptyText(viewMode));
        }

        _listPanel.VerticalScrollOffset = listScrollOffset;
        _updatedLabel.Text = $"{GetViewModeLabel(viewMode)} update: {updatedAt:yyyy-MM-dd HH:mm:ss} | Actions: {_lastRenderedMoneyActionRows.Count} | Wallet: {FormatCoin(_lastAccountSnapshot.AvailableCopper)}";
        UpdatePortfolioStripLabel(_lastRenderedPortfolioSnapshot?.Summary);
        UpdateDetailPanel();
    }

    public void Dispose() {
        ClearDetailGraph();
        _miniPanel.Dispose();
        _listScrollbar.Dispose();
        _detailScrollbar.Dispose();
        _panel.Dispose();
    }

    public void UpdateInteraction() {
        if (!_isMinimized && _panel.Size != _expandedPanelSize) {
            _expandedPanelSize = _panel.Size;
            ApplyLayout();

            if (_lastRenderedResult != null) {
                RenderRows(_lastRenderedResult, _lastRenderedResult.GeneratedAtUtc.LocalDateTime, _currentViewMode, _lastAccountSnapshot);
            } else if (_currentViewMode == OverlayViewMode.Advisor && _lastRenderedAdvisorBriefing != null) {
                RenderAdvisor(_lastRenderedAdvisorBriefing, _lastRenderedAdvisorBriefing.GeneratedAtUtc.LocalDateTime, _lastAccountSnapshot, _lastRenderedPortfolioSnapshot);
            } else if (_currentViewMode == OverlayViewMode.Portfolio && _lastRenderedPortfolioSnapshot != null) {
                RenderPortfolio(_lastRenderedPortfolioSnapshot, _lastAccountSnapshot);
            } else if (_lastRenderedLedgerEntries != null && _lastRenderedLedgerEntries.Count > 0) {
                RenderLedgerRows(_lastRenderedLedgerEntries, _lastAccountSnapshot.CapturedAtUtc.LocalDateTime, _lastAccountSnapshot);
            }
        }

        if (!_isDraggingMiniPanel) {
            return;
        }

        var mousePosition = GameService.Input.Mouse.Position;
        var delta = mousePosition - _dragMiniMouseOrigin;

        if (delta.X != 0 || delta.Y != 0) {
            _suppressMiniRestoreClick = true;
        }

        _miniPanel.Location = ClampLocationToScreen(_dragMiniOrigin + delta, _miniPanel.Width, _miniPanel.Height);
    }

    public void SetGameFocusState(bool hasGameFocus, bool hideWhenUnfocused) {
        if (!hideWhenUnfocused) {
            if (_isHiddenForGameFocus) {
                RestoreAfterGameFocus();
            }

            return;
        }

        if (!hasGameFocus) {
            HideForGameFocus();
            return;
        }

        if (_isHiddenForGameFocus) {
            RestoreAfterGameFocus();
        }
    }

    private void HideWindow() {
        _isHiddenForGameFocus = false;
        _restorePanelAfterFocus = false;
        _restoreMiniAfterFocus = false;
        SetContentVisibility(false);
        _panel.Hide();
        _miniPanel.Visible = false;
        _isMinimized = false;
        _isDraggingMiniPanel = false;
        _suppressMiniRestoreClick = false;
    }

    private void HideForGameFocus() {
        if (_isHiddenForGameFocus || (!_panel.Visible && !_miniPanel.Visible)) {
            return;
        }

        _restorePanelAfterFocus = _panel.Visible;
        _restoreMiniAfterFocus = _miniPanel.Visible;
        _isHiddenForGameFocus = true;
        _panel.Visible = false;
        _miniPanel.Visible = false;
        _isDraggingMiniPanel = false;
        _suppressMiniRestoreClick = false;
    }

    private void RestoreAfterGameFocus() {
        _isHiddenForGameFocus = false;

        if (_restoreMiniAfterFocus) {
            _miniPanel.Visible = true;
            ApplyMiniLayout();
        } else if (_restorePanelAfterFocus) {
            _panel.Show();
            SetContentVisibility(true);
            ApplyLayout();
        }

        _restorePanelAfterFocus = false;
        _restoreMiniAfterFocus = false;
    }

    private void CreateHeader() {
        UpdateHeadersForView(OpportunityMode.Flip, OverlayViewMode.Market);
    }

    private Label CreateStaticCell(int x, int y, int width, string text) {
        var label = new Label() {
            Parent = _panel,
            Location = new Point(x, y),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = Color.LightGray
        };

        return label;
    }

    private void CreateRow(int rank, FlipCandidate candidate, int y) {
        var isSelected = _selectedItemId == candidate.ItemId;
        var isWatched = _watchlistItemIds.Contains(candidate.ItemId);
        var rowWidth = Math.Max(260, _listPanel.Width - 12);
        var rowPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(4, y),
            Size = new Point(rowWidth, 26),
            BackgroundColor = isSelected
                ? new Color(44, 86, 118, 235)
                : (isWatched ? new Color(73, 56, 26, rank % 2 == 0 ? 185 : 152) : new Color(22, 32, 46, rank % 2 == 0 ? 150 : 116)),
            ShowBorder = false
        };

        _dynamicControls.Add(rowPanel);
        AttachSelection(rowPanel, candidate);
        CreateRowAccent(rowPanel, GetRecommendationColor(candidate.RecommendationState));

        var itemLabel = BuildCandidateRowLabel(candidate, isWatched);
        var tooltip = BuildItemTooltip(candidate);
        CreateRowCell(rowPanel, 0, 24, $"{rank}", Color.LightGray, tooltip, candidate);
        CreateRowCell(rowPanel, 30, 228, itemLabel, GetRarityColor(candidate.Rarity), tooltip, candidate);
        CreateRowCell(rowPanel, 264, 70, FormatCoin(candidate.AcquisitionCostCopper > 0 ? candidate.AcquisitionCostCopper : candidate.HighestBuy), Color.White, null, candidate);
        CreateRowCell(rowPanel, 340, 70, FormatCoin(candidate.LowestSell), Color.White, null, candidate);
        CreateRowCell(rowPanel, 416, 72, FormatCoin(candidate.EstimatedProfit), candidate.EstimatedProfit > 0 ? Color.Gold : Color.IndianRed, null, candidate);
        CreateRowCell(rowPanel, 494, 54, $"{candidate.MarketValuePercent:N0}%", GetMarketValueColor(candidate.MarketValueBand), tooltip, candidate);
        CreateRowCell(rowPanel, 554, 64, FormatMarketValueBandShort(candidate.MarketValueBand), GetMarketValueColor(candidate.MarketValueBand), tooltip, candidate);
        CreateRowCell(rowPanel, 624, 56, candidate.MarketDepth.ToString("N0"), Color.White, null, candidate);
        CreateRowCell(rowPanel, 686, 60, $"{candidate.TurnoverScore:N2}", Color.White, null, candidate);
        CreateRowCell(rowPanel, 752, 70, _currentViewMode == OverlayViewMode.Watchlist
            ? $"{candidate.AlertScore:N1}"
            : (_currentViewMode == OverlayViewMode.Snipe ? FormatSnipeEdge(candidate) : FormatCoinCompact(candidate.ExpectedGoldPerDayCopper)), Color.LightGreen, null, candidate);
    }

    private void CreateRowCell(Container parent, int x, int width, string text, Color textColor, string tooltip, FlipCandidate candidate) {
        var label = new Label() {
            Parent = parent,
            Location = new Point(x + 6, 1),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };

        if (!string.IsNullOrWhiteSpace(tooltip)) {
            label.BasicTooltipText = tooltip;
        }

        AttachSelection(label, candidate);
    }

    private void CreateLedgerRow(int rank, TransactionLedgerEntry entry, int y) {
        var isSelected = _selectedItemId == entry.ItemId;
        var rowWidth = Math.Max(260, _listPanel.Width - 12);
        var rowPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(4, y),
            Size = new Point(rowWidth, 26),
            BackgroundColor = isSelected ? new Color(44, 86, 118, 235) : new Color(22, 32, 46, rank % 2 == 0 ? 150 : 116),
            ShowBorder = false
        };

        _dynamicControls.Add(rowPanel);
        AttachLedgerSelection(rowPanel, entry);
        CreateRowAccent(rowPanel, GetRecommendationColor(entry.RecommendationState));

        var tooltip = BuildLedgerTooltip(entry);
        CreateLedgerCell(rowPanel, 0, 24, $"{rank}", Color.LightGray, tooltip, entry);
        CreateLedgerCell(rowPanel, 30, 228, entry.ItemName, Color.White, tooltip, entry);
        CreateLedgerCell(rowPanel, 264, 70, FormatCoin(entry.AverageBuyPriceCopper), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 340, 70, FormatCoin(entry.CurrentSellFloorCopper), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 416, 72, FormatCoin(entry.RealizedProfitCopper + entry.UnrealizedProfitCopper), entry.RealizedProfitCopper + entry.UnrealizedProfitCopper >= 0 ? Color.Gold : Color.IndianRed, null, entry);
        CreateLedgerCell(rowPanel, 494, 54, entry.HeldQuantity.ToString("N0"), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 554, 64, entry.BoughtQuantity.ToString("N0"), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 624, 56, entry.SoldQuantity.ToString("N0"), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 686, 60, (entry.CurrentOpenBuyQuantity + entry.CurrentOpenSellQuantity).ToString("N0"), Color.White, null, entry);
        CreateLedgerCell(rowPanel, 752, 70, $"{entry.MarketConfidenceScore:N0}", Color.LightGreen, null, entry);
    }

    private void CreateAdvisorRow(int rank, AdvisorSuggestion suggestion, int y) {
        var isSelected = _selectedItemId == suggestion.ItemId;
        var rowWidth = Math.Max(260, _listPanel.Width - 12);
        var rowPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(4, y),
            Size = new Point(rowWidth, 26),
            BackgroundColor = isSelected
                ? new Color(44, 86, 118, 235)
                : new Color(18, 29, 42, rank % 2 == 0 ? 152 : 118),
            ShowBorder = false
        };

        _dynamicControls.Add(rowPanel);
        AttachAdvisorSelection(rowPanel, suggestion);
        CreateRowAccent(rowPanel, GetAdvisorActionColor(suggestion.Action));

        var tooltip = BuildAdvisorTooltip(suggestion);
        CreateAdvisorCell(rowPanel, 0, 24, $"{rank}", Color.LightGray, tooltip, suggestion);
        CreateAdvisorCell(rowPanel, 30, 228, BuildAdvisorRowLabel(suggestion), Color.White, tooltip, suggestion);
        CreateAdvisorCell(rowPanel, 264, 70, FormatCoin(suggestion.CapitalRequiredCopper), Color.White, null, suggestion);
        CreateAdvisorCell(rowPanel, 340, 70, FormatCoin(suggestion.EstimatedProfitCopper), suggestion.EstimatedProfitCopper >= 0 ? Color.Gold : Color.IndianRed, null, suggestion);
        CreateAdvisorCell(rowPanel, 416, 72, $"MV {suggestion.MarketValuePercent:N0}%", GetMarketValuePercentColor(suggestion.MarketValuePercent), tooltip, suggestion);
        CreateAdvisorCell(rowPanel, 494, 54, $"{suggestion.ConfidenceScore:N0}", Color.White, null, suggestion);
        CreateAdvisorCell(rowPanel, 554, 64, FormatLaneLabel(suggestion.StrategyTag), Color.White, null, suggestion);
        CreateAdvisorCell(rowPanel, 624, 56, FormatHorizon(suggestion.InvestmentHorizonDays), Color.White, null, suggestion);
        CreateAdvisorCell(rowPanel, 686, 60, FormatActionShort(suggestion.Action), GetAdvisorActionColor(suggestion.Action), null, suggestion);
        CreateAdvisorCell(rowPanel, 752, 70, FormatCoinCompact(suggestion.ExpectedGoldPerDayCopper), Color.LightGreen, null, suggestion);
    }

    private void CreatePortfolioRow(int rank, PortfolioRow row, int y) {
        var isSelected = string.Equals(_selectedPortfolioKey, GetPortfolioKey(row), StringComparison.Ordinal);
        var isWatched = _watchlistItemIds.Contains(row.ItemId);
        var rowWidth = Math.Max(260, _listPanel.Width - 12);
        var rowPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(4, y),
            Size = new Point(rowWidth, 26),
            BackgroundColor = isSelected
                ? new Color(44, 86, 118, 235)
                : (isWatched ? new Color(73, 56, 26, rank % 2 == 0 ? 185 : 152) : new Color(18, 29, 42, rank % 2 == 0 ? 152 : 118)),
            ShowBorder = false
        };

        _dynamicControls.Add(rowPanel);
        AttachPortfolioSelection(rowPanel, row);
        CreateRowAccent(rowPanel, GetPortfolioKindColor(row.Kind));

        var tooltip = BuildPortfolioTooltip(row);
        CreatePortfolioCell(rowPanel, 0, 24, $"{rank}", Color.LightGray, tooltip, row);
        CreatePortfolioCell(rowPanel, 30, 228, BuildPortfolioRowLabel(row, isWatched), Color.White, tooltip, row);
        CreatePortfolioCell(rowPanel, 264, 70, row.Quantity.ToString("N0"), Color.White, null, row);
        CreatePortfolioCell(rowPanel, 340, 70, FormatCoinCompact(row.UnitPriceCopper), Color.White, null, row);
        CreatePortfolioCell(rowPanel, 416, 72, FormatCoinCompact(row.NetValueCopper), row.NetValueCopper >= 0 ? Color.Gold : Color.IndianRed, null, row);
        CreatePortfolioCell(rowPanel, 494, 54, $"{row.MarketValuePercent:N0}%", GetMarketValuePercentColor(row.MarketValuePercent), tooltip, row);
        CreatePortfolioCell(rowPanel, 554, 64, FormatPortfolioKind(row.Kind), GetPortfolioKindColor(row.Kind), null, row);
        CreatePortfolioCell(rowPanel, 624, 56, FormatCoinCompact(row.FairValueCopper), Color.White, null, row);
        CreatePortfolioCell(rowPanel, 686, 60, TrimLabel(ExtractMarketValueBand(row.MarketValueLabel), 8), GetMarketValuePercentColor(row.MarketValuePercent), null, row);
        CreatePortfolioCell(rowPanel, 752, 70, TrimLabel(BuildPortfolioNoteLabel(row), 10), Color.LightGreen, tooltip, row);
    }

    private void CreateMoneyActionRow(int rank, MoneyActionRow row, int y) {
        var isSelected = string.Equals(_selectedMoneyActionKey, GetMoneyActionKey(row), StringComparison.Ordinal);
        var isWatched = _watchlistItemIds.Contains(row.ItemId);
        var rowWidth = Math.Max(260, _listPanel.Width - 12);
        var rowPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(4, y),
            Size = new Point(rowWidth, 26),
            BackgroundColor = isSelected
                ? new Color(44, 86, 118, 235)
                : (isWatched ? new Color(73, 56, 26, rank % 2 == 0 ? 185 : 152) : new Color(18, 29, 42, rank % 2 == 0 ? 152 : 118)),
            ShowBorder = false
        };

        _dynamicControls.Add(rowPanel);
        AttachMoneyActionSelection(rowPanel, row);
        CreateRowAccent(rowPanel, GetMoneyActionColor(row));

        var tooltip = BuildMoneyActionTooltip(row);
        CreateMoneyActionCell(rowPanel, 0, 24, $"{rank}", Color.LightGray, tooltip, row);
        CreateMoneyActionCell(rowPanel, 30, 228, BuildMoneyActionLabel(row, isWatched), Color.White, tooltip, row);
        CreateMoneyActionCell(rowPanel, 264, 70, row.Action, GetMoneyActionColor(row), tooltip, row);
        CreateMoneyActionCell(rowPanel, 340, 70, row.Quantity.ToString("N0"), Color.White, null, row);
        CreateMoneyActionCell(rowPanel, 416, 72, FormatCoinCompact(row.CapitalCopper), Color.White, null, row);
        CreateMoneyActionCell(rowPanel, 494, 54, FormatCoinCompact(row.TargetCopper), Color.White, null, row);
        CreateMoneyActionCell(rowPanel, 554, 64, FormatSignedCoin(row.EdgeCopper), row.EdgeCopper >= 0 ? Color.Gold : Color.IndianRed, null, row);
        CreateMoneyActionCell(rowPanel, 624, 56, $"{row.ConfidenceScore:N0}", Color.LightGreen, null, row);
        CreateMoneyActionCell(rowPanel, 686, 136, TrimLabel(row.Notes, 24), Color.LightGray, tooltip, row);
    }

    private void CreateLedgerCell(Container parent, int x, int width, string text, Color textColor, string tooltip, TransactionLedgerEntry entry) {
        var label = new Label() {
            Parent = parent,
            Location = new Point(x + 6, 1),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };

        if (!string.IsNullOrWhiteSpace(tooltip)) {
            label.BasicTooltipText = tooltip;
        }

        AttachLedgerSelection(label, entry);
    }

    private void CreateAdvisorCell(Container parent, int x, int width, string text, Color textColor, string tooltip, AdvisorSuggestion suggestion) {
        var label = new Label() {
            Parent = parent,
            Location = new Point(x + 6, 1),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };

        if (!string.IsNullOrWhiteSpace(tooltip)) {
            label.BasicTooltipText = tooltip;
        }

        AttachAdvisorSelection(label, suggestion);
    }

    private void CreateMoneyActionCell(Container parent, int x, int width, string text, Color textColor, string tooltip, MoneyActionRow row) {
        var label = new Label() {
            Parent = parent,
            Location = new Point(x + 6, 1),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };

        if (!string.IsNullOrWhiteSpace(tooltip)) {
            label.BasicTooltipText = tooltip;
        }

        AttachMoneyActionSelection(label, row);
    }

    private void CreateEmptyState(string text = "No candidates matched the current filters. Try lowering min profit, depth, or practical filtering.", int y = 10) {
        var label = new Label() {
            Parent = _listPanel,
            Location = new Point(12, y),
            Size = new Point(Math.Max(220, _listPanel.Width - 24), 24),
            Text = text,
            ShowShadow = true,
            TextColor = Color.LightGray
        };

        _dynamicControls.Add(label);
    }

    private int CreatePortfolioOverview(PortfolioSnapshot snapshot) {
        var summary = snapshot?.Summary ?? new PortfolioSummary();
        var contentWidth = Math.Max(320, _listPanel.Width - 24);
        var gap = 10;

        CreateSectionLabel(_listPanel, "Portfolio Board", new Point(8, 4), Math.Max(220, contentWidth - 16), new Color(231, 208, 144));
        CreateSectionLabel(_listPanel, summary.IsAuthenticated
            ? "Current status, historical wins, and bad investments from authenticated account data."
            : "Partial account picture. Add wallet, inventories, trading post, and characters scopes for the full view.", new Point(8, 24), Math.Max(220, contentWidth - 16), new Color(197, 213, 190), 18);

        CreatePortfolioStatusPanel(8, 46, contentWidth - 8, 112, summary);

        var chartY = 168;
        var chartHeight = 152;
        CreatePortfolioTrendChart(snapshot, new Rectangle(8, chartY, contentWidth - 8, chartHeight));

        var historyY = chartY + chartHeight + 10;
        var historyHeight = 110;
        if (contentWidth >= 860) {
            var historyWidth = Math.Max(280, (contentWidth - gap - 8) / 2);
            CreateHistoricalResultsPanel(8, historyY, historyWidth, historyHeight, "Historically Good Flips / Sells", GetBestHistoricalResults(snapshot), new Color(103, 192, 122));
            CreateHistoricalResultsPanel(8 + historyWidth + gap, historyY, historyWidth, historyHeight, "Bad Investments To Review", GetWorstHistoricalResults(snapshot), new Color(210, 145, 72));
        } else {
            CreateHistoricalResultsPanel(8, historyY, contentWidth - 8, historyHeight, "Historically Good Flips / Sells", GetBestHistoricalResults(snapshot), new Color(103, 192, 122));
            historyY += historyHeight + 10;
            CreateHistoricalResultsPanel(8, historyY, contentWidth - 8, historyHeight, "Bad Investments To Review", GetWorstHistoricalResults(snapshot), new Color(210, 145, 72));
        }

        var positionsY = historyY + historyHeight + 24;
        CreateSectionLabel(_listPanel, "Current Exposure", new Point(8, positionsY), Math.Max(220, contentWidth - 16), new Color(231, 208, 144));
        CreateSectionLabel(_listPanel, "Open buys are tied capital, open sells are expected post-fee exits, held rows are inventory still at risk.", new Point(8, positionsY + 18), Math.Max(220, contentWidth - 16), new Color(175, 187, 198), 16);
        CreatePortfolioTableHeader(positionsY + 38);
        return positionsY + 66;
    }

    private void CreatePortfolioSummaryCard(int x, int y, int width, int height, string title, string value, string subtitle, Color accentColor, Color backgroundColor) {
        var panel = new Panel() {
            Parent = _listPanel,
            Location = new Point(x, y),
            Size = new Point(width, height),
            BackgroundColor = backgroundColor,
            ShowBorder = true
        };
        _dynamicControls.Add(panel);
        CreateRowAccent(panel, accentColor);

        var titleLabel = new Label() {
            Parent = panel,
            Location = new Point(12, 4),
            Size = new Point(width - 18, 16),
            Text = title,
            ShowShadow = true,
            TextColor = new Color(198, 214, 226)
        };
        _dynamicControls.Add(titleLabel);

        var valueLabel = new Label() {
            Parent = panel,
            Location = new Point(12, 18),
            Size = new Point(width - 18, 16),
            Text = value,
            ShowShadow = true,
            TextColor = Color.White
        };
        _dynamicControls.Add(valueLabel);

        var subtitleLabel = new Label() {
            Parent = panel,
            Location = new Point(12, 32),
            Size = new Point(width - 18, 12),
            Text = TrimLabel(subtitle, 40),
            ShowShadow = true,
            TextColor = Color.LightGray
        };
        _dynamicControls.Add(subtitleLabel);
    }

    private void CreatePortfolioStatusPanel(int x, int y, int width, int height, PortfolioSummary summary) {
        var panel = new Panel() {
            Parent = _listPanel,
            Location = new Point(x, y),
            Size = new Point(width, height),
            BackgroundColor = new Color(18, 27, 36, 226),
            ShowBorder = true
        };
        _dynamicControls.Add(panel);
        CreateRowAccent(panel, new Color(197, 161, 82));

        CreateSectionLabel(panel, "Current Status", new Point(12, 6), width - 24, new Color(231, 208, 144));

        var columnGap = 6;
        var columnWidth = Math.Max(120, (width - 24 - (columnGap * 2)) / 3);
        CreatePortfolioStatusColumn(
            panel,
            12,
            30,
            columnWidth,
            "Capital",
            $"Net worth {FormatCoin(summary.NetWorthCopper)}",
            $"Wallet {FormatCoin(summary.WalletCopper)}",
            $"1d {FormatSignedCoin(summary.DailyDeltaCopper)} | 7d {FormatSignedCoin(summary.WeeklyDeltaCopper)} | 30d {FormatSignedCoin(summary.MonthlyDeltaCopper)}",
            new Color(104, 156, 196));
        CreatePortfolioStatusColumn(
            panel,
            12 + columnWidth + columnGap,
            30,
            columnWidth,
            "Exposure",
            $"Buy orders {FormatCoin(summary.OutstandingBuyCopper)}",
            $"Sell exits {FormatCoin(summary.OutstandingSellNetCopper)}",
            $"Held inventory {FormatCoin(summary.HoldingsValueCopper)}",
            new Color(99, 162, 111));
        CreatePortfolioStatusColumn(
            panel,
            12 + ((columnWidth + columnGap) * 2),
            30,
            columnWidth,
            "Performance",
            $"Realized {FormatCoin(summary.RealizedProfitCopper)}",
            $"Open P/L {FormatCoin(summary.UnrealizedProfitCopper)}",
            BuildPortfolioStatusVerdict(summary),
            new Color(156, 124, 200));
    }

    private void CreatePortfolioStatusColumn(Container parent, int x, int y, int width, string title, string line1, string line2, string line3, Color accentColor) {
        CreateSectionLabel(parent, title, new Point(x, y), width, accentColor, 16);
        CreateSectionLabel(parent, TrimLabel(line1, 44), new Point(x, y + 16), width, Color.White, 16);
        CreateSectionLabel(parent, TrimLabel(line2, 44), new Point(x, y + 32), width, new Color(205, 212, 220), 16);
        CreateSectionLabel(parent, TrimLabel(line3, 44), new Point(x, y + 48), width, new Color(175, 187, 198), 16);
    }

    private void CreatePortfolioGuidePanel(int x, int y, int width, int height, PortfolioSummary summary) {
        var panel = new Panel() {
            Parent = _listPanel,
            Location = new Point(x, y),
            Size = new Point(width, height),
            BackgroundColor = new Color(24, 24, 24, 220),
            ShowBorder = true
        };
        _dynamicControls.Add(panel);
        CreateRowAccent(panel, new Color(143, 117, 69));

        var headline = new Label() {
            Parent = panel,
            Location = new Point(12, 4),
            Size = new Point(width - 18, 16),
            Text = "How To Read This",
            ShowShadow = true,
            TextColor = new Color(231, 208, 144)
        };
        _dynamicControls.Add(headline);

        var message = new Label() {
            Parent = panel,
            Location = new Point(12, 20),
            Size = new Point(width - 18, 20),
            Text = summary.IsAuthenticated
                ? "Cheap MV% means the position is below its local fair value. Hot means it is expensive now. Buy rows tie up gold; Sell rows should release gold."
                : "This view is partial until the account sync can see wallet, inventory, and TP orders.",
            WrapText = true,
            ShowShadow = true,
            TextColor = new Color(205, 212, 220)
        };
        _dynamicControls.Add(message);
    }

    private void CreateHistoricalResultsPanel(int x, int y, int width, int height, string title, IReadOnlyList<HistoricalInvestmentResult> results, Color accentColor) {
        var panel = new Panel() {
            Parent = _listPanel,
            Location = new Point(x, y),
            Size = new Point(width, height),
            BackgroundColor = new Color(22, 30, 38, 224),
            ShowBorder = true
        };
        _dynamicControls.Add(panel);
        CreateRowAccent(panel, accentColor);

        var titleLabel = new Label() {
            Parent = panel,
            Location = new Point(12, 4),
            Size = new Point(width - 18, 16),
            Text = title,
            ShowShadow = true,
            TextColor = new Color(231, 208, 144)
        };
        _dynamicControls.Add(titleLabel);

        if (results == null || results.Count == 0) {
            var emptyLabel = new Label() {
                Parent = panel,
                Location = new Point(12, 24),
                Size = new Point(width - 18, 34),
                Text = title.IndexOf("Bad", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "No bad outcomes recorded yet."
                    : "No profitable exits recorded yet.",
                WrapText = true,
                ShowShadow = true,
                TextColor = Color.LightGray
            };
            _dynamicControls.Add(emptyLabel);
            return;
        }

        var rowY = 24;
        foreach (var result in results.Take(4)) {
            var line = new Label() {
                Parent = panel,
                Location = new Point(12, rowY),
                Size = new Point(width - 18, 16),
                Text = TrimLabel($"{result.ItemName}: {FormatSignedCoin(result.TotalProfitCopper)} | {result.ReturnOnCapitalPercent:N1}% | {result.Verdict}", 72),
                ShowShadow = true,
                TextColor = result.TotalProfitCopper >= 0 ? Color.LightGreen : Color.IndianRed,
                BasicTooltipText = BuildHistoricalResultTooltip(result)
            };
            _dynamicControls.Add(line);
            rowY += 18;
        }
    }

    private void CreatePortfolioTrendChart(PortfolioSnapshot snapshot, Rectangle bounds) {
        var chartPanel = new Panel() {
            Parent = _listPanel,
            Location = new Point(bounds.X, bounds.Y),
            Size = new Point(bounds.Width, bounds.Height),
            BackgroundColor = new Color(18, 27, 36, 228),
            ShowBorder = true
        };
        _dynamicControls.Add(chartPanel);
        CreateRowAccent(chartPanel, new Color(197, 161, 82));

        var titleLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(12, 6),
            Size = new Point(Math.Max(120, bounds.Width - 226), 18),
            Text = "Portfolio Growth",
            ShowShadow = true,
            TextColor = new Color(231, 208, 144)
        };
        _dynamicControls.Add(titleLabel);

        CreatePortfolioGrowthPeriodButton(chartPanel, bounds.Width - 202, PortfolioGrowthPeriod.Day, "Days");
        CreatePortfolioGrowthPeriodButton(chartPanel, bounds.Width - 138, PortfolioGrowthPeriod.Week, "Weeks");
        CreatePortfolioGrowthPeriodButton(chartPanel, bounds.Width - 70, PortfolioGrowthPeriod.Month, "Months");

        var subtitleLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(12, 22),
            Size = new Point(bounds.Width - 24, 16),
            Text = "Period change from saved net worth snapshots",
            ShowShadow = true,
            TextColor = new Color(193, 201, 209)
        };
        _dynamicControls.Add(subtitleLabel);

        var trend = NormalizePortfolioTrend(snapshot?.Trend);
        if (trend.Count < 2) {
            var emptyLabel = new Label() {
                Parent = chartPanel,
                Location = new Point(12, 58),
                Size = new Point(bounds.Width - 24, 36),
                Text = "No saved growth points yet. Keep scanning over time to build daily, weekly, and monthly bars.",
                WrapText = true,
                ShowShadow = true,
                TextColor = Color.LightGray
            };
            _dynamicControls.Add(emptyLabel);
            return;
        }

        var buckets = BuildPortfolioGrowthBuckets(trend, _portfolioGrowthPeriod);
        buckets = buckets.Skip(Math.Max(0, buckets.Count - GetPortfolioGrowthBucketLimit(_portfolioGrowthPeriod))).ToList();

        if (buckets.Count == 0) {
            var emptyLabel = new Label() {
                Parent = chartPanel,
                Location = new Point(12, 58),
                Size = new Point(bounds.Width - 24, 36),
                Text = $"Need at least two snapshots in the selected {GetPortfolioGrowthPeriodPlural(_portfolioGrowthPeriod).ToLowerInvariant()} view.",
                WrapText = true,
                ShowShadow = true,
                TextColor = Color.LightGray
            };
            _dynamicControls.Add(emptyLabel);
            return;
        }

        var maxGain = Math.Max(0, buckets.Max(bucket => bucket.DeltaCopper));
        var maxLoss = Math.Abs(Math.Min(0, buckets.Min(bucket => bucket.DeltaCopper)));
        var maxMagnitude = Math.Max(1, Math.Max(maxGain, maxLoss));
        var displayedGrowth = buckets.Sum(bucket => bucket.DeltaCopper);
        var chartInnerX = 12;
        var chartInnerY = 58;
        var chartInnerWidth = Math.Max(120, bounds.Width - 24);
        var chartInnerHeight = Math.Max(48, bounds.Height - 96);
        var baselineY = chartInnerY + (chartInnerHeight / 2);

        for (var grid = 0; grid < 3; grid++) {
            var y = chartInnerY + (grid * chartInnerHeight / 2);
            var gridLine = new Panel() {
                Parent = chartPanel,
                Location = new Point(chartInnerX, y),
                Size = new Point(chartInnerWidth, 1),
                BackgroundColor = new Color(84, 76, 63, 180),
                ShowBorder = false
            };
            _dynamicControls.Add(gridLine);
        }

        var baseline = new Panel() {
            Parent = chartPanel,
            Location = new Point(chartInnerX, baselineY),
            Size = new Point(chartInnerWidth, 2),
            BackgroundColor = new Color(197, 161, 82, 220),
            ShowBorder = false
        };
        _dynamicControls.Add(baseline);

        var barGap = buckets.Count > 18 ? 2 : 4;
        var barWidth = Math.Max(4, (chartInnerWidth - (barGap * Math.Max(0, buckets.Count - 1))) / Math.Max(1, buckets.Count));
        var halfHeight = Math.Max(12, (chartInnerHeight / 2) - 6);

        for (var i = 0; i < buckets.Count; i++) {
            var bucket = buckets[i];
            var isFlat = bucket.DeltaCopper == 0;
            var barHeight = isFlat
                ? 2
                : Math.Max(4, (int)Math.Round(Math.Abs(bucket.DeltaCopper) / (decimal)maxMagnitude * halfHeight));
            var x = chartInnerX + (i * (barWidth + barGap));
            var y = bucket.DeltaCopper >= 0
                ? baselineY - barHeight
                : baselineY + 2;
            var barColor = bucket.DeltaCopper > 0
                ? new Color(103, 192, 122, 230)
                : bucket.DeltaCopper < 0
                    ? new Color(205, 82, 82, 230)
                    : new Color(142, 151, 160, 210);
            var pointControl = new Panel() {
                Parent = chartPanel,
                Location = new Point(x, y),
                Size = new Point(barWidth, barHeight),
                BackgroundColor = i == buckets.Count - 1 && bucket.DeltaCopper >= 0
                    ? new Color(214, 187, 98, 245)
                    : barColor,
                ShowBorder = false
            };
            pointControl.BasicTooltipText =
                $"{bucket.TooltipLabel}\n" +
                $"Growth {FormatSignedCoin(bucket.DeltaCopper)}\n" +
                $"Close {FormatCoin(bucket.CloseNetWorthCopper)}";
            _dynamicControls.Add(pointControl);
        }

        CreateSectionLabel(chartPanel, $"Best {FormatSignedCoin(maxGain)} | Worst {FormatSignedCoin(-maxLoss)}", new Point(12, 38), bounds.Width - 24, new Color(205, 212, 220), 16);
        CreateSectionLabel(chartPanel, $"{GetPortfolioGrowthPeriodPlural(_portfolioGrowthPeriod)} total {FormatSignedCoin(displayedGrowth)} | Current {FormatCoin(trend.Last().NetWorthCopper)}", new Point(12, bounds.Height - 34), bounds.Width - 24, new Color(231, 208, 144), 16);
        CreateSectionLabel(chartPanel, buckets.First().Label, new Point(12, bounds.Height - 18), 90, new Color(172, 182, 194), 16);
        CreateSectionLabel(chartPanel, buckets.Last().Label, new Point(Math.Max(100, bounds.Width - 94), bounds.Height - 18), 82, new Color(172, 182, 194), 16);
    }

    private void CreatePortfolioGrowthPeriodButton(Container parent, int x, PortfolioGrowthPeriod period, string text) {
        var button = new StandardButton() {
            Parent = parent,
            Location = new Point(Math.Max(12, x), 6),
            Size = new Point(58, 24),
            Text = text,
            Enabled = _portfolioGrowthPeriod != period
        };
        button.Click += (_, __) => {
            _portfolioGrowthPeriod = period;
            RenderPortfolio(_lastRenderedPortfolioSnapshot, _lastAccountSnapshot);
        };
        _dynamicControls.Add(button);
    }

    private static List<PortfolioTrendPoint> NormalizePortfolioTrend(IReadOnlyList<PortfolioTrendPoint> trend) {
        return (trend ?? new List<PortfolioTrendPoint>())
            .Where(point => point != null)
            .GroupBy(point => point.CapturedAtUtc)
            .Select(group => group.Last())
            .OrderBy(point => point.CapturedAtUtc)
            .ToList();
    }

    private static List<PortfolioGrowthBucket> BuildPortfolioGrowthBuckets(IReadOnlyList<PortfolioTrendPoint> trend, PortfolioGrowthPeriod period) {
        var grouped = trend
            .GroupBy(point => GetPortfolioGrowthBucketStart(point.CapturedAtUtc.LocalDateTime, period))
            .OrderBy(group => group.Key)
            .Select(group => {
                var points = group.OrderBy(point => point.CapturedAtUtc).ToList();
                return new PortfolioGrowthBucket() {
                    StartLocal = group.Key,
                    OpenNetWorthCopper = points.First().NetWorthCopper,
                    CloseNetWorthCopper = points.Last().NetWorthCopper
                };
            })
            .ToList();

        var previousClose = (int?) null;
        foreach (var bucket in grouped) {
            bucket.DeltaCopper = previousClose.HasValue
                ? bucket.CloseNetWorthCopper - previousClose.Value
                : bucket.CloseNetWorthCopper - bucket.OpenNetWorthCopper;
            bucket.Label = FormatPortfolioGrowthBucketLabel(bucket.StartLocal, period);
            bucket.TooltipLabel = FormatPortfolioGrowthBucketTooltip(bucket.StartLocal, period);
            previousClose = bucket.CloseNetWorthCopper;
        }

        return grouped;
    }

    private static DateTime GetPortfolioGrowthBucketStart(DateTime localTime, PortfolioGrowthPeriod period) {
        var date = localTime.Date;

        if (period == PortfolioGrowthPeriod.Month) {
            return new DateTime(date.Year, date.Month, 1);
        }

        if (period == PortfolioGrowthPeriod.Week) {
            var daysSinceMonday = ((int) date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysSinceMonday);
        }

        return date;
    }

    private static string FormatPortfolioGrowthBucketLabel(DateTime bucketStart, PortfolioGrowthPeriod period) {
        if (period == PortfolioGrowthPeriod.Month) {
            return bucketStart.ToString("MMM yy");
        }

        if (period == PortfolioGrowthPeriod.Week) {
            return $"W {bucketStart:MM-dd}";
        }

        return bucketStart.ToString("MM-dd");
    }

    private static string FormatPortfolioGrowthBucketTooltip(DateTime bucketStart, PortfolioGrowthPeriod period) {
        if (period == PortfolioGrowthPeriod.Month) {
            return bucketStart.ToString("MMMM yyyy");
        }

        if (period == PortfolioGrowthPeriod.Week) {
            return $"Week of {bucketStart:yyyy-MM-dd}";
        }

        return bucketStart.ToString("yyyy-MM-dd");
    }

    private static int GetPortfolioGrowthBucketLimit(PortfolioGrowthPeriod period) {
        if (period == PortfolioGrowthPeriod.Month) {
            return 12;
        }

        if (period == PortfolioGrowthPeriod.Week) {
            return 12;
        }

        return 30;
    }

    private static string GetPortfolioGrowthPeriodPlural(PortfolioGrowthPeriod period) {
        if (period == PortfolioGrowthPeriod.Month) {
            return "Months";
        }

        if (period == PortfolioGrowthPeriod.Week) {
            return "Weeks";
        }

        return "Days";
    }

    private void CreateItemPriceHistoryChart(FlipCandidate candidate) {
        CreateItemPriceHistoryChart(candidate?.ItemName, candidate?.PriceHistory);
    }

    private void CreateItemPriceHistoryChart(string itemName, IReadOnlyList<PriceSnapshotEntry> priceHistory) {
        var width = Math.Max(180, _detailPanel.Width - 30);
        var height = 128;
        var chartPanel = new Panel() {
            Parent = _detailPanel,
            Location = new Point(12, 500),
            Size = new Point(width, height),
            BackgroundColor = new Color(18, 27, 36, 228),
            ShowBorder = true
        };
        _detailGraphControls.Add(chartPanel);

        var titleLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(10, 6),
            Size = new Point(width - 20, 18),
            Text = "Local Sell History",
            ShowShadow = true,
            TextColor = new Color(231, 208, 144)
        };
        _detailGraphControls.Add(titleLabel);

        var allPoints = priceHistory ?? new List<PriceSnapshotEntry>();
        var orderedPoints = allPoints
            .Where(point => point.LowestSell > 0)
            .OrderBy(point => point.RecordedAtUtc)
            .ToList();
        var points = orderedPoints
            .Skip(Math.Max(0, orderedPoints.Count - 24))
            .ToList();

        if (points.Count == 0) {
            var emptyLabel = new Label() {
                Parent = chartPanel,
                Location = new Point(10, 36),
                Size = new Point(width - 20, 48),
                Text = "Run Full Scan over time to build a local price graph for this item.",
                WrapText = true,
                ShowShadow = true,
                TextColor = Color.LightGray
            };
            _detailGraphControls.Add(emptyLabel);
            return;
        }

        var minSell = points.Min(point => point.LowestSell);
        var maxSell = points.Max(point => point.LowestSell);
        var firstSell = points.First().LowestSell;
        var lastSell = points.Last().LowestSell;
        var deltaColor = lastSell >= firstSell ? Color.LightGreen : Color.IndianRed;
        var chartX = 10;
        var chartY = 38;
        var chartWidth = Math.Max(120, width - 20);
        var chartHeight = 58;

        for (var grid = 0; grid < 3; grid++) {
            var y = chartY + (grid * chartHeight / 2);
            var gridLine = new Panel() {
                Parent = chartPanel,
                Location = new Point(chartX, y),
                Size = new Point(chartWidth, 1),
                BackgroundColor = new Color(74, 85, 96, 155),
                ShowBorder = false
            };
            _detailGraphControls.Add(gridLine);
        }

        var gap = points.Count > 16 ? 2 : 4;
        var barWidth = Math.Max(4, (chartWidth - (gap * Math.Max(0, points.Count - 1))) / Math.Max(1, points.Count));

        for (var i = 0; i < points.Count; i++) {
            var point = points[i];
            var normalized = maxSell == minSell ? 0.5m : (point.LowestSell - minSell) / (decimal)Math.Max(1, maxSell - minSell);
            var barHeight = Math.Max(4, (int)Math.Round(normalized * (chartHeight - 6)) + 4);
            var x = chartX + (i * (barWidth + gap));
            var y = chartY + chartHeight - barHeight;
            var isLatest = i == points.Count - 1;
            var bar = new Panel() {
                Parent = chartPanel,
                Location = new Point(x, y),
                Size = new Point(barWidth, barHeight),
                BackgroundColor = isLatest ? new Color(231, 208, 144, 245) : new Color(103, 192, 122, 210),
                ShowBorder = false,
                BasicTooltipText = $"{point.RecordedAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}\nSell {FormatCoin(point.LowestSell)}\nBuy {FormatCoin(point.HighestBuy)}\nDepth {Math.Min(point.BuyQuantity, point.SellQuantity):N0}"
            };
            _detailGraphControls.Add(bar);
        }

        var summaryLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(10, 22),
            Size = new Point(width - 20, 16),
            Text = $"{points.Count} samples | Low {FormatCoinCompact(minSell)} | High {FormatCoinCompact(maxSell)} | Now {FormatCoinCompact(lastSell)}",
            ShowShadow = true,
            TextColor = deltaColor
        };
        _detailGraphControls.Add(summaryLabel);

        var startLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(10, height - 22),
            Size = new Point(98, 16),
            Text = points.First().RecordedAtUtc.LocalDateTime.ToString("MM-dd HH:mm"),
            ShowShadow = true,
            TextColor = new Color(172, 182, 194)
        };
        _detailGraphControls.Add(startLabel);

        var endLabel = new Label() {
            Parent = chartPanel,
            Location = new Point(Math.Max(110, width - 100), height - 22),
            Size = new Point(90, 16),
            Text = points.Last().RecordedAtUtc.LocalDateTime.ToString("MM-dd HH:mm"),
            ShowShadow = true,
            TextColor = new Color(172, 182, 194)
        };
        _detailGraphControls.Add(endLabel);
    }

    private void CreatePortfolioTableHeader(int y) {
        CreateSectionLabel(_listPanel, "#", new Point(12, y), 24, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Item / Position", new Point(42, y), 228, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Qty", new Point(276, y), 70, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Unit", new Point(352, y), 70, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Value", new Point(428, y), 72, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "MV%", new Point(506, y), 54, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Type", new Point(566, y), 64, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Fair", new Point(636, y), 56, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Signal", new Point(698, y), 60, new Color(205, 212, 220));
        CreateSectionLabel(_listPanel, "Read", new Point(764, y), 70, new Color(205, 212, 220));
    }

    private void CreateSectionLabel(Container parent, string text, Point location, int width, Color textColor, int height = 18) {
        var label = new Label() {
            Parent = parent,
            Location = location,
            Size = new Point(width, height),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };
        _dynamicControls.Add(label);
    }

    private void CreateChartLineSegment(Container parent, Point from, Point to, Color color) {
        var left = Math.Min(from.X, to.X);
        var top = Math.Min(from.Y, to.Y);
        var width = Math.Max(2, Math.Abs(to.X - from.X) + 2);
        var height = Math.Max(2, Math.Abs(to.Y - from.Y) + 2);
        var segment = new Panel() {
            Parent = parent,
            Location = new Point(left, top),
            Size = new Point(width, height),
            BackgroundColor = color,
            ShowBorder = false
        };
        _dynamicControls.Add(segment);
    }

    private void CreateRowAccent(Container parent, Color accentColor) {
        var accent = new Panel() {
            Parent = parent,
            Location = new Point(0, 0),
            Size = new Point(6, parent.Height),
            BackgroundColor = accentColor,
            ShowBorder = false
        };
        _dynamicControls.Add(accent);
    }

    private void AttachSelection(Control control, FlipCandidate candidate) {
        control.Click += (_, __) => SelectCandidate(candidate);
    }

    private void AttachLedgerSelection(Control control, TransactionLedgerEntry entry) {
        control.Click += (_, __) => SelectLedgerEntry(entry);
    }

    private void AttachAdvisorSelection(Control control, AdvisorSuggestion suggestion) {
        control.Click += (_, __) => SelectAdvisorSuggestion(suggestion);
    }

    private void AttachPortfolioSelection(Control control, PortfolioRow row) {
        control.Click += (_, __) => SelectPortfolioRow(row);
    }

    private void AttachMoneyActionSelection(Control control, MoneyActionRow row) {
        control.Click += (_, __) => SelectMoneyAction(row);
    }

    private void SelectCandidate(FlipCandidate candidate) {
        if (candidate == null) {
            return;
        }

        _selectedItemId = candidate.ItemId;

        if (_lastRenderedResult != null) {
            RenderRows(_lastRenderedResult, _lastRenderedResult.GeneratedAtUtc.LocalDateTime, _currentViewMode, _lastAccountSnapshot);
        } else {
            UpdateDetailPanel();
        }
    }

    private void SelectLedgerEntry(TransactionLedgerEntry entry) {
        if (entry == null) {
            return;
        }

        _selectedItemId = entry.ItemId;

        if (_lastRenderedLedgerEntries != null && _lastRenderedLedgerEntries.Count > 0) {
            RenderLedgerRows(_lastRenderedLedgerEntries, _lastAccountSnapshot.CapturedAtUtc.LocalDateTime, _lastAccountSnapshot);
        } else {
            UpdateDetailPanel();
        }
    }

    private void SelectAdvisorSuggestion(AdvisorSuggestion suggestion) {
        if (suggestion == null) {
            return;
        }

        _selectedItemId = suggestion.ItemId;

        if (_lastRenderedAdvisorBriefing != null) {
            RenderAdvisor(_lastRenderedAdvisorBriefing, _lastRenderedAdvisorBriefing.GeneratedAtUtc.LocalDateTime, _lastAccountSnapshot, _lastRenderedPortfolioSnapshot);
        } else {
            UpdateDetailPanel();
        }
    }

    private void SelectPortfolioRow(PortfolioRow row) {
        if (row == null) {
            return;
        }

        _selectedPortfolioKey = GetPortfolioKey(row);
        _selectedItemId = row.ItemId > 0 ? row.ItemId : null;

        if (_lastRenderedPortfolioSnapshot?.Rows?.Count > 0) {
            RenderPortfolio(_lastRenderedPortfolioSnapshot, _lastAccountSnapshot);
        } else {
            UpdateDetailPanel();
        }
    }

    private void SelectMoneyAction(MoneyActionRow row) {
        if (row == null) {
            return;
        }

        _selectedMoneyActionKey = GetMoneyActionKey(row);
        _selectedItemId = row.ItemId > 0 ? row.ItemId : null;

        if (_lastRenderedMoneyActionRows != null && _lastRenderedMoneyActionRows.Count > 0) {
            RenderMoneyActions(_lastRenderedMoneyActionRows, _lastAccountSnapshot.CapturedAtUtc.LocalDateTime, _currentViewMode, _lastAccountSnapshot);
        } else {
            UpdateDetailPanel();
        }
    }

    private void UpdateDetailPanel() {
        ClearDetailGraph();
        SetDetailTrendLayout(false);
        var ledgerEntry = GetSelectedLedgerEntry();
        var candidate = GetSelectedCandidate();
        var advisorSuggestion = GetSelectedAdvisorSuggestion();
        var portfolioRow = GetSelectedPortfolioRow();
        var moneyAction = GetSelectedMoneyAction();
        var hasSelection = _currentViewMode == OverlayViewMode.Ledger
            ? ledgerEntry != null
            : (_currentViewMode == OverlayViewMode.Advisor
                ? advisorSuggestion != null
                : (_currentViewMode == OverlayViewMode.Portfolio
                    ? portfolioRow != null
                    : (IsMoneyActionView(_currentViewMode) ? moneyAction != null : candidate != null)));

        _copyNameButton.Enabled = candidate != null || advisorSuggestion != null || portfolioRow != null || moneyAction != null;
        _copyWikiButton.Enabled = candidate != null || advisorSuggestion != null || portfolioRow != null || moneyAction != null;
        _openWikiButton.Enabled = candidate != null || advisorSuggestion != null || portfolioRow != null || moneyAction != null;
        _watchButton.Enabled = hasSelection;

        if (!hasSelection) {
            _detailTitleLabel.Text = "Select a row";
            _detailSubtitleLabel.Text = "Quick TP workflow and market stats";
            _detailStatsLabel.Text = "The right panel will show fast-flip stats, turnover clues, and quick actions for the selected item.";
            _detailTrendLabel.Text = "Trend data appears after at least one saved market snapshot.";
            _watchButton.Text = "Watch";
            ResetDetailScroll();
            return;
        }

        if (_currentViewMode == OverlayViewMode.Ledger && ledgerEntry != null) {
            _detailTitleLabel.Text = ledgerEntry.ItemName;
            _detailSubtitleLabel.Text = $"Ledger | Held {ledgerEntry.HeldQuantity:N0} | Recommendation {FormatRecommendation(ledgerEntry.RecommendationState)}";
            _watchButton.Text = _watchlistItemIds.Contains(ledgerEntry.ItemId) ? "Unwatch" : "Watch";
            _detailStatsLabel.Text = BuildLedgerDetailStats(ledgerEntry);
            _detailTrendLabel.Text = BuildLedgerDetailTrend(ledgerEntry);
            ResetDetailScroll();
            return;
        }

        if (_currentViewMode == OverlayViewMode.Advisor && advisorSuggestion != null) {
            _detailTitleLabel.Text = advisorSuggestion.ItemName;
            _detailSubtitleLabel.Text = $"{FormatAction(advisorSuggestion.Action)} | {FormatStrategyTag(advisorSuggestion.StrategyTag)} | {GetModeLabel(advisorSuggestion.OpportunityMode)}";
            _watchButton.Text = _watchlistItemIds.Contains(advisorSuggestion.ItemId) ? "Unwatch" : "Watch";
            _detailHintLabel.Text = _lastRenderedAdvisorBriefing?.Summary ?? "The advisor summarizes current best actions after each scan.";
            _detailStatsLabel.Text = BuildAdvisorDetailStats(advisorSuggestion);
            SetDetailTrendLayout(true);
            CreateItemPriceHistoryChart(advisorSuggestion.ItemName, advisorSuggestion.PriceHistory);
            _detailTrendLabel.Text = BuildAdvisorDetailTrend(advisorSuggestion, _lastRenderedAdvisorBriefing);
            ResetDetailScroll();
            return;
        }

        if (_currentViewMode == OverlayViewMode.Portfolio && portfolioRow != null) {
            _detailTitleLabel.Text = portfolioRow.ItemName;
            _detailSubtitleLabel.Text = $"{FormatPortfolioKindLong(portfolioRow.Kind)} | {portfolioRow.MarketValueLabel}";
            _watchButton.Text = _watchlistItemIds.Contains(portfolioRow.ItemId) ? "Unwatch" : "Watch";
            _detailHintLabel.Text = _lastRenderedPortfolioSnapshot?.Summary?.StatusMessage ?? "Portfolio tracks wallet, outstanding orders, and conservative holdings value.";
            _detailStatsLabel.Text = BuildPortfolioDetailStats(portfolioRow, _lastRenderedPortfolioSnapshot?.Summary);
            _detailTrendLabel.Text = BuildPortfolioDetailTrend(portfolioRow, _lastRenderedPortfolioSnapshot);
            ResetDetailScroll();
            return;
        }

        if (IsMoneyActionView(_currentViewMode) && moneyAction != null) {
            _detailTitleLabel.Text = moneyAction.ItemName;
            _detailSubtitleLabel.Text = $"{moneyAction.Lane} | {moneyAction.Action} | Confidence {moneyAction.ConfidenceScore:N0}";
            _watchButton.Text = _watchlistItemIds.Contains(moneyAction.ItemId) ? "Unwatch" : "Watch";
            _detailHintLabel.Text = moneyAction.Notes;
            _detailStatsLabel.Text = BuildMoneyActionDetailStats(moneyAction);
            _detailTrendLabel.Text = BuildMoneyActionDetailTrend(moneyAction);
            ResetDetailScroll();
            return;
        }

        var isWatched = _watchlistItemIds.Contains(candidate.ItemId);
        _detailTitleLabel.Text = candidate.ItemName;
        _detailSubtitleLabel.Text = $"{GetModeLabel(candidate.OpportunityMode)} | {candidate.ItemType} | {candidate.Rarity} | {FormatRecommendation(candidate.RecommendationState)}";
        _watchButton.Text = isWatched ? "Unwatch" : "Watch";
        _detailHintLabel.Text = _currentViewMode == OverlayViewMode.Snipe
            ? BuildSnipeHint(candidate)
            : !string.IsNullOrWhiteSpace(candidate.AdvisorWhyNow)
            ? candidate.AdvisorWhyNow
            : candidate.RecommendationNote;
        _detailStatsLabel.Text = BuildDetailStats(candidate);
        SetDetailTrendLayout(true);
        CreateItemPriceHistoryChart(candidate);
        _detailTrendLabel.Text = BuildDetailTrend(candidate);
        ResetDetailScroll();
    }

    private void ToggleMinimized() {
        if (_isMinimized) {
            RestoreFromMiniPanel();
            return;
        }

        _expandedPanelSize = _panel.Size;
        _isMinimized = true;
        _suppressMiniRestoreClick = false;
        SetContentVisibility(false);
        _panel.Visible = false;
        _miniPanel.Location = ClampLocationToScreen(_panel.Location, _miniPanel.Width, _miniPanel.Height);
        _miniPanel.Visible = true;
        ApplyMiniLayout();
    }

    private void ApplyLayout() {
        var panelWidth = _panel.ContentRegion.Width;
        var panelHeight = _panel.ContentRegion.Height;
        var rightEdge = panelWidth - 12;
        var isPortfolioView = _currentViewMode == OverlayViewMode.Portfolio;

        _closeButton.Visible = false;
        _minimizeButton.Location = new Point(rightEdge - _minimizeButton.Width, 12);
        rightEdge = _minimizeButton.Left - 12;
        _fullScanButton.Location = new Point(rightEdge - _fullScanButton.Width, 12);
        rightEdge = _fullScanButton.Left - 8;
        _quickScanButton.Location = new Point(rightEdge - _quickScanButton.Width, 12);
        _scannerTabButton.Location = new Point(16, 12);
        _portfolioTabButton.Location = new Point(_scannerTabButton.Right + 8, 12);
        _snipeTabButton.Location = new Point(_portfolioTabButton.Right + 8, 12);
        _ordersTabButton.Location = new Point(_snipeTabButton.Right + 8, 12);
        _craftTabButton.Location = new Point(_ordersTabButton.Right + 8, 12);
        _inventoryTabButton.Location = new Point(_craftTabButton.Right + 8, 12);
        _subtitleLabel.Location = new Point(_inventoryTabButton.Right + 14, 12);
        _summaryLabel.Location = new Point(_inventoryTabButton.Right + 14, 30);
        _subtitleLabel.Size = new Point(Math.Max(140, _quickScanButton.Left - _subtitleLabel.Left - 20), 20);
        _summaryLabel.Size = new Point(Math.Max(160, _quickScanButton.Left - _summaryLabel.Left - 20), 18);
        _toolbarPanel.Location = new Point(14, 44);
        _toolbarPanel.Size = new Point(Math.Max(240, Math.Min(panelWidth - 28, _quickScanButton.Left - 22)), 38);
        _filterPanel.Location = new Point(14, 78);
        _filterPanel.Size = new Point(Math.Max(240, Math.Min(panelWidth - 28, _autoPlanButton.Right + 18)), 38);
        _footerPanel.Location = new Point(14, panelHeight - 68);
        _footerPanel.Size = new Point(Math.Max(240, panelWidth - 28), 46);

        var contentBottom = panelHeight - 70;
        var detailWidth = isPortfolioView ? 0 : Math.Max(250, Math.Min(390, (int)(panelWidth * 0.32f)));
        var detailTop = 168;
        var detailHeight = Math.Max(220, contentBottom - detailTop);
        var detailLeft = isPortfolioView ? panelWidth - 12 : panelWidth - detailWidth - 26;
        _detailPanel.Location = new Point(detailLeft, detailTop);
        _detailPanel.Size = new Point(detailWidth, detailHeight);

        _detailScrollbar.Location = new Point(detailLeft + detailWidth - 16, detailTop + 34);
        _detailScrollbar.Size = new Point(16, Math.Max(120, detailHeight - 42));

        var listLeft = 16;
        var listTop = isPortfolioView ? 166 : 194;
        var listHeight = Math.Max(120, contentBottom - listTop);
        var listWidth = isPortfolioView
            ? Math.Max(260, panelWidth - listLeft - 28)
            : Math.Max(260, detailLeft - listLeft - 24);
        _listPanel.Location = new Point(listLeft, listTop);
        _listPanel.Size = new Point(listWidth, listHeight);
        _listScrollbar.Location = new Point(listLeft + listWidth - 16, listTop + 4);
        _listScrollbar.Size = new Point(16, Math.Max(90, listHeight - 8));

        var detailTextWidth = detailWidth - 30;
        _detailTitleLabel.Size = new Point(detailTextWidth, 26);
        _detailSubtitleLabel.Size = new Point(detailTextWidth, 20);
        _detailHintLabel.Size = new Point(detailTextWidth, 64);
        _detailStatsLabel.Size = new Point(detailTextWidth, 240);
        _detailTrendLabel.Location = new Point(12, _detailGraphControls.Count > 0 ? 642 : 500);
        _detailTrendLabel.Size = new Point(detailTextWidth - 16, 128);
        _openWikiButton.Location = new Point(Math.Max(120, detailWidth - 108), 94);
        _watchButton.Location = new Point(Math.Max(12, detailWidth - 108), 128);

        _statusLabel.Location = new Point(16, 116);
        _statusLabel.Size = new Point(panelWidth - 28, 24);
        _portfolioStripLabel.Location = new Point(16, 138);
        _portfolioStripLabel.Size = new Point(panelWidth - 28, 20);
        _updatedLabel.Location = new Point(28, panelHeight - 62);
        _updatedLabel.Size = new Point(panelWidth - 92, 38);
    }

    private void ApplyMiniLayout() {
        _miniTitleLabel.Size = new Point(_miniPanel.Width - _miniRestoreButton.Width - 28, 20);
        _miniRestoreButton.Location = new Point(_miniPanel.Width - _miniRestoreButton.Width - 8, 6);
    }

    private void ResetDetailScroll() {
        _detailPanel.VerticalScrollOffset = 0;
    }

    private void HandleMiniPanelClick(object sender, EventArgs e) {
        if (_suppressMiniRestoreClick) {
            _suppressMiniRestoreClick = false;
            return;
        }

        RestoreFromMiniPanel();
    }

    private void RestoreFromMiniPanel() {
        _isMinimized = false;
        _miniPanel.Visible = false;
        _isDraggingMiniPanel = false;
        _suppressMiniRestoreClick = false;
        _panel.Location = ClampLocationToScreen(_miniPanel.Location, _expandedPanelSize.X, _expandedPanelSize.Y);
        _panel.Size = _expandedPanelSize;
        _panel.Show();
        SetContentVisibility(true);
        _minimizeButton.Text = "-";
        ApplyLayout();
    }

    private void SetContentVisibility(bool isVisible) {
        _toolbarPanel.Visible = isVisible;
        _filterPanel.Visible = isVisible;
        _footerPanel.Visible = isVisible;
        _scannerTabButton.Visible = isVisible;
        _portfolioTabButton.Visible = isVisible;
        _snipeTabButton.Visible = isVisible;
        _ordersTabButton.Visible = isVisible;
        _craftTabButton.Visible = isVisible;
        _inventoryTabButton.Visible = isVisible;
        _subtitleLabel.Visible = isVisible;
        _summaryLabel.Visible = isVisible;
        _modeButton.Visible = isVisible;
        _viewButton.Visible = isVisible;
        _presetButton.Visible = isVisible;
        _savePresetButton.Visible = isVisible;
        _sortButton.Visible = isVisible;
        _rowsButton.Visible = isVisible;
        _capButton.Visible = isVisible;
        _depthButton.Visible = isVisible;
        _discountButton.Visible = isVisible;
        _roiButton.Visible = isVisible;
        _ownedButton.Visible = isVisible;
        _openSellButton.Visible = isVisible;
        _volatilityButton.Visible = isVisible;
        _autoQuantityButton.Visible = isVisible;
        _autoPlanButton.Visible = isVisible;
        _profitDownButton.Visible = isVisible;
        _profitLabel.Visible = isVisible;
        _profitUpButton.Visible = isVisible;
        _practicalButton.Visible = isVisible;
        _quickScanButton.Visible = isVisible;
        _fullScanButton.Visible = isVisible;
        _minimizeButton.Visible = isVisible;
        _statusLabel.Visible = isVisible;
        _portfolioStripLabel.Visible = isVisible;
        _costHeaderLabel.Visible = isVisible;
        _listPanel.Visible = isVisible;
        _listScrollbar.Visible = isVisible;
        _detailPanel.Visible = isVisible;
        _detailScrollbar.Visible = isVisible;
        _updatedLabel.Visible = isVisible;
        _resizeHandle.Visible = false;
        _resizeGlyphLabel.Visible = false;

        foreach (var control in _dynamicControls) {
            control.Visible = isVisible;
        }

        foreach (var control in _detailGraphControls) {
            control.Visible = isVisible;
        }

        if (isVisible) {
            ApplyChromeForView(_currentViewMode);
        }
    }

    private void HandleMiniPanelMousePressed(object sender, Blish_HUD.Input.MouseEventArgs e) {
        var relativeMouse = _miniPanel.RelativeMousePosition;
        _suppressMiniRestoreClick = false;

        if (relativeMouse.X > _miniRestoreButton.Left - 8) {
            return;
        }

        _isDraggingMiniPanel = true;
        _dragMiniMouseOrigin = e.MousePosition;
        _dragMiniOrigin = _miniPanel.Location;
    }

    private void HandleMiniPanelMouseReleased(object sender, Blish_HUD.Input.MouseEventArgs e) {
        _isDraggingMiniPanel = false;
    }

    private void HandleMiniPanelMouseMoved(object sender, Blish_HUD.Input.MouseEventArgs e) {
        if (!_isDraggingMiniPanel) {
            return;
        }

        var delta = e.MousePosition - _dragMiniMouseOrigin;

        if (delta.X != 0 || delta.Y != 0) {
            _suppressMiniRestoreClick = true;
        }

        _miniPanel.Location = ClampLocationToScreen(_dragMiniOrigin + delta, _miniPanel.Width, _miniPanel.Height);
    }

    private FlipCandidate GetSelectedCandidate() {
        if (_currentViewMode == OverlayViewMode.Ledger || _currentViewMode == OverlayViewMode.Advisor || _currentViewMode == OverlayViewMode.Portfolio || IsMoneyActionView(_currentViewMode) || _lastRenderedResult == null || !_selectedItemId.HasValue) {
            return null;
        }

        foreach (var candidate in _lastRenderedResult.Candidates) {
            if (candidate.ItemId == _selectedItemId.Value) {
                return candidate;
            }
        }

        return null;
    }

    private TransactionLedgerEntry GetSelectedLedgerEntry() {
        if (_currentViewMode != OverlayViewMode.Ledger || _lastRenderedLedgerEntries == null || !_selectedItemId.HasValue) {
            return null;
        }

        return _lastRenderedLedgerEntries.FirstOrDefault(entry => entry.ItemId == _selectedItemId.Value);
    }

    private AdvisorSuggestion GetSelectedAdvisorSuggestion() {
        if (_currentViewMode != OverlayViewMode.Advisor || _lastRenderedAdvisorSuggestions == null || !_selectedItemId.HasValue) {
            return null;
        }

        return _lastRenderedAdvisorSuggestions.FirstOrDefault(suggestion => suggestion.ItemId == _selectedItemId.Value);
    }

    private PortfolioRow GetSelectedPortfolioRow() {
        if (_currentViewMode != OverlayViewMode.Portfolio || _lastRenderedPortfolioSnapshot?.Rows == null || string.IsNullOrWhiteSpace(_selectedPortfolioKey)) {
            return null;
        }

        return _lastRenderedPortfolioSnapshot.Rows.FirstOrDefault(row => string.Equals(GetPortfolioKey(row), _selectedPortfolioKey, StringComparison.Ordinal));
    }

    private MoneyActionRow GetSelectedMoneyAction() {
        if (!IsMoneyActionView(_currentViewMode) || _lastRenderedMoneyActionRows == null || string.IsNullOrWhiteSpace(_selectedMoneyActionKey)) {
            return null;
        }

        return _lastRenderedMoneyActionRows.FirstOrDefault(row => string.Equals(GetMoneyActionKey(row), _selectedMoneyActionKey, StringComparison.Ordinal));
    }

    private string GetSelectedItemName() {
        return GetSelectedCandidate()?.ItemName ?? GetSelectedAdvisorSuggestion()?.ItemName ?? GetSelectedPortfolioRow()?.ItemName ?? GetSelectedMoneyAction()?.ItemName;
    }

    private async System.Threading.Tasks.Task CopySelectedTextAsync(string text, string successMessage) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        await ClipboardUtil.WindowsClipboardService.SetTextAsync(text);
        SetStatus(successMessage);
    }

    private async System.Threading.Tasks.Task CopyAutoFlipPlanAsync() {
        if (_lastRenderedResult?.Candidates == null || _lastRenderedResult.Candidates.Count == 0) {
            SetStatus("Run or open a market, watchlist, or snipe scan before copying an auto flip plan.");
            return;
        }

        var quantity = Math.Max(1, _autoFlipQuantity);
        var planRows = _lastRenderedResult.Candidates
            .Where(candidate => candidate != null)
            .Take(10)
            .Select((candidate, index) => BuildAutoFlipPlanRow(candidate, index + 1, quantity))
            .Where(row => row != null)
            .ToList();

        if (planRows.Count == 0) {
            SetStatus("No safe top-10 buy-order rows are available at the current prices.");
            return;
        }

        var totalCapital = planRows.Sum(row => row.CapitalCopper);
        var totalProfit = planRows.Sum(row => row.EstimatedProfitCopper);
        var builder = new StringBuilder();
        builder.AppendLine("GW2 Flip Overlay - Manual Auto Flip Plan");
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Quantity per item: {quantity:N0}");
        builder.AppendLine("Note: GW2 API and Blish HUD cannot place Trading Post orders for you. Use this as a manual buy-order checklist.");
        builder.AppendLine();
        builder.AppendLine("Rank\tItem\tQty\tBid each\tCapital\tEst net sell\tEst profit\tROI\tRead");

        foreach (var row in planRows) {
            builder.AppendLine(
                $"{row.Rank}\t{row.ItemName}\t{quantity:N0}\t{FormatCoin(row.TargetBidCopper)}\t{FormatCoinLong(row.CapitalCopper)}\t{FormatCoin(row.NetSellCopper)}\t{FormatCoinLong(row.EstimatedProfitCopper)}\t{row.RoiPercent:N1}%\t{row.Read}");
        }

        builder.AppendLine();
        builder.AppendLine($"Total planned capital: {FormatCoinLong(totalCapital)}");
        builder.AppendLine($"Estimated post-fee profit if filled and resold at current net sell: {FormatCoinLong(totalProfit)}");

        if (_lastAccountSnapshot?.AvailableCopper > 0 && totalCapital > _lastAccountSnapshot.AvailableCopper) {
            builder.AppendLine($"Wallet warning: plan exceeds available wallet by {FormatCoinLong(totalCapital - _lastAccountSnapshot.AvailableCopper)}.");
        }

        await ClipboardUtil.WindowsClipboardService.SetTextAsync(builder.ToString());
        SetStatus($"Copied manual auto flip plan: {planRows.Count} items x{quantity:N0}, capital {FormatCoinLong(totalCapital)}, est profit {FormatCoinLong(totalProfit)}.");
    }

    private static AutoFlipPlanRow BuildAutoFlipPlanRow(FlipCandidate candidate, int rank, int quantity) {
        var currentBid = candidate.HighestBuy > 0
            ? candidate.HighestBuy
            : candidate.AcquisitionCostCopper;

        if (currentBid <= 0 || candidate.NetResaleValue <= 0) {
            return null;
        }

        var targetBid = currentBid + 1;
        var read = "Top bid + 1c";

        if (candidate.LowestSell > 0 && targetBid >= candidate.LowestSell) {
            targetBid = currentBid;
            read = "Do not cross sell floor";
        }

        var unitProfit = candidate.NetResaleValue - targetBid;

        if (unitProfit <= 0) {
            return null;
        }

        var capital = (long)targetBid * quantity;
        var estimatedProfit = (long)unitProfit * quantity;
        var roi = targetBid <= 0 ? 0m : unitProfit / (decimal)targetBid * 100m;

        return new AutoFlipPlanRow() {
            Rank = rank,
            ItemName = candidate.ItemName,
            TargetBidCopper = targetBid,
            NetSellCopper = candidate.NetResaleValue,
            CapitalCopper = capital,
            EstimatedProfitCopper = estimatedProfit,
            RoiPercent = roi,
            Read = read
        };
    }

    private void OpenSelectedWiki() {
        var candidate = GetSelectedCandidate();
        var suggestion = GetSelectedAdvisorSuggestion();
        var portfolioRow = GetSelectedPortfolioRow();
        var moneyAction = GetSelectedMoneyAction();

        if (candidate == null && suggestion == null && portfolioRow == null && moneyAction == null) {
            return;
        }

        var url = BuildWikiUrl(candidate, suggestion, portfolioRow, moneyAction);

        try {
            Process.Start(new ProcessStartInfo(url) {
                UseShellExecute = true
            });
            SetStatus($"Opened wiki page for {(candidate?.ItemName ?? suggestion?.ItemName ?? portfolioRow?.ItemName ?? moneyAction?.ItemName)}.");
        } catch (Exception ex) {
            SetStatus($"Failed to open wiki page. {ex.Message}");
        }
    }

    private void ClearRows() {
        foreach (var control in _dynamicControls) {
            control.Parent = null;
            control.Dispose();
        }

        _dynamicControls.Clear();
    }

    private void ClearDetailGraph() {
        foreach (var control in _detailGraphControls) {
            control.Parent = null;
            control.Dispose();
        }

        _detailGraphControls.Clear();
    }

    private void SetDetailTrendLayout(bool hasChart) {
        _detailTrendLabel.Location = new Point(12, hasChart ? 642 : 500);
    }

    private IReadOnlyList<AdvisorSuggestion> BuildAdvisorRows(AdvisorBriefing briefing) {
        var rows = new List<AdvisorSuggestion>();

        if (briefing == null) {
            return rows;
        }

        rows.AddRange(briefing.DailySuggestions ?? new List<AdvisorSuggestion>());
        rows.AddRange((briefing.ExitActions ?? new List<AdvisorSuggestion>()).Where(suggestion => rows.All(existing => existing.ItemId != suggestion.ItemId)));
        rows.AddRange((briefing.CooldownPicks ?? new List<AdvisorSuggestion>()).Where(suggestion => rows.All(existing => existing.ItemId != suggestion.ItemId)));
        rows.AddRange((briefing.InvestmentWatch ?? new List<AdvisorSuggestion>()).Where(suggestion => rows.All(existing => existing.ItemId != suggestion.ItemId)));

        return rows.Take(12).ToList();
    }

    private void ApplyChromeForView(OverlayViewMode viewMode) {
        var isPortfolio = viewMode == OverlayViewMode.Portfolio;
        var isSnipe = viewMode == OverlayViewMode.Snipe;
        _scannerTabButton.Enabled = isPortfolio || isSnipe || IsMoneyActionView(viewMode);
        _portfolioTabButton.Enabled = !isPortfolio;
        _snipeTabButton.Enabled = !isSnipe;
        _ordersTabButton.Enabled = viewMode != OverlayViewMode.Orders;
        _craftTabButton.Enabled = viewMode != OverlayViewMode.CraftBoard;
        _inventoryTabButton.Enabled = viewMode != OverlayViewMode.Inventory;

        _toolbarPanel.Visible = !isPortfolio;
        _filterPanel.Visible = !isPortfolio;
        _modeButton.Visible = !isPortfolio;
        _viewButton.Visible = !isPortfolio;
        _presetButton.Visible = !isPortfolio;
        _savePresetButton.Visible = !isPortfolio;
        _sortButton.Visible = !isPortfolio;
        _rowsButton.Visible = !isPortfolio;
        _capButton.Visible = !isPortfolio;
        _depthButton.Visible = !isPortfolio;
        _discountButton.Visible = !isPortfolio;
        _roiButton.Visible = !isPortfolio;
        _ownedButton.Visible = !isPortfolio;
        _openSellButton.Visible = !isPortfolio;
        _volatilityButton.Visible = !isPortfolio;
        _autoQuantityButton.Visible = !isPortfolio;
        _autoPlanButton.Visible = !isPortfolio;
        _profitDownButton.Visible = !isPortfolio;
        _profitLabel.Visible = !isPortfolio;
        _profitUpButton.Visible = !isPortfolio;
        _practicalButton.Visible = !isPortfolio;
        _portfolioStripLabel.Visible = !isPortfolio;
        _detailPanel.Visible = !isPortfolio;
        _detailScrollbar.Visible = !isPortfolio;

        var showStaticHeaders = !isPortfolio;
        _rankHeaderLabel.Visible = showStaticHeaders;
        _itemHeaderLabel.Visible = showStaticHeaders;
        _costHeaderLabel.Visible = showStaticHeaders;
        _sellHeaderLabel.Visible = showStaticHeaders;
        _profitHeaderLabel.Visible = showStaticHeaders;
        _roiHeaderLabel.Visible = showStaticHeaders;
        _depthHeaderLabel.Visible = showStaticHeaders;
        _pressHeaderLabel.Visible = showStaticHeaders;
        _turnHeaderLabel.Visible = showStaticHeaders;
        _scoreHeaderLabel.Visible = showStaticHeaders;

        _subtitleLabel.Text = isPortfolio
            ? "Track your trading worth, open capital, and market exposure over time"
            : (isSnipe
                ? "Standalone snipe board for urgent under-fair-value Trading Post deals"
                : "Trading Post desk for flips, craft margins, value dips, and account-aware exits");
    }

    private void UpdateHeadersForView(OpportunityMode opportunityMode, OverlayViewMode viewMode) {
        if (viewMode == OverlayViewMode.Advisor) {
            _rankHeaderLabel.Text = "#";
            _itemHeaderLabel.Text = "Pick";
            _costHeaderLabel.Text = "Capital";
            _sellHeaderLabel.Text = "Profit";
            _profitHeaderLabel.Text = "Value";
            _roiHeaderLabel.Text = "Conf";
            _depthHeaderLabel.Text = "Lane";
            _pressHeaderLabel.Text = "Days";
            _turnHeaderLabel.Text = "Act";
            _scoreHeaderLabel.Text = "Gold/Day";
            return;
        }

        if (viewMode == OverlayViewMode.Portfolio) {
            _rankHeaderLabel.Text = "#";
            _itemHeaderLabel.Text = "Exposure";
            _costHeaderLabel.Text = "Qty";
            _sellHeaderLabel.Text = "Unit";
            _profitHeaderLabel.Text = "Net";
            _roiHeaderLabel.Text = "MV%";
            _depthHeaderLabel.Text = "Kind";
            _pressHeaderLabel.Text = "Fair";
            _turnHeaderLabel.Text = "Band";
            _scoreHeaderLabel.Text = "Note";
            return;
        }

        if (viewMode == OverlayViewMode.Snipe) {
            _rankHeaderLabel.Text = "#";
            _itemHeaderLabel.Text = "Snipe Target";
            _costHeaderLabel.Text = "Buy";
            _sellHeaderLabel.Text = "Sell";
            _profitHeaderLabel.Text = "Profit";
            _roiHeaderLabel.Text = "MV%";
            _depthHeaderLabel.Text = "Band";
            _pressHeaderLabel.Text = "Depth";
            _turnHeaderLabel.Text = "Turn";
            _scoreHeaderLabel.Text = "Edge";
            return;
        }

        if (IsMoneyActionView(viewMode)) {
            _rankHeaderLabel.Text = "#";
            _itemHeaderLabel.Text = "Item / Lane";
            _costHeaderLabel.Text = "Action";
            _sellHeaderLabel.Text = "Qty";
            _profitHeaderLabel.Text = "Capital";
            _roiHeaderLabel.Text = "Target";
            _depthHeaderLabel.Text = "Edge";
            _pressHeaderLabel.Text = "Conf";
            _turnHeaderLabel.Text = "Read";
            _scoreHeaderLabel.Text = string.Empty;
            return;
        }

        _rankHeaderLabel.Text = "#";
        _itemHeaderLabel.Text = "Item";
        _costHeaderLabel.Text = opportunityMode == OpportunityMode.Craft || opportunityMode == OpportunityMode.Cooldown ? "Cost" : "Buy";
        _sellHeaderLabel.Text = "Sell";
        _profitHeaderLabel.Text = "Profit";
        _roiHeaderLabel.Text = "MV%";
        _depthHeaderLabel.Text = "Band";
        _pressHeaderLabel.Text = "Depth";
        _turnHeaderLabel.Text = "Turn";
        _scoreHeaderLabel.Text = viewMode == OverlayViewMode.Watchlist ? "Alert" : "Gold/Day";
    }

    private static bool ContainsCandidate(MarketScanResult scanResult, int itemId) {
        foreach (var candidate in scanResult.Candidates) {
            if (candidate.ItemId == itemId) {
                return true;
            }
        }

        return false;
    }

    private static string GetSortModeLabel(FlipSortMode sortMode) {
        switch (sortMode) {
            case FlipSortMode.EstimatedProfit:
                return "Profit";
            case FlipSortMode.SpreadPercent:
                return "ROI";
            case FlipSortMode.MarketValueCheap:
                return "Cheap";
            case FlipSortMode.MarketValueHot:
                return "Hot";
            default:
                return "Fast";
        }
    }

    private static string GetModeLabel(OpportunityMode opportunityMode) {
        switch (opportunityMode) {
            case OpportunityMode.Craft:
                return "Craft";
            case OpportunityMode.Cooldown:
                return "Cooldown";
            case OpportunityMode.Investment:
                return "Invest";
            case OpportunityMode.Value:
                return "Value";
            default:
                return "Flip";
        }
    }

    private static string GetViewModeLabel(OverlayViewMode viewMode) {
        switch (viewMode) {
            case OverlayViewMode.Watchlist:
                return "Watchlist";
            case OverlayViewMode.Ledger:
                return "Ledger";
            case OverlayViewMode.Advisor:
                return "Advisor";
            case OverlayViewMode.Portfolio:
                return "Portfolio";
            case OverlayViewMode.Snipe:
                return "Snipe";
            case OverlayViewMode.Orders:
                return "Orders";
            case OverlayViewMode.CraftBoard:
                return "Craft";
            case OverlayViewMode.Inventory:
                return "Inventory";
            default:
                return "Market";
        }
    }

    private static string BuildSummaryText(FlipQueryOptions queryOptions, OverlayViewMode viewMode, string presetName) {
        var preset = string.IsNullOrWhiteSpace(presetName) ? "Custom" : presetName;
        if (viewMode == OverlayViewMode.Advisor) {
            return $"Advisor board active | Preset {preset} | Mode {GetModeLabel(queryOptions.OpportunityMode)} | Concrete picks for now, later, and exits";
        }

        if (viewMode == OverlayViewMode.Portfolio) {
            return $"Portfolio board active | Preset {preset} | Wallet = liquid gold | Buys = reserved capital | Sells = post-fee value | Holdings = safe inventory value";
        }

        if (viewMode == OverlayViewMode.Snipe) {
            return $"Snipe board active | Preset {preset} | Urgent deals use fair value, ROI, liquidity, and exposure checks";
        }

        if (IsMoneyActionView(viewMode)) {
            return $"{GetViewModeLabel(viewMode)} board active | Short-cycle actions for orders, crafting, and inventory exits";
        }

        return $"{GetViewModeLabel(viewMode)} desk active | Preset {preset} | ROI {FormatPercentFilter(queryOptions.MinimumRoiPercent)} | Depth {FormatDepth(queryOptions.MinimumMarketDepth)} | Exposure {FormatQuantityFilter(queryOptions.MaxOwnedQuantity)}";
    }

    private static string BuildCandidateRowLabel(FlipCandidate candidate, bool isWatched) {
        var watchPrefix = isWatched ? "* " : string.Empty;
        var accountBadge = candidate.OwnedQuantity > 0 || candidate.CurrentSellOrderQuantity > 0 || candidate.CurrentBuyOrderQuantity > 0
            ? $" [{candidate.OwnedQuantity:N0}|{candidate.CurrentBuyOrderQuantity:N0}/{candidate.CurrentSellOrderQuantity:N0}]"
            : string.Empty;
        var alertBadge = candidate.AlertMatches != null && candidate.AlertMatches.Count > 0
            ? $" !{candidate.AlertMatches.Count}"
            : string.Empty;
        return TrimLabel(watchPrefix + candidate.ItemName + accountBadge + alertBadge, 34);
    }

    private static string BuildAdvisorRowLabel(AdvisorSuggestion suggestion) {
        var prefix = suggestion.Action switch {
            AdvisorActionType.Buy => "Buy ",
            AdvisorActionType.Craft => "Craft ",
            AdvisorActionType.Sell => "Sell ",
            AdvisorActionType.Accumulate => "Acc ",
            AdvisorActionType.Skip => "Skip ",
            _ => "Hold "
        };

        return TrimLabel(prefix + suggestion.ItemName, 34);
    }

    private static string BuildSnipeHint(FlipCandidate candidate) {
        var edge = FormatSnipeEdge(candidate);
        var valueRead = candidate.MarketValuePercent > 0m
            ? $"{candidate.MarketValuePercent:N0}% of fair value"
            : "fair value unavailable";
        return $"Snipe edge {edge}: {valueRead}, {candidate.SpreadPercent:N1}% ROI, {candidate.MarketDepth:N0} depth, {candidate.ExpectedFillsPerDay:N1} expected fills/day.";
    }

    private static string BuildDetailStats(FlipCandidate candidate) {
        var accountSection =
            $"Owned now: {candidate.OwnedQuantity:N0}\n" +
            $"Open buys / sells: {candidate.CurrentBuyOrderQuantity:N0} / {candidate.CurrentSellOrderQuantity:N0}\n" +
            $"Weighted fair value: {FormatCoin(candidate.FairValueWeightedCopper)}\n" +
            $"Median fair value: {FormatCoin(candidate.FairValueRecentMedianCopper)}\n" +
            $"Market value: {candidate.MarketValueLabel} ({GetCandidateMarketValueBasis(candidate)})\n" +
            $"Volatility: {candidate.VolatilityPercent:N1}%\n" +
            $"Sold-through confidence: {candidate.SoldThroughConfidence:N1}\n" +
            $"Recommendation: {FormatRecommendation(candidate.RecommendationState)}\n" +
            $"Note: {candidate.RecommendationNote}";

        if (candidate.OpportunityMode == OpportunityMode.Value) {
            return
                $"Acquire now: {FormatCoin(candidate.AcquisitionCostCopper)}\n" +
                $"Current sell floor: {FormatCoin(candidate.LowestSell)}\n" +
                $"Fair value sell: {FormatCoin(candidate.FairValueCopper)}\n" +
                $"Market value: {candidate.MarketValueLabel} ({GetCandidateMarketValueBasis(candidate)})\n" +
                $"Net at fair value: {FormatCoin(candidate.NetResaleValue)}\n" +
                $"Estimated reversion profit: {FormatCoin(candidate.EstimatedProfit)}\n" +
                $"Discount vs fair value: {candidate.DiscountPercent:N1}%\n" +
                $"Reversion ROI: {candidate.SpreadPercent:N1}%\n" +
                $"History samples: {candidate.HistoricalSampleCount}\n" +
                $"Depth: {candidate.MarketDepth:N0}\n" +
                $"Demand pressure: {candidate.DemandPressure:N2}\n" +
                $"Expected fills/day: {candidate.ExpectedFillsPerDay:N1}\n" +
                $"Expected gold/day: {FormatCoin(candidate.ExpectedGoldPerDayCopper)}\n" +
                $"Confidence: {candidate.ConfidenceScore:N1}\n" +
                $"Value score: {candidate.ValueScore:N0}\n" +
                $"{accountSection}";
        }

        var craftSection = candidate.CraftIngredients != null && candidate.CraftIngredients.Count > 0
            ? "\n" + BuildCraftBreakdown(candidate)
            : string.Empty;

        return
            $"Acquire: {FormatCoin(candidate.AcquisitionCostCopper)}\n" +
            $"Sell floor: {FormatCoin(candidate.LowestSell)}\n" +
            $"Net after fees: {FormatCoin(candidate.NetResaleValue)}\n" +
            $"Market value: {candidate.MarketValueLabel} ({GetCandidateMarketValueBasis(candidate)})\n" +
            $"Estimated profit: {FormatCoin(candidate.EstimatedProfit)}\n" +
            $"ROI: {candidate.SpreadPercent:N1}%\n" +
            $"Depth: {candidate.MarketDepth:N0}\n" +
            $"Demand pressure: {candidate.DemandPressure:N2}\n" +
            $"Expected fills/day: {candidate.ExpectedFillsPerDay:N1}\n" +
            $"Expected gold/day: {FormatCoin(candidate.ExpectedGoldPerDayCopper)}\n" +
            $"Volume score: {candidate.VolumeScore:N2}\n" +
            $"Turnover score: {candidate.TurnoverScore:N2}\n" +
            $"Exit quality: {candidate.ExitQualityScore:N1}\n" +
            $"Capital efficiency: {candidate.CapitalEfficiencyScore:N1}\n" +
            $"Confidence: {candidate.ConfidenceScore:N1}\n" +
            $"Advisor score: {candidate.AdvisorScore:N0}\n" +
            $"Liquidity: {candidate.LiquidityScore:N2} | Stability: {candidate.StabilityScore:N2}\n" +
            $"{accountSection}{craftSection}";
    }

    private static string BuildDetailTrend(FlipCandidate candidate) {
        var badgeSection = candidate.InsightBadges != null && candidate.InsightBadges.Count > 0
            ? $"Badges: {string.Join(", ", candidate.InsightBadges)}\n"
            : string.Empty;
        var alertSection = candidate.AlertMatches != null && candidate.AlertMatches.Count > 0
            ? $"Alerts: {string.Join(" | ", candidate.AlertMatches.Select(alert => alert.RuleName))}\n"
            : string.Empty;
        var priceHistory = candidate.PriceHistory?
            .Where(point => point.LowestSell > 0)
            .OrderBy(point => point.RecordedAtUtc)
            .ToList() ?? new List<PriceSnapshotEntry>();
        var historySection = priceHistory.Count >= 2
            ? $"Graph: {priceHistory.Count} local samples | sell {FormatSignedCoin(priceHistory.Last().LowestSell - priceHistory.First().LowestSell)} since {priceHistory.First().RecordedAtUtc.LocalDateTime:MM-dd HH:mm}\n"
            : "Graph: save more full-scan snapshots to build a stronger local trend.\n";
        if (candidate.OpportunityMode == OpportunityMode.Value) {
            return
                $"Previous snapshot: {FormatPreviousSeen(candidate)}\n" +
                $"Buy delta: {FormatSignedCoin(candidate.BuyDeltaCopper)}\n" +
                $"Sell delta: {FormatSignedCoin(candidate.SellDeltaCopper)}\n" +
                historySection +
                badgeSection +
                alertSection +
                $"Guide: higher discount plus healthy depth usually means the floor is cheap relative to its own recent market history.";
        }

        return
            $"Previous snapshot: {FormatPreviousSeen(candidate)}\n" +
            $"Buy delta: {FormatSignedCoin(candidate.BuyDeltaCopper)}\n" +
            $"Sell delta: {FormatSignedCoin(candidate.SellDeltaCopper)}\n" +
            historySection +
            badgeSection +
            alertSection +
            $"Pressure guide: above 1.00 usually means buy demand is keeping up with sell-side supply.";
    }

    private static string BuildItemTooltip(FlipCandidate candidate) {
        if (candidate.OpportunityMode == OpportunityMode.Value) {
            return $"{candidate.ItemName}\n" +
                   $"Mode: Value\n" +
                   $"Type: {candidate.ItemType}\n" +
                   $"Buy now: {FormatCoin(candidate.AcquisitionCostCopper)}\n" +
                   $"Fair value: {FormatCoin(candidate.FairValueCopper)}\n" +
                   $"Market value: {candidate.MarketValueLabel} ({GetCandidateMarketValueBasis(candidate)})\n" +
                   $"Discount: {candidate.DiscountPercent:N1}%\n" +
                   $"Reversion profit: {FormatCoin(candidate.EstimatedProfit)}\n" +
                   $"Gold/day: {FormatCoin(candidate.ExpectedGoldPerDayCopper)}\n" +
                   $"Depth: {candidate.MarketDepth:N0}\n" +
                   $"Samples: {candidate.HistoricalSampleCount}\n" +
                   $"Owned / open sells: {candidate.OwnedQuantity:N0} / {candidate.CurrentSellOrderQuantity:N0}\n" +
                   $"Confidence: {candidate.ConfidenceScore:N1}\n" +
                   $"Value score: {candidate.ValueScore:N0}\n" +
                   $"Recommendation: {FormatRecommendation(candidate.RecommendationState)}";
        }

        return $"{candidate.ItemName}\n" +
               $"Mode: {candidate.OpportunityMode}\n" +
               $"Type: {candidate.ItemType}\n" +
               $"Acquire: {FormatCoin(candidate.AcquisitionCostCopper)}\n" +
               $"Profit: {FormatCoin(candidate.EstimatedProfit)}\n" +
               $"Gold/day: {FormatCoin(candidate.ExpectedGoldPerDayCopper)}\n" +
               $"ROI: {candidate.SpreadPercent:N1}%\n" +
               $"Market value: {candidate.MarketValueLabel} ({GetCandidateMarketValueBasis(candidate)})\n" +
               $"Depth: {candidate.MarketDepth:N0}\n" +
               $"Demand pressure: {candidate.DemandPressure:N2}\n" +
               $"Owned / open sells: {candidate.OwnedQuantity:N0} / {candidate.CurrentSellOrderQuantity:N0}\n" +
               $"Weighted fair value: {FormatCoin(candidate.FairValueWeightedCopper)}\n" +
               $"Volatility: {candidate.VolatilityPercent:N1}%\n" +
               $"Volume: {candidate.VolumeScore:N2}\n" +
               $"Turnover: {candidate.TurnoverScore:N2}\n" +
               $"Confidence: {candidate.ConfidenceScore:N1}\n" +
               $"Fast score: {candidate.FastFlipScore:N0}\n" +
               $"Recommendation: {FormatRecommendation(candidate.RecommendationState)}";
    }

    private static string BuildLedgerDetailStats(TransactionLedgerEntry entry) {
        return
            $"Held quantity: {entry.HeldQuantity:N0}\n" +
            $"Bought / sold: {entry.BoughtQuantity:N0} / {entry.SoldQuantity:N0}\n" +
            $"Average buy: {FormatCoin(entry.AverageBuyPriceCopper)}\n" +
            $"Average sell: {FormatCoin(entry.AverageSellPriceCopper)}\n" +
            $"Current sell floor: {FormatCoin(entry.CurrentSellFloorCopper)}\n" +
            $"Fees paid: {FormatCoin(entry.FeesPaidCopper)}\n" +
            $"Realized profit: {FormatCoin(entry.RealizedProfitCopper)}\n" +
            $"Unrealized profit: {FormatCoin(entry.UnrealizedProfitCopper)}\n" +
            $"Open buys / sells: {entry.CurrentOpenBuyQuantity:N0} / {entry.CurrentOpenSellQuantity:N0}\n" +
            $"Confidence: {entry.MarketConfidenceScore:N1}\n" +
            $"Recommendation: {FormatRecommendation(entry.RecommendationState)}";
    }

    private static string BuildLedgerDetailTrend(TransactionLedgerEntry entry) {
        return
            $"Last buy: {FormatTimestamp(entry.LastBoughtUtc)}\n" +
            $"Last sell: {FormatTimestamp(entry.LastSoldUtc)}\n" +
            $"Guidance: {entry.Notes}";
    }

    private static string BuildAdvisorDetailStats(AdvisorSuggestion suggestion) {
        return
            $"Action: {FormatAction(suggestion.Action)}\n" +
            $"Section: {FormatSection(suggestion.Section)}\n" +
            $"Strategy: {FormatStrategyTag(suggestion.StrategyTag)}\n" +
            $"Capital: {FormatCoin(suggestion.CapitalRequiredCopper)}\n" +
            $"Profit per move: {FormatCoin(suggestion.EstimatedProfitCopper)}\n" +
            $"Market value: {suggestion.MarketValueLabel} ({GetAdvisorMarketValueBasis(suggestion)})\n" +
            $"Expected fills/day: {suggestion.ExpectedFillsPerDay:N1}\n" +
            $"Expected gold/day: {FormatCoin(suggestion.ExpectedGoldPerDayCopper)}\n" +
            $"Confidence: {suggestion.ConfidenceScore:N1}\n" +
            $"Portfolio impact: {FormatSignedCoin(suggestion.PortfolioImpactCopper)} | {suggestion.LiquidityImpactLabel}\n" +
            $"Why now: {suggestion.WhyNow}\n" +
            $"Risk: {suggestion.RiskNotes}";
    }

    private static string BuildAdvisorDetailTrend(AdvisorSuggestion suggestion, AdvisorBriefing briefing) {
        var digest = briefing?.DigestLines?.Count > 0
            ? string.Join("\n", briefing.DigestLines.Take(3))
            : "No digest notes yet.";

        return
            $"Season window: {suggestion.SeasonWindowState}\n" +
            $"Why not: {suggestion.WhyNot}\n" +
            $"What changed: {suggestion.WhatChanged}\n" +
            $"Wallet/open-order use: {(suggestion.UsesWalletCapital ? "wallet " : string.Empty)}{(suggestion.UsesOpenOrders ? "orders " : string.Empty)}{(suggestion.UsesOwnedMaterials ? "owned mats" : string.Empty)}\n" +
            $"Digest:\n{digest}";
    }

    private static string BuildLedgerTooltip(TransactionLedgerEntry entry) {
        return $"{entry.ItemName}\n" +
               $"Held: {entry.HeldQuantity:N0}\n" +
               $"Realized: {FormatCoin(entry.RealizedProfitCopper)}\n" +
               $"Unrealized: {FormatCoin(entry.UnrealizedProfitCopper)}\n" +
               $"Open sells: {entry.CurrentOpenSellQuantity:N0}\n" +
               $"Recommendation: {FormatRecommendation(entry.RecommendationState)}";
    }

    private static string BuildAdvisorTooltip(AdvisorSuggestion suggestion) {
        return $"{suggestion.ItemName}\n" +
               $"Action: {FormatAction(suggestion.Action)}\n" +
               $"Lane: {FormatStrategyTag(suggestion.StrategyTag)}\n" +
               $"Capital: {FormatCoin(suggestion.CapitalRequiredCopper)}\n" +
               $"Profit: {FormatCoin(suggestion.EstimatedProfitCopper)}\n" +
               $"Value: {suggestion.MarketValueLabel} ({GetAdvisorMarketValueBasis(suggestion)})\n" +
               $"Gold/day: {FormatCoin(suggestion.ExpectedGoldPerDayCopper)}\n" +
               $"Reason: {suggestion.BriefReason}";
    }

    private static string BuildPortfolioDetailStats(PortfolioRow row, PortfolioSummary summary) {
        var meaning = row.Kind switch {
            PortfolioRowKind.OpenBuy => "Gold is reserved here while the buy order waits to fill.",
            PortfolioRowKind.OpenSell => "This is listed stock. The value shown is what you should receive after TP fees.",
            _ => "This is inventory you still hold. The value is intentionally conservative."
        };

        return
            $"What this is: {FormatPortfolioKindLong(row.Kind)}\n" +
            $"Why it matters: {meaning}\n" +
            $"Quantity: {row.Quantity:N0}\n" +
            $"Price per item: {FormatCoin(row.UnitPriceCopper)}\n" +
            $"Estimated total value: {FormatCoin(row.NetValueCopper)}\n" +
            $"Local fair value: {FormatCoin(row.FairValueCopper)}\n" +
            $"Value signal: {row.MarketValueLabel}\n" +
            $"Portfolio total worth: {FormatCoin(summary?.NetWorthCopper ?? 0)}\n" +
            $"Working capital: buys {FormatCoin(summary?.OutstandingBuyCopper ?? 0)} | sells {FormatCoin(summary?.OutstandingSellNetCopper ?? 0)}\n" +
            $"Inventory bucket: {FormatCoin(summary?.HoldingsValueCopper ?? 0)}";
    }

    private static string BuildPortfolioDetailTrend(PortfolioRow row, PortfolioSnapshot snapshot) {
        var summary = snapshot?.Summary ?? new PortfolioSummary();
        var trendPreview = snapshot?.Trend?.Count > 0
            ? string.Join("\n", snapshot.Trend.Skip(Math.Max(0, snapshot.Trend.Count - 4)).Select(point => $"{point.CapturedAtUtc.LocalDateTime:MM-dd HH:mm}  {FormatCoin(point.NetWorthCopper)}"))
            : "No trend points saved yet.";
        var itemHistory = snapshot?.HistoricalResults?
            .FirstOrDefault(result => result.ItemId == row.ItemId);
        var historyLine = itemHistory == null
            ? "Historical result: no completed buy/sell history for this item yet."
            : $"Historical result: {itemHistory.Verdict} | {FormatSignedCoin(itemHistory.TotalProfitCopper)} | ROI {itemHistory.ReturnOnCapitalPercent:N1}%";

        return
            $"Portfolio momentum: 1 day {FormatSignedCoin(summary.DailyDeltaCopper)} | 7 days {FormatSignedCoin(summary.WeeklyDeltaCopper)} | 30 days {FormatSignedCoin(summary.MonthlyDeltaCopper)}\n" +
            $"Profit read: realized {FormatCoin(summary.RealizedProfitCopper)} | still open {FormatCoin(summary.UnrealizedProfitCopper)}\n" +
            $"{historyLine}\n" +
            $"Recent worth points:\n{trendPreview}";
    }

    private static IReadOnlyList<HistoricalInvestmentResult> GetBestHistoricalResults(PortfolioSnapshot snapshot) {
        return (snapshot?.HistoricalResults ?? new List<HistoricalInvestmentResult>())
            .Where(result => result.TotalProfitCopper > 0)
            .OrderByDescending(result => result.TotalProfitCopper)
            .ThenByDescending(result => result.ReturnOnCapitalPercent)
            .Take(3)
            .ToList();
    }

    private static IReadOnlyList<HistoricalInvestmentResult> GetWorstHistoricalResults(PortfolioSnapshot snapshot) {
        return (snapshot?.HistoricalResults ?? new List<HistoricalInvestmentResult>())
            .Where(result => result.TotalProfitCopper < 0)
            .OrderBy(result => result.TotalProfitCopper)
            .ThenBy(result => result.ReturnOnCapitalPercent)
            .Take(3)
            .ToList();
    }

    private static string BuildHistoricalResultTooltip(HistoricalInvestmentResult result) {
        return $"{result.ItemName}\n" +
               $"{result.Verdict}\n" +
               $"Total P/L: {FormatSignedCoin(result.TotalProfitCopper)}\n" +
               $"Realized: {FormatSignedCoin(result.RealizedProfitCopper)}\n" +
               $"Open P/L: {FormatSignedCoin(result.UnrealizedProfitCopper)}\n" +
               $"ROI: {result.ReturnOnCapitalPercent:N1}%\n" +
               $"Bought / sold / held: {result.BoughtQuantity:N0} / {result.SoldQuantity:N0} / {result.HeldQuantity:N0}\n" +
               $"Average buy: {FormatCoin(result.AverageBuyPriceCopper)}\n" +
               $"Current sell floor: {FormatCoin(result.CurrentSellFloorCopper)}\n" +
               $"Last activity: {FormatTimestamp(result.LastActivityUtc)}";
    }

    private static string BuildPortfolioStatusVerdict(PortfolioSummary summary) {
        var totalProfit = summary.RealizedProfitCopper + summary.UnrealizedProfitCopper;

        if (summary.WeeklyDeltaCopper > 0 && totalProfit >= 0) {
            return "Trend: portfolio growing";
        }

        if (summary.WeeklyDeltaCopper < 0 && totalProfit < 0) {
            return "Trend: review exposure";
        }

        if (summary.OutstandingBuyCopper > summary.WalletCopper + summary.OutstandingSellNetCopper) {
            return "Trend: capital tied in buys";
        }

        return "Trend: stable watch";
    }

    private static string GetMoneyActionKey(MoneyActionRow row) {
        return row == null ? string.Empty : $"{row.Lane}:{row.Action}:{row.ItemId}:{row.Quantity}:{row.TargetCopper}";
    }

    private static bool IsMoneyActionView(OverlayViewMode viewMode) {
        return viewMode == OverlayViewMode.Orders ||
               viewMode == OverlayViewMode.CraftBoard ||
               viewMode == OverlayViewMode.Inventory;
    }

    private static string BuildMoneyActionLabel(MoneyActionRow row, bool isWatched) {
        var watchPrefix = isWatched ? "* " : string.Empty;
        return TrimLabel($"{watchPrefix}{row.ItemName} [{row.Lane}]", 34);
    }

    private static string BuildMoneyActionTooltip(MoneyActionRow row) {
        return $"{row.ItemName}\n" +
               $"Lane: {row.Lane}\n" +
               $"Action: {row.Action}\n" +
               $"Quantity: {row.Quantity:N0}\n" +
               $"Capital/value: {FormatCoin(row.CapitalCopper)}\n" +
               $"Target: {FormatCoin(row.TargetCopper)}\n" +
               $"Edge: {FormatSignedCoin(row.EdgeCopper)}\n" +
               $"Confidence: {row.ConfidenceScore:N1}\n" +
               $"Read: {row.Notes}";
    }

    private static string BuildMoneyActionDetailStats(MoneyActionRow row) {
        return
            $"Action: {row.Action}\n" +
            $"Lane: {row.Lane}\n" +
            $"Quantity: {row.Quantity:N0}\n" +
            $"Capital/value: {FormatCoin(row.CapitalCopper)}\n" +
            $"Target price/value: {FormatCoin(row.TargetCopper)}\n" +
            $"Expected edge: {FormatSignedCoin(row.EdgeCopper)}\n" +
            $"Confidence: {row.ConfidenceScore:N1}\n" +
            $"Item id: {row.ItemId}";
    }

    private static string BuildMoneyActionDetailTrend(MoneyActionRow row) {
        return $"Short-cycle read: {row.Notes}\n" +
               "Use this as a TP checklist item: confirm current price, then act only if the spread still exists.";
    }

    private static Color GetMoneyActionColor(MoneyActionRow row) {
        switch (row?.Action ?? string.Empty) {
            case "Reprice":
            case "Sell":
                return new Color(210, 145, 72);
            case "Cancel":
            case "Avoid":
                return new Color(196, 89, 89);
            case "Craft":
                return new Color(116, 178, 209);
            case "Hold":
                return new Color(197, 185, 84);
            default:
                return new Color(103, 192, 122);
        }
    }

    private static string GetMoneyActionEmptyText(OverlayViewMode viewMode) {
        return viewMode switch {
            OverlayViewMode.Orders => "No order actions yet. Add an API key with tradingpost scope and run a scan to find undercut sells and stale buy orders.",
            OverlayViewMode.CraftBoard => "No short-cycle craft actions yet. Run Craft or Cooldown scans to populate profitable craft actions.",
            OverlayViewMode.Inventory => "No inventory exit actions yet. Add inventory scopes and run scans to classify sell, hold, and craft-from-stock options.",
            _ => "No actions available yet."
        };
    }

    private static string BuildPortfolioTooltip(PortfolioRow row) {
        return $"{row.ItemName}\n" +
               $"Kind: {FormatPortfolioKindLong(row.Kind)}\n" +
               $"Quantity: {row.Quantity:N0}\n" +
               $"Unit: {FormatCoin(row.UnitPriceCopper)}\n" +
               $"Total value: {FormatCoin(row.NetValueCopper)}\n" +
               $"Signal: {row.MarketValueLabel}\n" +
               $"Meaning: {row.Notes}";
    }

    private static Color GetRecommendationColor(RecommendationState recommendationState) {
        switch (recommendationState) {
            case RecommendationState.SellExisting:
                return new Color(210, 145, 72);
            case RecommendationState.CraftFromStockOnly:
                return new Color(116, 178, 209);
            case RecommendationState.Skip:
                return new Color(196, 89, 89);
            case RecommendationState.Hold:
                return new Color(197, 185, 84);
            default:
                return new Color(103, 192, 122);
        }
    }

    private static Color GetAdvisorActionColor(AdvisorActionType action) {
        switch (action) {
            case AdvisorActionType.Sell:
                return new Color(210, 145, 72);
            case AdvisorActionType.Craft:
                return new Color(116, 178, 209);
            case AdvisorActionType.Hold:
                return new Color(197, 185, 84);
            case AdvisorActionType.Skip:
                return new Color(196, 89, 89);
            case AdvisorActionType.Accumulate:
                return new Color(168, 132, 212);
            default:
                return new Color(103, 192, 122);
        }
    }

    private static Color GetPressureColor(decimal demandPressure) {
        if (demandPressure >= 1.00m) {
            return Color.LightGreen;
        }

        if (demandPressure < 0.65m) {
            return Color.IndianRed;
        }

        return Color.White;
    }

    private static Color GetRarityColor(string rarity) {
        switch (rarity ?? string.Empty) {
            case "Rare":
                return new Color(255, 204, 102);
            case "Exotic":
                return new Color(255, 178, 64);
            case "Ascended":
                return new Color(255, 102, 255);
            case "Fine":
                return new Color(102, 186, 255);
            default:
                return Color.White;
        }
    }

    private static string BuildWikiCommand(FlipCandidate candidate, AdvisorSuggestion suggestion, PortfolioRow row = null, MoneyActionRow actionRow = null) {
        var itemName = candidate?.ItemName ?? suggestion?.ItemName ?? row?.ItemName ?? actionRow?.ItemName;
        return string.IsNullOrWhiteSpace(itemName) ? string.Empty : $"/wiki {itemName}";
    }

    private static string BuildWikiUrl(FlipCandidate candidate, AdvisorSuggestion suggestion, PortfolioRow row = null, MoneyActionRow actionRow = null) {
        var itemName = candidate?.ItemName ?? suggestion?.ItemName ?? row?.ItemName ?? actionRow?.ItemName;
        return string.IsNullOrWhiteSpace(itemName)
            ? "https://wiki.guildwars2.com/"
            : $"https://wiki.guildwars2.com/wiki/Special:Search/{Uri.EscapeDataString(itemName)}";
    }

    private static string FormatPreviousSeen(FlipCandidate candidate) {
        return candidate.PreviousSeenUtc.HasValue
            ? candidate.PreviousSeenUtc.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "No prior snapshot";
    }

    private static string FormatSignedCoin(int copper) {
        if (copper == 0) {
            return "0c";
        }

        var prefix = copper > 0 ? "+" : "-";
        return prefix + FormatCoin(Math.Abs(copper));
    }

    private static string FormatCoin(int copper) {
        var sign = copper < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(copper);
        var gold = absolute / 10000;
        var silver = absolute / 100 % 100;
        var bronze = absolute % 100;

        return $"{sign}{gold}g {silver:D2}s {bronze:D2}c";
    }

    private static string FormatCoinLong(long copper) {
        var sign = copper < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(copper);
        var gold = absolute / 10000;
        var silver = absolute / 100 % 100;
        var bronze = absolute % 100;

        return $"{sign}{gold}g {silver:D2}s {bronze:D2}c";
    }

    private static string FormatCoinCompact(int copper) {
        var sign = copper < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(copper);
        var gold = absolute / 10000;
        var silver = absolute / 100 % 100;
        var bronze = absolute % 100;

        if (gold > 0) {
            return $"{sign}{gold}g {silver:D2}s";
        }

        if (silver > 0) {
            return $"{sign}{silver}s {bronze:D2}c";
        }

        return $"{sign}{bronze}c";
    }

    private static string FormatCap(int copper) {
        return copper <= 0 ? "Any" : FormatCoin(copper);
    }

    private static string FormatDepth(int depth) {
        if (depth <= 0) {
            return "Any";
        }

        return depth >= 1000
            ? $"{depth / 1000}k"
            : depth.ToString();
    }

    private static string FormatDiscount(int discountPercent) {
        return discountPercent <= 0 ? "Any" : $"{discountPercent}%";
    }

    private static string FormatPercentFilter(int value) {
        return value <= 0 ? "Any" : $"{value}%";
    }

    private static string FormatQuantityFilter(int value) {
        return value <= 0 ? "Any" : value >= 1000 ? $"{value / 1000}k" : value.ToString();
    }

    private static string FormatRecommendation(RecommendationState recommendationState) {
        switch (recommendationState) {
            case RecommendationState.SellExisting:
                return "Sell existing";
            case RecommendationState.CraftFromStockOnly:
                return "Craft from stock";
            case RecommendationState.Skip:
                return "Do not re-enter";
            case RecommendationState.Hold:
                return "Hold";
            default:
                return "Buy";
        }
    }

    private static string FormatAction(AdvisorActionType action) {
        return action switch {
            AdvisorActionType.Buy => "Buy now",
            AdvisorActionType.Craft => "Craft now",
            AdvisorActionType.Sell => "Sell stock",
            AdvisorActionType.Accumulate => "Accumulate",
            AdvisorActionType.Skip => "Avoid",
            _ => "Hold"
        };
    }

    private static string FormatSection(AdvisorSection section) {
        return section switch {
            AdvisorSection.ExitActions => "Best Exit Actions",
            AdvisorSection.CooldownPicks => "Daily Cooldown Picks",
            AdvisorSection.InvestmentWatch => "Investment Watch",
            _ => "Top Picks Today"
        };
    }

    private static string FormatStrategyTag(AdvisorStrategyTag strategyTag) {
        return strategyTag switch {
            AdvisorStrategyTag.CraftMargin => "Craft margin",
            AdvisorStrategyTag.ValueReversion => "Value reversion",
            AdvisorStrategyTag.Cooldown => "Cooldown",
            AdvisorStrategyTag.Seasonal => "Seasonal",
            AdvisorStrategyTag.Rotation => "Rotation",
            AdvisorStrategyTag.Exit => "Exit",
            _ => "Fast flip"
        };
    }

    private static string FormatLaneLabel(AdvisorStrategyTag strategyTag) {
        return strategyTag switch {
            AdvisorStrategyTag.Cooldown => "Cooldown",
            AdvisorStrategyTag.Seasonal => "Season",
            AdvisorStrategyTag.ValueReversion => "Value",
            AdvisorStrategyTag.CraftMargin => "Craft",
            AdvisorStrategyTag.Exit => "Exit",
            _ => "Flip"
        };
    }

    private static string FormatHorizon(int investmentHorizonDays) {
        return investmentHorizonDays <= 0 ? "Now" : investmentHorizonDays == 1 ? "1d" : $"{investmentHorizonDays}d";
    }

    private static string FormatSnipeEdge(FlipCandidate candidate) {
        var cheapEdge = candidate.MarketValuePercent > 0
            ? Math.Max(0m, 100m - candidate.MarketValuePercent)
            : 0m;
        var edge = cheapEdge + Math.Max(0m, candidate.SpreadPercent / 2m) + Math.Max(0m, candidate.AlertScore);
        return $"{edge:N0}";
    }

    private static string FormatActionShort(AdvisorActionType action) {
        return action switch {
            AdvisorActionType.Buy => "Buy",
            AdvisorActionType.Craft => "Craft",
            AdvisorActionType.Sell => "Sell",
            AdvisorActionType.Accumulate => "Acc",
            AdvisorActionType.Skip => "Skip",
            _ => "Hold"
        };
    }

    private static string FormatPortfolioKind(PortfolioRowKind kind) {
        return kind switch {
            PortfolioRowKind.OpenBuy => "Buy",
            PortfolioRowKind.OpenSell => "Sell",
            _ => "Held"
        };
    }

    private static string FormatPortfolioKindLong(PortfolioRowKind kind) {
        return kind switch {
            PortfolioRowKind.OpenBuy => "Open buy order",
            PortfolioRowKind.OpenSell => "Open sell listing",
            _ => "Held inventory"
        };
    }

    private static Color GetPortfolioKindColor(PortfolioRowKind kind) {
        return kind switch {
            PortfolioRowKind.OpenBuy => new Color(116, 178, 209),
            PortfolioRowKind.OpenSell => new Color(210, 145, 72),
            _ => new Color(103, 192, 122)
        };
    }

    private static string FormatMarketValueBandShort(MarketValueBand band) {
        return band switch {
            MarketValueBand.BelowFair => "Below",
            MarketValueBand.Overheated => "Hot",
            _ => band.ToString()
        };
    }

    private static Color GetMarketValueColor(MarketValueBand band) {
        return band switch {
            MarketValueBand.Cheap => new Color(100, 220, 126),
            MarketValueBand.BelowFair => new Color(142, 226, 176),
            MarketValueBand.Rich => new Color(255, 198, 102),
            MarketValueBand.Overheated => new Color(224, 120, 120),
            _ => Color.White
        };
    }

    private static Color GetMarketValuePercentColor(decimal marketValuePercent) {
        if (marketValuePercent < 85m) {
            return GetMarketValueColor(MarketValueBand.Cheap);
        }

        if (marketValuePercent < 95m) {
            return GetMarketValueColor(MarketValueBand.BelowFair);
        }

        if (marketValuePercent <= 105m) {
            return GetMarketValueColor(MarketValueBand.Fair);
        }

        if (marketValuePercent <= 120m) {
            return GetMarketValueColor(MarketValueBand.Rich);
        }

        return GetMarketValueColor(MarketValueBand.Overheated);
    }

    private static string FormatTimestamp(DateTimeOffset? timestamp) {
        return timestamp.HasValue
            ? timestamp.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "No data";
    }

    private static string BuildCraftBreakdown(FlipCandidate candidate) {
        if (candidate.CraftIngredients == null || candidate.CraftIngredients.Count == 0) {
            return string.Empty;
        }

        var preview = candidate.CraftIngredients
            .Take(4)
            .Select(ingredient => $"{ingredient.ItemName}: need {ingredient.RequiredCount}, own {ingredient.OwnedCount}, miss {ingredient.MissingCount}")
            .ToList();
        var overflow = candidate.CraftIngredients.Count > 4 ? $"\n+ {candidate.CraftIngredients.Count - 4} more ingredients" : string.Empty;
        return $"Craft from owned: {candidate.CraftFromOwnedCount:N0}\nMissing material cost: {FormatCoin(candidate.MissingCraftCostCopper)}\n" +
               string.Join("\n", preview) +
               overflow;
    }

    private static string TrimLabel(string text, int maxLength) {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength) {
            return text ?? string.Empty;
        }

        return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private void UpdatePortfolioStripLabel(PortfolioSummary summary) {
        var portfolioSummary = summary ?? new PortfolioSummary();
        var authLabel = portfolioSummary.IsAuthenticated ? "Live" : "Partial";
        _portfolioStripLabel.Text =
            $"Portfolio {authLabel} | Wallet {FormatCoin(portfolioSummary.WalletCopper)} | Buys {FormatCoin(portfolioSummary.OutstandingBuyCopper)} | Sells {FormatCoin(portfolioSummary.OutstandingSellNetCopper)} | Holdings {FormatCoin(portfolioSummary.HoldingsValueCopper)} | Worth {FormatCoin(portfolioSummary.NetWorthCopper)} | 1d {FormatSignedCoin(portfolioSummary.DailyDeltaCopper)}";
    }

    private static string GetPortfolioKey(PortfolioRow row) {
        return row == null ? string.Empty : $"{row.Kind}:{row.ItemId}:{row.Quantity}:{row.UnitPriceCopper}";
    }

    private static string BuildPortfolioRowLabel(PortfolioRow row, bool isWatched) {
        var watchPrefix = isWatched ? "* " : string.Empty;
        return TrimLabel($"{watchPrefix}{row.ItemName} [{FormatPortfolioKind(row.Kind)}]", 34);
    }

    private static string BuildPortfolioNoteLabel(PortfolioRow row) {
        if (row == null) {
            return string.Empty;
        }

        return row.Kind switch {
            PortfolioRowKind.OpenBuy => "Tied up",
            PortfolioRowKind.OpenSell => "Listed",
            _ => "Stored"
        };
    }

    private static string GetCandidateMarketValueBasis(FlipCandidate candidate) {
        if (candidate == null) {
            return "vs fair value";
        }

        return candidate.OpportunityMode switch {
            OpportunityMode.Craft => "vs fair craft cost",
            OpportunityMode.Cooldown => "vs fair cooldown cost",
            OpportunityMode.Investment => "vs fair market floor",
            OpportunityMode.Value => "vs fair acquire price",
            _ => "vs fair buy price"
        };
    }

    private static string GetAdvisorMarketValueBasis(AdvisorSuggestion suggestion) {
        if (suggestion == null) {
            return "vs fair value";
        }

        return suggestion.OpportunityMode switch {
            OpportunityMode.Craft => "vs fair craft cost",
            OpportunityMode.Cooldown => "vs fair cooldown cost",
            OpportunityMode.Investment => "vs fair market floor",
            OpportunityMode.Value => "vs fair acquire price",
            _ => "vs fair buy price"
        };
    }

    private static string ExtractMarketValueBand(string marketValueLabel) {
        if (string.IsNullOrWhiteSpace(marketValueLabel)) {
            return "Fair";
        }

        if (marketValueLabel.IndexOf("Cheap", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Cheap";
        }

        if (marketValueLabel.IndexOf("Below", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Below";
        }

        if (marketValueLabel.IndexOf("Rich", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Rich";
        }

        if (marketValueLabel.IndexOf("Hot", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "Hot";
        }

        return "Fair";
    }

    private void CreatePortfolioCell(Container parent, int x, int width, string text, Color textColor, string tooltip, PortfolioRow row) {
        var label = new Label() {
            Parent = parent,
            Location = new Point(x + 6, 1),
            Size = new Point(width, 20),
            Text = text,
            ShowShadow = true,
            TextColor = textColor
        };

        if (!string.IsNullOrWhiteSpace(tooltip)) {
            label.BasicTooltipText = tooltip;
        }

        AttachPortfolioSelection(label, row);
    }

    private static Point ClampLocationToScreen(Point candidateLocation, int width, int height) {
        var screenWidth = GameService.Graphics.SpriteScreen.Width;
        var screenHeight = GameService.Graphics.SpriteScreen.Height;
        var maxX = Math.Max(0, screenWidth - width);
        var maxY = Math.Max(0, screenHeight - height);

        return new Point(
            Math.Max(0, Math.Min(maxX, candidateLocation.X)),
            Math.Max(0, Math.Min(maxY, candidateLocation.Y)));
    }

    private enum PortfolioGrowthPeriod {
        Day,
        Week,
        Month
    }

    private sealed class PortfolioGrowthBucket {
        public DateTime StartLocal { get; set; }
        public int OpenNetWorthCopper { get; set; }
        public int CloseNetWorthCopper { get; set; }
        public int DeltaCopper { get; set; }
        public string Label { get; set; } = string.Empty;
        public string TooltipLabel { get; set; } = string.Empty;
    }

    private sealed class AutoFlipPlanRow {
        public int Rank { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int TargetBidCopper { get; set; }
        public int NetSellCopper { get; set; }
        public long CapitalCopper { get; set; }
        public long EstimatedProfitCopper { get; set; }
        public decimal RoiPercent { get; set; }
        public string Read { get; set; } = string.Empty;
    }
}
