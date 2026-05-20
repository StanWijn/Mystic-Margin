using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class RecipeCacheStore {

    private const int ChunkSize = 200;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
    private readonly string _cachePath;

    public RecipeCacheStore(string cachePath = null) {
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Guild Wars 2",
                "addons",
                "blishhud",
                "data",
                "Gw2FlipOverlay",
                "cache",
                "recipes.json.gz")
            : cachePath;

        var directory = Path.GetDirectoryName(_cachePath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IReadOnlyList<RecipeRecord>> GetRecipesAsync(HttpClient httpClient, CancellationToken cancellationToken) {
        if (File.Exists(_cachePath)) {
            var age = DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(_cachePath);

            if (age < CacheLifetime) {
                var cachedRecipes = await ReadCacheAsync(cancellationToken);

                if (cachedRecipes.Count > 0) {
                    return cachedRecipes;
                }
            }
        }

        var recipeIdsResponse = await httpClient.GetAsync("recipes", cancellationToken);
        recipeIdsResponse.EnsureSuccessStatusCode();
        var recipeIdsJson = await recipeIdsResponse.Content.ReadAsStringAsync();
        var recipeIds = JsonConvert.DeserializeObject<List<int>>(recipeIdsJson) ?? new List<int>();
        var recipes = new List<RecipeRecord>();

        foreach (var chunk in Chunk(recipeIds, ChunkSize)) {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await httpClient.GetAsync($"recipes?ids={string.Join(",", chunk)}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var chunkRecipes = JsonConvert.DeserializeObject<List<RecipeRecord>>(json) ?? new List<RecipeRecord>();
            recipes.AddRange(chunkRecipes);
        }

        await WriteCacheAsync(recipes, cancellationToken);
        return recipes;
    }

    private async Task<IReadOnlyList<RecipeRecord>> ReadCacheAsync(CancellationToken cancellationToken) {
        using (var stream = File.OpenRead(_cachePath))
        using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzipStream)) {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<List<RecipeRecord>>(json) ?? new List<RecipeRecord>();
        }
    }

    private async Task WriteCacheAsync(IReadOnlyList<RecipeRecord> recipes, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(_cachePath);

        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        using (var stream = File.Create(_cachePath))
        using (var gzipStream = new GZipStream(stream, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzipStream)) {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(JsonConvert.SerializeObject(recipes));
        }
    }

    private static IEnumerable<int[]> Chunk(IReadOnlyList<int> values, int chunkSize) {
        for (var i = 0; i < values.Count; i += chunkSize) {
            var length = Math.Min(chunkSize, values.Count - i);
            var chunk = new int[length];

            for (var offset = 0; offset < length; offset++) {
                chunk[offset] = values[i + offset];
            }

            yield return chunk;
        }
    }
}

public sealed class RecipeRecord {

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("output_item_id")]
    public int OutputItemId { get; set; }

    [JsonProperty("output_item_count")]
    public int OutputItemCount { get; set; }

    [JsonProperty("disciplines")]
    public List<string> Disciplines { get; set; } = new List<string>();

    [JsonProperty("ingredients")]
    public List<RecipeIngredientRecord> Ingredients { get; set; } = new List<RecipeIngredientRecord>();
}

public sealed class RecipeIngredientRecord {

    [JsonProperty("item_id")]
    public int ItemId { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }
}
