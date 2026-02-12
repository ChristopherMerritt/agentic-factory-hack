using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a maintenance technician with their skills and availability.
/// Stored in Cosmos DB "Technicians" container with partition key "department".
/// </summary>
public sealed class Technician
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    [JsonProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    [JsonProperty("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("certifications")]
    [JsonProperty("certifications")]
    public List<string> Certifications { get; set; } = new();

    [JsonPropertyName("available")]
    [JsonProperty("available")]
    public bool Available { get; set; }

    [JsonPropertyName("currentWorkloadHours")]
    [JsonProperty("currentWorkloadHours")]
    public int CurrentWorkloadHours { get; set; }

    [JsonPropertyName("shiftSchedule")]
    [JsonProperty("shiftSchedule")]
    public string ShiftSchedule { get; set; } = string.Empty;
}
