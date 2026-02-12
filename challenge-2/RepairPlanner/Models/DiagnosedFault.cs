using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a diagnosed fault from the Fault Diagnosis Agent.
/// This is the input to the Repair Planner Agent.
/// </summary>
public sealed class DiagnosedFault
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("faultType")]
    [JsonProperty("faultType")]
    public string FaultType { get; set; } = string.Empty;

    [JsonPropertyName("rootCause")]
    [JsonProperty("rootCause")]
    public string RootCause { get; set; } = string.Empty;

    [JsonPropertyName("machineName")]
    [JsonProperty("machineName")]
    public string MachineName { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    [JsonProperty("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("recommendedActions")]
    [JsonProperty("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();

    [JsonPropertyName("detectedAt")]
    [JsonProperty("detectedAt")]
    public DateTime DetectedAt { get; set; }

    [JsonPropertyName("diagnosedAt")]
    [JsonProperty("diagnosedAt")]
    public DateTime DiagnosedAt { get; set; }
}
