using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a part/component in the inventory.
/// Stored in Cosmos DB "PartsInventory" container with partition key "category".
/// </summary>
public sealed class Part
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("partNumber")]
    [JsonProperty("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    [JsonProperty("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("quantityInStock")]
    [JsonProperty("quantityInStock")]
    public int QuantityInStock { get; set; }

    [JsonPropertyName("quantityReserved")]
    [JsonProperty("quantityReserved")]
    public int QuantityReserved { get; set; }

    [JsonPropertyName("reorderLevel")]
    [JsonProperty("reorderLevel")]
    public int ReorderLevel { get; set; }

    [JsonPropertyName("unitPrice")]
    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("supplier")]
    [JsonProperty("supplier")]
    public string Supplier { get; set; } = string.Empty;

    [JsonPropertyName("leadTimeDays")]
    [JsonProperty("leadTimeDays")]
    public int LeadTimeDays { get; set; }

    [JsonPropertyName("location")]
    [JsonProperty("location")]
    public string Location { get; set; } = string.Empty;
}
