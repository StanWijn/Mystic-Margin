using Blish_HUD.Settings;
using Gw2FlipOverlay.Models;

namespace Gw2FlipOverlay;

public sealed class ModuleSettings {

    public SettingEntry<bool> OpenOnLoad { get; private set; }
    public SettingEntry<bool> UseMockData { get; private set; }
    public SettingEntry<bool> HideWhenGw2Unfocused { get; private set; }
    public SettingEntry<bool> PracticalOnly { get; private set; }
    public SettingEntry<string> ApiKey { get; private set; }
    public SettingEntry<int> TopCount { get; private set; }
    public SettingEntry<int> MinimumProfitCopper { get; private set; }
    public SettingEntry<int> RefreshIntervalSeconds { get; private set; }
    public SettingEntry<int> SortMode { get; private set; }
    public SettingEntry<int> HistoryRetentionDays { get; private set; }
    public SettingEntry<int> OpportunityMode { get; private set; }
    public SettingEntry<int> MaxAcquireCostCopper { get; private set; }
    public SettingEntry<int> MinimumMarketDepth { get; private set; }
    public SettingEntry<int> MinimumDiscountPercent { get; private set; }
    public SettingEntry<int> MinimumRoiPercent { get; private set; }
    public SettingEntry<bool> WatchlistOnly { get; private set; }
    public SettingEntry<int> MaxOwnedQuantity { get; private set; }
    public SettingEntry<int> MaxOpenSellQuantity { get; private set; }
    public SettingEntry<int> MaxVolatilityPercent { get; private set; }
    public SettingEntry<int> AutoFlipQuantity { get; private set; }
    public SettingEntry<string> ActivePresetId { get; private set; }

    public void Define(SettingCollection settings) {
        OpenOnLoad = settings.DefineSetting(
            "open_on_load",
            true,
            () => "Open overlay on load",
            () => "Shows the flip overlay as soon as the module is enabled."
        );

        UseMockData = settings.DefineSetting(
            "use_mock_data",
            false,
            () => "Use mock data",
            () => "Shows seeded demo rows instead of live Trading Post data."
        );

        HideWhenGw2Unfocused = settings.DefineSetting(
            "hide_when_gw2_unfocused",
            true,
            () => "Hide when GW2 is not focused",
            () => "Keeps the module at normal Guild Wars 2 UI priority instead of remaining visible over other desktop windows."
        );

        PracticalOnly = settings.DefineSetting(
            "practical_only",
            true,
            () => "Practical items only",
            () => "Prefers liquid materials, upgrades, and consumables over vanity or one-off listings."
        );

        ApiKey = settings.DefineSetting(
            "api_key",
            string.Empty,
            () => "GW2 API key",
            () => "Optional API key with wallet, inventories, and tradingpost scopes for account-aware insights."
        );

        TopCount = settings.DefineSetting(
            "top_count",
            30,
            () => "Rows to show",
            () => "Controls how many candidates are shown in the overlay."
        );
        TopCount.SetRange(15, 100);

        MinimumProfitCopper = settings.DefineSetting(
            "minimum_profit_copper",
            500,
            () => "Min profit (copper)",
            () => "Filters out candidates below this estimated post-fee profit."
        );
        MinimumProfitCopper.SetRange(1, 50000);

        RefreshIntervalSeconds = settings.DefineSetting(
            "refresh_interval_seconds",
            300,
            () => "Refresh interval (seconds)",
            () => "How often the live scan refreshes while the overlay is open."
        );
        RefreshIntervalSeconds.SetRange(30, 1800);

        SortMode = settings.DefineSetting(
            "sort_mode",
            (int) FlipSortMode.Score,
            () => "Sort mode",
            () => "Controls whether candidates are ranked by score, profit, or spread."
        );
        SortMode.SetRange(0, 4);

        HistoryRetentionDays = settings.DefineSetting(
            "history_retention_days",
            14,
            () => "History retention (days)",
            () => "How many days of market snapshots to keep in the local history database."
        );
        HistoryRetentionDays.SetRange(1, 60);

        OpportunityMode = settings.DefineSetting(
            "opportunity_mode",
            (int) Gw2FlipOverlay.Models.OpportunityMode.Flip,
            () => "Opportunity mode",
            () => "Switches between flips, craft gains, and value deals versus recent market history."
        );
        OpportunityMode.SetRange(0, 4);

        MaxAcquireCostCopper = settings.DefineSetting(
            "max_acquire_cost_copper",
            200000,
            () => "Capital cap (copper)",
            () => "Maximum buy-in or craft cost for surfaced opportunities."
        );
        MaxAcquireCostCopper.SetRange(0, 5000000);

        MinimumMarketDepth = settings.DefineSetting(
            "minimum_market_depth",
            2000,
            () => "Minimum market depth",
            () => "Filters out thin items by requiring at least this much matched buy/sell depth."
        );
        MinimumMarketDepth.SetRange(0, 100000);

        MinimumDiscountPercent = settings.DefineSetting(
            "minimum_discount_percent",
            0,
            () => "Minimum discount (%)",
            () => "For value mode, require the current sell floor to be at least this far below recent fair value."
        );
        MinimumDiscountPercent.SetRange(0, 30);

        MinimumRoiPercent = settings.DefineSetting(
            "minimum_roi_percent",
            5,
            () => "Minimum ROI (%)",
            () => "Filters out low-return flips even if their copper profit is acceptable."
        );
        MinimumRoiPercent.SetRange(0, 50);

        WatchlistOnly = settings.DefineSetting(
            "watchlist_only",
            false,
            () => "Watchlist only",
            () => "Restrict suggestions to watched items or preset sniper lists."
        );

        MaxOwnedQuantity = settings.DefineSetting(
            "max_owned_quantity",
            100,
            () => "Max owned quantity",
            () => "Avoids suggesting more buys when you already own at least this many units."
        );
        MaxOwnedQuantity.SetRange(0, 10000);

        MaxOpenSellQuantity = settings.DefineSetting(
            "max_open_sell_quantity",
            50,
            () => "Max open sell quantity",
            () => "Avoids re-entry while you already have this much listed."
        );
        MaxOpenSellQuantity.SetRange(0, 5000);

        MaxVolatilityPercent = settings.DefineSetting(
            "max_volatility_percent",
            25,
            () => "Max volatility (%)",
            () => "Filters out items with unstable recent sell floors."
        );
        MaxVolatilityPercent.SetRange(0, 100);

        AutoFlipQuantity = settings.DefineSetting(
            "auto_flip_quantity",
            10,
            () => "Auto flip quantity",
            () => "Quantity per item used when copying the top-10 manual buy-order plan."
        );
        AutoFlipQuantity.SetRange(1, 250);

        ActivePresetId = settings.DefineSetting(
            "active_preset_id",
            "daily-volume",
            () => "Active preset",
            () => "The currently selected scan preset."
        );
    }
}
