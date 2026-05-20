# GW2 Flip Overlay

Small Blish HUD module scaffold for showing the best Trading Post flip candidates in-game.

## What is included

- A Blish HUD module project targeting `.NET Framework 4.7.2`
- A floating overlay window with:
  - item
  - buy or craft cost
  - lowest sell
  - estimated profit
  - ROI
  - market depth
  - demand pressure
  - turnover score
  - fast flip score
- A right-side inspect panel with:
  - copy item name
  - copy `/wiki` command
  - open wiki page
  - deeper per-item turnover stats
- A corner icon that toggles the overlay window
- A live public GW2 commerce scan using:
  - `/v2/commerce/prices`
  - `/v2/items`
- In-overlay controls for:
  - opportunity mode (`Flip` / `Craft` / `Value` / `Cooldown` / `Investment`)
  - sort mode
  - row count
  - capital cap
  - minimum depth
  - minimum profit threshold
  - practical-item filtering
- A larger local candidate universe cache so filter/sort changes feel instant after a scan finishes
- A local compressed price-history database that stores every public market scan over time
- A cached last-scan universe so the overlay can show prior results immediately on startup
- A mock data provider fallback so the UI is still testable without live API data

## Project layout

- `src/Gw2FlipOverlay/Gw2FlipOverlay.csproj`
- `src/Gw2FlipOverlay/manifest.json`
- `src/Gw2FlipOverlay/FlipOverlayModule.cs`
- `src/Gw2FlipOverlay/ModuleSettings.cs`
- `src/Gw2FlipOverlay/Models/FlipCandidate.cs`
- `src/Gw2FlipOverlay/Services/FlipScoringService.cs`
- `src/Gw2FlipOverlay/Services/IMarketDataProvider.cs`
- `src/Gw2FlipOverlay/Services/MockMarketDataProvider.cs`
- `src/Gw2FlipOverlay/Services/Gw2CommerceDataProvider.cs`
- `src/Gw2FlipOverlay/UI/FlipOverlayWindow.cs`

## Notes

- The live scan is intentionally read-only and does not automate any in-game actions.
- The current MVP uses public market data only. It does not yet wire in `/v2/commerce/transactions`.
- Auto-refresh only runs while the overlay window is visible to keep API traffic and module work modest.
- Sort/filter controls now run against the last downloaded candidate universe locally, so changing rows, caps, profit, or depth should feel much faster than a full re-scan.
- Price history is stored under:

  `Documents\Guild Wars 2\addons\blishhud\data\Gw2FlipOverlay\price-history`

- Last rendered shortlist cache is stored under:

  `Documents\Guild Wars 2\addons\blishhud\data\Gw2FlipOverlay\cache\last-scan.json`

- Cached recipe data for craft-profit scans is stored under:

  `Documents\Guild Wars 2\addons\blishhud\data\Gw2FlipOverlay\cache\recipes.json.gz`

- Each refresh writes a compressed snapshot of all `/commerce/prices` rows so we can build trend analysis on top later.
- The default ranking is now a turnover-first heuristic:

`fast_flip_score = estimated_profit * volume_score * turnover_score * liquidity_score * stability_score`

- Default presets are intentionally conservative and focused on capital that can turn over:
  - `Starter Volume`: cheap, steady-depth materials and consumables for building rhythm
  - `Daily Volume`: stronger repeatable flips once more wallet capital is available
  - `Craft Margin`: crafts that still clear fees after buying missing materials
  - `Deep Value`: clear discounts against local scan history
  - `Daily Cooldowns`: limited-throughput daily craft conversions with high margins
  - `Seasonal Watch`: discounted festival/rotation items only, not broad speculation

## Build

1. Install Visual Studio 2022 Community or Build Tools with:
   - `.NET desktop development`
   - `.NET Framework 4.7.2 targeting pack`
2. Project instruction for this repo:

   `Always build with -Install after each implementation pass so the latest addon is immediately available in Blish HUD for testing.`

3. Run:

   `powershell -ExecutionPolicy Bypass -File .\scripts\Build-BlishModule.ps1 -Install`

4. The build now automatically creates:

   `src\Gw2FlipOverlay\bin\Release\Gw2FlipOverlay.bhm`

5. The same command also copies it directly into Blish HUD's local modules folder:

   `powershell -ExecutionPolicy Bypass -File .\scripts\Build-BlishModule.ps1 -Install`

## Good next steps

1. Add item icons from `/v2/items`.
2. Add authenticated `/v2/commerce/transactions` history.
3. Add watchlists and favorites.
4. Persist pinned items and sorting preferences.
