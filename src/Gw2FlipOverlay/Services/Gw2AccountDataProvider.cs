using Gw2FlipOverlay.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class Gw2AccountDataProvider : IAccountDataProvider, IDisposable {

    private readonly HttpClient _httpClient;

    public Gw2AccountDataProvider() {
        _httpClient = new HttpClient() {
            BaseAddress = new Uri("https://api.guildwars2.com/v2/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string SourceName => "live account sync";

    public async Task<AccountSnapshot> GetSnapshotAsync(string apiKey, CancellationToken cancellationToken) {
        var snapshot = new AccountSnapshot() {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            HasApiKey = !string.IsNullOrWhiteSpace(apiKey)
        };

        if (string.IsNullOrWhiteSpace(apiKey)) {
            snapshot.StatusMessage = "Add a GW2 API key with trading post, wallet, inventories, and characters scopes to unlock account-aware insights.";
            return snapshot;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        try {
            var walletTask = GetAsync<List<WalletCurrencyDto>>("account/wallet", cancellationToken);
            var bankTask = GetAsync<List<AccountItemDto>>("account/bank", cancellationToken);
            var materialsTask = GetAsync<List<AccountItemDto>>("account/materials", cancellationToken);
            var sharedInventoryTask = GetAsync<List<AccountItemDto>>("account/inventory", cancellationToken);
            var currentBuysTask = GetAsync<List<CommerceTransactionDto>>("commerce/transactions/current/buys", cancellationToken);
            var currentSellsTask = GetAsync<List<CommerceTransactionDto>>("commerce/transactions/current/sells", cancellationToken);
            var historicalBuysTask = GetAsync<List<CommerceTransactionDto>>("commerce/transactions/history/buys", cancellationToken);
            var historicalSellsTask = GetAsync<List<CommerceTransactionDto>>("commerce/transactions/history/sells", cancellationToken);

            await Task.WhenAll(walletTask, bankTask, materialsTask, sharedInventoryTask, currentBuysTask, currentSellsTask, historicalBuysTask, historicalSellsTask).ConfigureAwait(false);

            string characterInventoryStatus = null;
            string itemNameStatus = null;
            Dictionary<int, int> characterInventoryCounts = null;
            try {
                characterInventoryCounts = await GetCharacterInventoryCountsAsync(cancellationToken).ConfigureAwait(false);
                if (characterInventoryCounts.Count > 0) {
                    characterInventoryStatus = "Character bags included in holdings.";
                }
            } catch (Exception) {
                characterInventoryStatus = "Character bag inventory unavailable. Add the characters permission to include bag items in holdings.";
            }

            snapshot.IsAuthenticated = true;
            snapshot.AvailableCopper = walletTask.Result.FirstOrDefault(currency => currency.Id == 1)?.Value ?? 0;
            snapshot.OwnedCounts = BuildOwnedCountMap(bankTask.Result, materialsTask.Result, sharedInventoryTask.Result);
            MergeOwnedCounts(snapshot.OwnedCounts, characterInventoryCounts);
            snapshot.OrderByItemId = BuildOrderMap(currentBuysTask.Result, currentSellsTask.Result);
            snapshot.HistoricalBuys = historicalBuysTask.Result.Select(row => row.ToRecord(false, false)).ToList();
            snapshot.HistoricalSells = historicalSellsTask.Result.Select(row => row.ToRecord(true, false)).ToList();

            try {
                snapshot.ItemNames = await FetchItemNamesAsync(BuildAccountItemIds(snapshot), cancellationToken).ConfigureAwait(false);
            } catch (Exception) {
                snapshot.ItemNames = new Dictionary<int, string>();
                itemNameStatus = "Item-name lookup unavailable; scanned market rows can still provide names.";
            }

            var syncDetails = new[] { characterInventoryStatus, itemNameStatus }
                .Where(detail => !string.IsNullOrWhiteSpace(detail));
            var detailText = string.Join(" ", syncDetails);
            snapshot.StatusMessage = string.IsNullOrWhiteSpace(detailText)
                ? "Authenticated account sync loaded wallet, holdings, TP orders, and transaction history."
                : $"Authenticated account sync loaded wallet, holdings, TP orders, and transaction history. {detailText}";
        } catch (Exception ex) {
            snapshot.IsAuthenticated = false;
            snapshot.StatusMessage = $"Authenticated sync failed: {ex.Message}";
        } finally {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        return snapshot;
    }

    public void Dispose() {
        _httpClient.Dispose();
    }

    private static Dictionary<int, int> BuildOwnedCountMap(params List<AccountItemDto>[] sources) {
        var counts = new Dictionary<int, int>();

        foreach (var source in sources) {
            foreach (var item in source ?? new List<AccountItemDto>()) {
                if (item == null || item.Id <= 0 || item.Count <= 0) {
                    continue;
                }

                counts[item.Id] = counts.TryGetValue(item.Id, out var currentCount)
                    ? currentCount + item.Count
                    : item.Count;
            }
        }

        return counts;
    }

    private static Dictionary<int, ItemOrderSnapshot> BuildOrderMap(List<CommerceTransactionDto> buys, List<CommerceTransactionDto> sells) {
        var orders = new Dictionary<int, ItemOrderSnapshot>();

        foreach (var buy in buys ?? new List<CommerceTransactionDto>()) {
            if (buy.ItemId <= 0) {
                continue;
            }

            if (!orders.TryGetValue(buy.ItemId, out var snapshot)) {
                snapshot = new ItemOrderSnapshot() { ItemId = buy.ItemId };
                orders[buy.ItemId] = snapshot;
            }

            snapshot.CurrentBuyQuantity += buy.Quantity;
            snapshot.CurrentBuyTotalCopper += (long)buy.Price * buy.Quantity;
        }

        foreach (var sell in sells ?? new List<CommerceTransactionDto>()) {
            if (sell.ItemId <= 0) {
                continue;
            }

            if (!orders.TryGetValue(sell.ItemId, out var snapshot)) {
                snapshot = new ItemOrderSnapshot() { ItemId = sell.ItemId };
                orders[sell.ItemId] = snapshot;
            }

            snapshot.CurrentSellQuantity += sell.Quantity;
            snapshot.CurrentSellTotalCopper += (long)sell.Price * sell.Quantity;
        }

        foreach (var snapshot in orders.Values) {
            if (snapshot.CurrentBuyQuantity > 0 && snapshot.CurrentBuyTotalCopper > 0) {
                snapshot.CurrentBuyUnitPrice = (int)Math.Round(snapshot.CurrentBuyTotalCopper / (decimal)snapshot.CurrentBuyQuantity);
            }

            if (snapshot.CurrentSellQuantity > 0 && snapshot.CurrentSellTotalCopper > 0) {
                snapshot.CurrentSellUnitPrice = (int)Math.Round(snapshot.CurrentSellTotalCopper / (decimal)snapshot.CurrentSellQuantity);
            }
        }

        return orders;
    }

    private async Task<Dictionary<int, int>> GetCharacterInventoryCountsAsync(CancellationToken cancellationToken) {
        var counts = new Dictionary<int, int>();
        var characterNames = await GetAsync<List<string>>("characters", cancellationToken).ConfigureAwait(false);

        foreach (var characterName in characterNames ?? new List<string>()) {
            if (string.IsNullOrWhiteSpace(characterName)) {
                continue;
            }

            var inventory = await GetAsync<CharacterInventoryDto>($"characters/{Uri.EscapeDataString(characterName)}/inventory", cancellationToken).ConfigureAwait(false);
            MergeOwnedCounts(counts, BuildOwnedCountMapFromBags(inventory?.Bags));
        }

        return counts;
    }

    private static Dictionary<int, int> BuildOwnedCountMapFromBags(List<CharacterBagDto> bags) {
        var counts = new Dictionary<int, int>();

        foreach (var bag in bags ?? new List<CharacterBagDto>()) {
            foreach (var item in bag?.Inventory ?? new List<AccountItemDto>()) {
                if (item == null || item.Id <= 0 || item.Count <= 0) {
                    continue;
                }

                counts[item.Id] = counts.TryGetValue(item.Id, out var currentCount)
                    ? currentCount + item.Count
                    : item.Count;
            }
        }

        return counts;
    }

    private static void MergeOwnedCounts(Dictionary<int, int> target, Dictionary<int, int> source) {
        if (target == null || source == null || source.Count == 0) {
            return;
        }

        foreach (var pair in source) {
            if (pair.Key <= 0 || pair.Value <= 0) {
                continue;
            }

            target[pair.Key] = target.TryGetValue(pair.Key, out var existing)
                ? existing + pair.Value
                : pair.Value;
        }
    }

    private static IReadOnlyList<int> BuildAccountItemIds(AccountSnapshot snapshot) {
        var ids = new HashSet<int>();

        foreach (var itemId in snapshot.OwnedCounts?.Keys ?? Enumerable.Empty<int>()) {
            ids.Add(itemId);
        }

        foreach (var itemId in snapshot.OrderByItemId?.Keys ?? Enumerable.Empty<int>()) {
            ids.Add(itemId);
        }

        foreach (var itemId in snapshot.HistoricalBuys?.Select(row => row.ItemId) ?? Enumerable.Empty<int>()) {
            ids.Add(itemId);
        }

        foreach (var itemId in snapshot.HistoricalSells?.Select(row => row.ItemId) ?? Enumerable.Empty<int>()) {
            ids.Add(itemId);
        }

        return ids.Where(itemId => itemId > 0).OrderBy(itemId => itemId).ToList();
    }

    private async Task<Dictionary<int, string>> FetchItemNamesAsync(IReadOnlyList<int> itemIds, CancellationToken cancellationToken) {
        var names = new Dictionary<int, string>();

        foreach (var batch in Batch(itemIds ?? Array.Empty<int>(), 200)) {
            if (batch.Count == 0) {
                continue;
            }

            var items = await GetAsync<List<ItemDto>>($"items?ids={string.Join(",", batch)}", cancellationToken).ConfigureAwait(false);

            foreach (var item in items ?? new List<ItemDto>()) {
                if (item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name)) {
                    names[item.Id] = item.Name;
                }
            }
        }

        return names;
    }

    private static IEnumerable<List<int>> Batch(IReadOnlyList<int> values, int size) {
        var batch = new List<int>(size);

        foreach (var value in values ?? Array.Empty<int>()) {
            batch.Add(value);

            if (batch.Count < size) {
                continue;
            }

            yield return batch;
            batch = new List<int>(size);
        }

        if (batch.Count > 0) {
            yield return batch;
        }
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken) where T : new() {
        var response = await _httpClient.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(content) ?? new T();
    }

    private sealed class WalletCurrencyDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; }
    }

    private sealed class AccountItemDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    private sealed class ItemDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    private sealed class CommerceTransactionDto {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("item_id")]
        public int ItemId { get; set; }

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("created")]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [JsonProperty("purchased")]
        public DateTimeOffset? FulfilledAtUtc { get; set; }

        public CommerceTransactionRecord ToRecord(bool isSell, bool isCurrent) {
            return new CommerceTransactionRecord() {
                Id = Id,
                ItemId = ItemId,
                Price = Price,
                Quantity = Quantity,
                CreatedAtUtc = CreatedAtUtc,
                FulfilledAtUtc = FulfilledAtUtc,
                IsSell = isSell,
                IsCurrent = isCurrent
            };
        }
    }

    private sealed class CharacterBagDto {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("inventory")]
        public List<AccountItemDto> Inventory { get; set; } = new List<AccountItemDto>();
    }

    private sealed class CharacterInventoryDto {
        [JsonProperty("bags")]
        public List<CharacterBagDto> Bags { get; set; } = new List<CharacterBagDto>();
    }
}
