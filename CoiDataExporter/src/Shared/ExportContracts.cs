using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CoiDataExporter.Contracts
{
    /// <summary>
    /// Versioned, game-independent export contract. Keep this model free of game DLL types.
    /// </summary>
    public sealed class ExportDocument
    {
        [JsonProperty("schema_version", Order = 0)]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("game_version", Order = 1)]
        public string GameVersion { get; set; } = "unknown";

        [JsonProperty("exported_at_utc", Order = 2)]
        public DateTime ExportedAtUtc { get; set; }

        [JsonProperty("products", Order = 10)]
        public List<ProductDto> Products { get; set; } = new List<ProductDto>();

        [JsonProperty("recipes", Order = 20)]
        public List<RecipeDto> Recipes { get; set; } = new List<RecipeDto>();

        [JsonProperty("buildings", Order = 30)]
        public List<BuildingDto> Buildings { get; set; } = new List<BuildingDto>();
    }

    public sealed class ProductDto
    {
        [JsonProperty("id", Order = 0)]
        public string Id { get; set; }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("state", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public string State { get; set; }

        [JsonProperty("is_storable", Order = 3)]
        public bool IsStorable { get; set; }

        [JsonProperty("is_trash", Order = 4)]
        public bool IsTrash { get; set; }

        [JsonProperty("radioactivity", Order = 5)]
        public double Radioactivity { get; set; }
    }

    public sealed class RecipeDto
    {
        [JsonProperty("id", Order = 0)]
        public string Id { get; set; }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("duration_seconds", Order = 2)]
        public double DurationSeconds { get; set; }

        [JsonProperty("inputs", Order = 3)]
        public List<ProductQuantityDto> Inputs { get; set; } = new List<ProductQuantityDto>();

        [JsonProperty("outputs", Order = 4)]
        public List<ProductQuantityDto> Outputs { get; set; } = new List<ProductQuantityDto>();
    }

    public sealed class ProductQuantityDto
    {
        [JsonProperty("product_id", Order = 0)]
        public string ProductId { get; set; }

        [JsonProperty("product_name", Order = 1)]
        public string ProductName { get; set; }

        [JsonProperty("amount", Order = 2)]
        public double Amount { get; set; }
    }

    public sealed class BuildingDto
    {
        [JsonProperty("id", Order = 0)]
        public string Id { get; set; }

        [JsonProperty("name", Order = 1)]
        public string Name { get; set; }

        [JsonProperty("category_id", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
        public string CategoryId { get; set; }

        [JsonProperty("category_name", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
        public string CategoryName { get; set; }

        [JsonProperty("is_farm", Order = 4)]
        public bool IsFarm { get; set; }

        [JsonProperty("is_storage", Order = 5)]
        public bool IsStorage { get; set; }

        [JsonProperty("is_mine", Order = 6)]
        public bool IsMine { get; set; }

        [JsonProperty("workers", Order = 10)]
        public int Workers { get; set; }

        [JsonProperty("maintenance_product_id", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
        public string MaintenanceProductId { get; set; }

        [JsonProperty("maintenance_per_month", Order = 12)]
        public double MaintenancePerMonth { get; set; }

        [JsonProperty("electricity_consumed", Order = 13)]
        public double ElectricityConsumed { get; set; }

        [JsonProperty("electricity_generated", Order = 14)]
        public double ElectricityGenerated { get; set; }

        [JsonProperty("storage_capacity", Order = 15)]
        public double StorageCapacity { get; set; }

        [JsonProperty("transfer_speed_per_minute", Order = 16)]
        public double TransferSpeedPerMinute { get; set; }

        [JsonProperty("research_speed", Order = 17)]
        public double ResearchSpeed { get; set; }

        [JsonProperty("build_costs", Order = 20)]
        public List<ProductQuantityDto> BuildCosts { get; set; } = new List<ProductQuantityDto>();

        [JsonProperty("recipe_ids", Order = 21)]
        public List<string> RecipeIds { get; set; } = new List<string>();
    }
}
