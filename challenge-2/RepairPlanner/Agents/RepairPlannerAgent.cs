using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;
using RepairPlanner.Services;

namespace RepairPlanner.Agents;

/// <summary>
/// Main agent that orchestrates the repair planning workflow.
/// Uses the Foundry Agents SDK to generate comprehensive repair plans.
/// </summary>
public sealed class RepairPlannerAgent
{
    private readonly AIProjectClient _projectClient;
    private readonly CosmosDbService _cosmosDb;
    private readonly IFaultMappingService _faultMapping;
    private readonly string _modelDeploymentName;
    private readonly ILogger<RepairPlannerAgent> _logger;

    public RepairPlannerAgent(
        AIProjectClient projectClient,
        CosmosDbService cosmosDb,
        IFaultMappingService faultMapping,
        AgentOptions options,
        ILogger<RepairPlannerAgent> logger)
    {
        _projectClient = projectClient;
        _cosmosDb = cosmosDb;
        _faultMapping = faultMapping;
        _modelDeploymentName = options.ModelDeploymentName;
        _logger = logger;
    }

    private const string AgentName = "RepairPlannerAgent";
    
    // System instructions for the AI agent
    // Simplified for structured output - JSON schema enforces the structure
    private const string AgentInstructions = """
        You are a Repair Planner Agent for tire manufacturing equipment.
        Generate comprehensive repair plans with detailed tasks, timelines, and resource allocation.
        
        Key responsibilities:
        - Analyze the diagnosed fault and required skills/parts
        - Select the most qualified available technician based on their skills and workload
        - Create ordered, actionable repair tasks with accurate time estimates
        - Include relevant parts from inventory; use empty array if none needed
        - Set priority based on fault severity (critical/high/medium/low)
        - Set type based on repair nature: "corrective" (reactive), "preventive" (scheduled), "emergency" (critical)
        - Add safety notes for all tasks and general notes for coordination needs
        
        The response will be validated against a strict JSON schema.
        """;

    // JSON serialization options for parsing LLM responses
    // AllowReadingFromString handles cases where LLM returns numbers as strings
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Ensures the agent is registered in Azure AI Foundry with JSON mode.
    /// Call this once at startup before invoking the agent.
    /// Uses JSON mode for structured responses.
    /// </summary>
    public async Task EnsureAgentVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registering {AgentName} with model {Model} in JSON mode", AgentName, _modelDeploymentName);
            
            // Generate JSON schema documentation for the prompt
            var schemaInfo = AIJsonUtilities.CreateJsonSchema(
                typeof(WorkOrder),
                serializerOptions: JsonOptions
            );
            
            _logger.LogInformation("Generated JSON schema for WorkOrder type");
            
            // Enhanced instructions with schema information
            var enhancedInstructions = $"""
                {AgentInstructions}
                
                Required JSON Schema:
                {schemaInfo}
                
                Respond with ONLY valid JSON matching this exact schema.
                """;
            
            var definition = new PromptAgentDefinition(model: _modelDeploymentName)
            {
                Instructions = enhancedInstructions
            };
            
            await _projectClient.Agents.CreateAgentVersionAsync(
                AgentName, 
                new AgentVersionCreationOptions(definition), 
                cancellationToken);
            
