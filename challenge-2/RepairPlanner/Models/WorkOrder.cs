using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace RepairPlanner.Models;

/// <summary>
/// Represents a complete work order for repairing a machine fault.
/// This is the output of the Repair Planner Agent.
/// Stored in Cosmos DB "WorkOrders" container with partition key "status".
/// </summary>
public sealed class WorkOrder
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("workOrderNumber")]
    [JsonProperty("workOrderNumber")]
    public string WorkOrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("machineId")]
    [JsonProperty("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty; // "corrective" | "preventive" | "emergency"

    [JsonPropertyName("priority")]
    [JsonProperty("priority")]
    public string Priority { get; set; } = string.Empty; // "critical" | "high" | "medium" | "low"

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; } = "pending"; // partition key

    [JsonPropertyName("assignedTo")]
    [JsonProperty("assignedTo")]
    public string? AssignedTo { get; set; } // technician id or null

    [JsonPropertyName("createdAt")]
    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("estimatedDuration")]
    [JsonProperty("estimatedDuration")]
    public int EstimatedDuration { get; set; } // in minutes

    [JsonPropertyName("tasks")]
    [JsonProperty("tasks")]
    public List<RepairTask> Tasks { get; set; } = new();

    [JsonPropertyName("partsUsed")]
    [JsonProperty("partsUsed")]
    public List<WorkOrderPartUsage> PartsUsed { get; set; } = new();

    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;
}