            _logger.LogInformation("Agent {AgentName} registered successfully with JSON schema guidance", AgentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register agent {AgentName}", AgentName);
            throw;
        }
    }

    /// <summary>
    /// Plans and creates a work order for a diagnosed fault.
    /// This is the main entry point for the repair planning workflow.
    /// </summary>
    public async Task<WorkOrder> PlanAndCreateWorkOrderAsync(
        DiagnosedFault fault,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Planning repair for fault {FaultId} on machine {MachineId}", 
            fault.Id, fault.MachineId);

        try
        {
            // Step 1: Get required skills and parts from mapping service
            var requiredSkills = _faultMapping.GetRequiredSkills(fault.FaultType);
            var requiredPartNumbers = _faultMapping.GetRequiredParts(fault.FaultType);
            
            _logger.LogInformation("Fault {FaultType} requires {SkillCount} skills and {PartCount} parts",
                fault.FaultType, requiredSkills.Count, requiredPartNumbers.Count);

            // Step 2: Query available technicians and parts from Cosmos DB
            var techniciansTask = _cosmosDb.GetAvailableTechniciansBySkillsAsync(requiredSkills, cancellationToken);
            var partsTask = _cosmosDb.GetPartsByPartNumbersAsync(requiredPartNumbers, cancellationToken);
            
            await Task.WhenAll(techniciansTask, partsTask);
            
            var technicians = await techniciansTask;
            var parts = await partsTask;

            // Step 2a: Validate technician availability
            if (technicians.Count == 0)
            {
                _logger.LogWarning(
                    "No technicians available with required skills: {Skills}. Work order will be created as unassigned and require manual assignment.",
                    string.Join(", ", requiredSkills));
            }
            else
            {
                _logger.LogInformation("Found {Count} qualified technician(s) available", technicians.Count);
            }

            // Step 2b: Validate parts availability
            if (requiredPartNumbers.Count > 0 && parts.Count == 0)
            {
                _logger.LogWarning(
                    "None of the required parts are in inventory: {PartNumbers}. Work order will proceed but parts must be ordered.",
                    string.Join(", ", requiredPartNumbers));
            }
            else if (parts.Count < requiredPartNumbers.Count)
            {
                var missingCount = requiredPartNumbers.Count - parts.Count;
                _logger.LogWarning(
                    "{MissingCount} of {TotalCount} required parts are not in inventory",
                    missingCount, requiredPartNumbers.Count);
            }

            // Step 3: Build the prompt with all context
            var prompt = BuildPrompt(fault, technicians, parts, requiredSkills, requiredPartNumbers);
            
            _logger.LogInformation("Invoking AI agent with structured output to generate repair plan");

            // Step 4: Invoke the AI agent with structured output
            var agent = _projectClient.GetAIAgent(name: AgentName);
            var response = await agent.RunAsync(prompt, thread: null, options: null, cancellationToken);
            
            var responseText = response.Text ?? throw new InvalidOperationException("Agent returned empty response");
            
            _logger.LogInformation("Structured response received ({Length} chars)", responseText.Length);
            _logger.LogDebug("Agent response: {Response}", responseText);

            // Step 5: Parse structured JSON response (guaranteed to match schema)
            var workOrder = ParseStructuredWorkOrderResponse(responseText);
            
            // Step 6: Apply defaults and validate
            ApplyDefaults(workOrder, fault);
            
            // Step 7: Save to Cosmos DB
            var savedWorkOrder = await _cosmosDb.CreateWorkOrderAsync(workOrder, cancellationToken);
            
            _logger.LogInformation("Work order {WorkOrderNumber} created successfully for machine {MachineId}", 
                savedWorkOrder.WorkOrderNumber, savedWorkOrder.MachineId);

            return savedWorkOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to plan and create work order for fault {FaultId}", fault.Id);
            throw;
        }
    }

    /// <summary>
    /// Builds the prompt for the AI agent with all necessary context.
    /// </summary>
    private string BuildPrompt(
        DiagnosedFault fault,
        List<Technician> technicians,
        List<Part> parts,
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> requiredPartNumbers)
    {
        var technicianInfo = technicians.Count > 0
            ? string.Join("\n", technicians.Select(t => 
                $"- {t.Name} (ID: {t.Id}): Skills: [{string.Join(", ", t.Skills)}], " +
                $"Workload: {t.CurrentWorkloadHours}h, Available: {t.Available}"))
            : "⚠️ WARNING: No technicians currently available with required skills. Work order must be assigned manually.";

        var partsInfo = parts.Count > 0
            ? string.Join("\n", parts.Select(p => 
                $"- {p.PartNumber} - {p.Name}: Stock: {p.QuantityInStock}, " +
                $"Location: {p.Location}, Price: ${p.UnitPrice}"))
            : "⚠️ WARNING: Required parts not in inventory and must be ordered before repair can begin.";

        return $"""
            DIAGNOSED FAULT:
            - Machine: {fault.MachineName} ({fault.MachineId})
            - Fault Type: {fault.FaultType}
            - Severity: {fault.Severity}
            - Description: {fault.Description}
            - Root Cause: {fault.RootCause}
            - Recommended Actions: {string.Join("; ", fault.RecommendedActions)}
            - Detected: {fault.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC

            REQUIRED SKILLS:
            {string.Join(", ", requiredSkills)}

            AVAILABLE TECHNICIANS:
            {technicianInfo}

            REQUIRED PARTS:
            {string.Join(", ", requiredPartNumbers)}

            PARTS IN INVENTORY:
            {partsInfo}

            Generate a comprehensive repair plan as JSON following the schema provided in your instructions.
            """;
    }

    /// <summary>
    /// Parses the structured JSON response from the AI agent into a WorkOrder object.
    /// With structured output, the response is guaranteed to match the JSON schema.
    /// No markdown cleanup needed as response is pure JSON.
    /// </summary>
    private WorkOrder ParseStructuredWorkOrderResponse(string responseText)
    {
        try
        {
            // With structured output, response is guaranteed to be valid JSON matching our schema
            var workOrder = JsonSerializer.Deserialize<WorkOrder>(responseText, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize work order");

            _logger.LogInformation(
                "Successfully parsed structured work order: {Tasks} tasks, {Parts} parts",
                workOrder.Tasks.Count,
                workOrder.PartsUsed.Count
            );
            return workOrder;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse structured response. Response: {Response}", responseText);
            throw new InvalidOperationException("Structured output response was invalid (this should not happen)", ex);
        }
    }

    /// <summary>
    /// Calculates priority from fault severity.
    /// Maps severity levels to corresponding priority levels.
    /// </summary>
    private static string CalculatePriorityFromSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => "critical",
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            _ => "medium" // default for unknown severity
        };
    }

    /// <summary>
    /// Ensures the calculated priority is at least as high as the minimum priority.
    /// Priority order: critical > high > medium > low
    /// </summary>
    private static string EnsureMinimumPriority(string? currentPriority, string minimumPriority)
    {
        // Priority ranking (higher number = higher priority)
        var priorityRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["low"] = 1,
            ["medium"] = 2,
            ["high"] = 3,
            ["critical"] = 4
        };

        var currentRank = !string.IsNullOrEmpty(currentPriority) && priorityRank.ContainsKey(currentPriority)
            ? priorityRank[currentPriority]
            : 0;

        var minimumRank = priorityRank.TryGetValue(minimumPriority, out var minRank) ? minRank : 2;

        // Return the higher priority
        return currentRank >= minimumRank ? currentPriority! : minimumPriority;
    }

    /// <summary>
    /// Applies default values to the work order and ensures required fields are set.
    /// </summary>
    private void ApplyDefaults(WorkOrder workOrder, DiagnosedFault fault)
    {
        // Ensure machine ID matches the fault
        workOrder.MachineId = fault.MachineId;

        // Calculate priority based on fault severity
        var calculatedPriority = CalculatePriorityFromSeverity(fault.Severity);
        
        // If LLM set a priority, ensure it's at least as high as severity suggests
        // Otherwise, use the calculated priority
        workOrder.Priority = EnsureMinimumPriority(workOrder.Priority, calculatedPriority);
        
        _logger.LogInformation("Priority set to {Priority} based on severity {Severity}", 
            workOrder.Priority, fault.Severity);

        // Apply default values if not set by LLM
        // ??= means "assign if null or empty" (like Python: x = x or default_value)
        workOrder.Status ??= "pending";
        workOrder.Type ??= "corrective";
        workOrder.Notes ??= string.Empty;
        
        // Add note if no technician was assigned
        if (string.IsNullOrEmpty(workOrder.AssignedTo))
        {
            var noTechNote = "⚠️ No qualified technician available. Manual assignment required.";
            workOrder.Notes = string.IsNullOrEmpty(workOrder.Notes) 
                ? noTechNote 
                : $"{workOrder.Notes}\n\n{noTechNote}";
            _logger.LogWarning("Work order created without assigned technician");
        }
        
        // Ensure lists are not null
        workOrder.Tasks ??= new List<RepairTask>();
        workOrder.PartsUsed ??= new List<WorkOrderPartUsage>();

        // Validate critical fields
        if (string.IsNullOrEmpty(workOrder.Title))
        {
            workOrder.Title = $"Repair for {fault.FaultType}";
        }

        if (string.IsNullOrEmpty(workOrder.Description))
        {
            workOrder.Description = fault.Description;
        }

        _logger.LogInformation("Applied defaults: Priority={Priority}, Type={Type}, Status={Status}, Tasks={TaskCount}, Parts={PartCount}",
            workOrder.Priority, workOrder.Type, workOrder.Status, workOrder.Tasks.Count, workOrder.PartsUsed.Count);
    }

    /// <summary>
    /// Configuration options for the Repair Planner Agent.
    /// </summary>
    public sealed class AgentOptions
    {
        public const string SectionName = "RepairPlannerAgent";

        public string ProjectEndpoint { get; set; } = string.Empty;
        public string ModelDeploymentName { get; set; } = string.Empty;
    }
}
