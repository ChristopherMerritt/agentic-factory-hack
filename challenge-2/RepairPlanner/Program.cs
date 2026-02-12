using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairPlanner;
using RepairPlanner.Agents;
using RepairPlanner.Models;
using RepairPlanner.Services;

/// <summary>
/// Main program class for the Repair Planner Agent.
/// Uses .NET Generic Host for configuration, dependency injection, and lifecycle management.
/// Supports layered configuration from appsettings.json, environment variables, and command-line args.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            // Display help if requested
            if (args.Contains("-h") || args.Contains("--help"))
            {
                Console.WriteLine("=== Repair Planner Agent ===");
                Console.WriteLine();
                Console.WriteLine("Usage: RepairPlanner [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  -t, --test    Run test scenarios");
                Console.WriteLine("  -h, --help    Display this help message");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  dotnet run              # Run as a service");
                Console.WriteLine("  dotnet run -- -t        # Run tests");
                Console.WriteLine("  dotnet run -- --help    # Show this help");
                Console.WriteLine();
                return 0;
            }

            // Run tests if explicitly requested
            if (args.Contains("-t") || args.Contains("--test"))
            {
                var host = CreateHostBuilder(args).Build();
                return await RunTests(host.Services);
            }

            // Default: Run as a hosted service
            await CreateHostBuilder(args).Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Creates and configures the host builder.
    /// Uses CreateDefaultBuilder which automatically:
    /// - Loads appsettings.json and appsettings.{Environment}.json
    /// - Loads environment variables
    /// - Loads command-line arguments
    /// - Sets up logging
    /// - Sets up dependency injection
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        IHostBuilder result = Host
            .CreateDefaultBuilder(args);

        result
            //.UseEnvironment("Development") //use this to set environment when running as a windows service
            .ConfigureLogging(ConfigureLogging)
            .ConfigureServices(ConfigureServices);

        return result;
    }

    /// <summary>
    /// Configures logging for the application.
    /// </summary>
    private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logging)
    {
        // Additional logging configuration can be added here
        // Default configuration is already loaded from appsettings.json
    }

    /// <summary>
    /// Configures dependency injection services using IOptions pattern.
    /// The IConfiguration is automatically populated by the host from:
    /// 1. appsettings.json
    /// 2. appsettings.{Environment}.json
    /// 3. Environment variables (overrides appsettings)
    /// 4. Command-line arguments (overrides all)
    /// </summary>
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Register Cosmos DB service using extension method
        services.UseCosmosDbService(context.Configuration);

        // Register Fault Mapping service using extension method
        services.UseFaultMappingService(context.Configuration);

        // Register Repair Planner Agent using extension method
        // This also registers AIProjectClient internally
        services.UseRepairPlannerAgent(context.Configuration);
    }

    /// <summary>
    /// Runs the test scenarios using the configured services.
    /// </summary>
    private static async Task<int> RunTests(IServiceProvider services)
    {
        // IOptions pattern: access configuration through dependency injection
        var agentConfig = services.GetRequiredService<IOptions<RepairPlannerAgent.AgentOptions>>().Value;
        var cosmosConfig = services.GetRequiredService<IOptions<CosmosDbService.CosmosDbOptions>>().Value;
        var logger = services.GetRequiredService<ILogger<Program>>();

        Console.WriteLine("=== Repair Planner Agent ===");
        Console.WriteLine($"Project Endpoint: {agentConfig.ProjectEndpoint}");
        Console.WriteLine($"Model: {agentConfig.ModelDeploymentName}");
        Console.WriteLine($"Cosmos Database: {cosmosConfig.DatabaseName}");
        Console.WriteLine();

        var agent = services.GetRequiredService<RepairPlannerAgent>();

        // Register the agent
        logger.LogInformation("Registering Repair Planner Agent...");
        await agent.EnsureAgentVersionAsync();
        logger.LogInformation("Agent registered successfully");
        Console.WriteLine();

        // Run test scenarios
        // Add or comment out scenarios as needed
        await TestCuringTemperatureFault(agent, logger);
        await TestBuildingDrumVibration(agent, logger);
        await TestLoadCellDrift(agent, logger);
        await TestPriorityCalculation(agent, logger);
        await TestNoTechnicianAvailable(agent, logger);

        logger.LogInformation("All repair planning tests completed successfully");
        return 0;
    }

    /// <summary>
    /// Test scenario: Curing temperature excessive fault.
    /// Tests high-severity fault with multiple required skills and parts.
    /// </summary>
    private static async Task TestCuringTemperatureFault(
        RepairPlannerAgent agent, 
        ILogger logger)
    {
        Console.WriteLine("--- Test Scenario 1: Curing Temperature Excessive ---");
        Console.WriteLine();

        var fault = new DiagnosedFault
        {
            Id = Guid.NewGuid().ToString(),
            MachineId = "TCP-001",
            MachineName = "Tire Curing Press #1",
            FaultType = "curing_temperature_excessive",
            Severity = "high",
            Description = "Curing temperature exceeds acceptable limits, causing rubber degradation",
            RootCause = "Heating element malfunction or temperature sensor calibration drift",
            RecommendedActions = new List<string>
            {
                "Inspect and test heating elements",
                "Calibrate temperature sensors",
                "Replace faulty components"
            },
            DetectedAt = DateTime.UtcNow.AddHours(-2),
            DiagnosedAt = DateTime.UtcNow.AddHours(-1)
        };

        await ProcessFaultAndDisplayResult(agent, logger, fault);
    }

    /// <summary>
    /// Test scenario: Building drum vibration fault.
    /// Tests medium-severity mechanical fault.
    /// </summary>
    private static async Task TestBuildingDrumVibration(
        RepairPlannerAgent agent, 
        ILogger logger)
    {
        Console.WriteLine("--- Test Scenario 2: Building Drum Vibration ---");
        Console.WriteLine();

        var fault = new DiagnosedFault
        {
            Id = Guid.NewGuid().ToString(),
            MachineId = "TBM-002",
            MachineName = "Tire Building Machine #2",
            FaultType = "building_drum_vibration",
            Severity = "medium",
            Description = "Excessive vibration detected in building drum during operation",
            RootCause = "Bearing wear or drum imbalance",
            RecommendedActions = new List<string>
            {
                "Perform vibration analysis",
                "Inspect and replace bearings",
                "Balance building drum"
            },
            DetectedAt = DateTime.UtcNow.AddHours(-3),
            DiagnosedAt = DateTime.UtcNow.AddHours(-2)
        };

        await ProcessFaultAndDisplayResult(agent, logger, fault);
    }

    /// <summary>
    /// Test scenario: Load cell drift fault.
    /// Tests low-severity calibration issue.
    /// </summary>
    private static async Task TestLoadCellDrift(
        RepairPlannerAgent agent, 
        ILogger logger)
    {
        Console.WriteLine("--- Test Scenario 3: Load Cell Drift ---");
        Console.WriteLine();

        var fault = new DiagnosedFault
        {
            Id = Guid.NewGuid().ToString(),
            MachineId = "TUM-003",
            MachineName = "Tire Uniformity Machine #3",
            FaultType = "load_cell_drift",
            Severity = "low",
            Description = "Load cell readings drifting outside calibration range",
            RootCause = "Sensor calibration drift or environmental factors",
            RecommendedActions = new List<string>
            {
                "Recalibrate load cells",
                "Check environmental conditions",
                "Replace sensors if necessary"
            },
            DetectedAt = DateTime.UtcNow.AddDays(-1),
            DiagnosedAt = DateTime.UtcNow.AddHours(-6)
        };

        await ProcessFaultAndDisplayResult(agent, logger, fault);
    }

    /// <summary>
    /// Test scenario: Priority calculation verification.
    /// Tests that work order priority correctly reflects fault severity.
    /// </summary>
    private static async Task TestPriorityCalculation(
        RepairPlannerAgent agent,
        ILogger logger)
    {
        Console.WriteLine("--- Test Scenario 4: Priority Calculation Verification ---");
        Console.WriteLine();

        var severities = new[] { "critical", "high", "medium", "low" };
        var results = new List<(string Severity, string Priority, bool Passed)>();

        foreach (var severity in severities)
        {
            logger.LogInformation("Testing severity: {Severity}", severity);

            var fault = new DiagnosedFault
            {
                Id = Guid.NewGuid().ToString(),
                MachineId = "TEST-001",
                MachineName = "Test Machine",
                FaultType = "curing_temperature_excessive",
                Severity = severity,
                Description = $"Test fault with {severity} severity",
                RootCause = "Test scenario",
                RecommendedActions = new List<string> { "Test action" },
                DetectedAt = DateTime.UtcNow.AddHours(-1),
                DiagnosedAt = DateTime.UtcNow
            };

            var workOrder = await agent.PlanAndCreateWorkOrderAsync(fault);
            
            // Verify priority matches or exceeds severity
            var expectedPriority = severity; // Should be at least this priority
            var actualPriority = workOrder.Priority;
            var passed = actualPriority == expectedPriority;

            results.Add((severity, actualPriority, passed));

            Console.WriteLine($"  Severity: {severity,-10} → Priority: {actualPriority,-10} [{(passed ? "✓ PASS" : "✗ FAIL")}]");
        }

        Console.WriteLine();
        var allPassed = results.All(r => r.Passed);
        
        if (allPassed)
        {
            logger.LogInformation("✓ All priority calculation tests PASSED");
            Console.WriteLine("✓ All priority calculations are correct!");
        }
        else
        {
            logger.LogWarning("✗ Some priority calculation tests FAILED");
            Console.WriteLine("✗ Priority calculation verification failed!");
            foreach (var result in results.Where(r => !r.Passed))
            {
                Console.WriteLine($"  Expected: {result.Severity}, Got: {result.Priority}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    /// <summary>
    /// Test scenario: No technician available.
    /// Tests that work order is created with warning when no qualified technicians exist.
    /// </summary>
    private static async Task TestNoTechnicianAvailable(
        RepairPlannerAgent agent,
        ILogger logger)
    {
        Console.WriteLine("--- Test Scenario 5: No Technician Available ---");
        Console.WriteLine();

        // Use a hypothetical fault type with skills that no technician is likely to have
        // This simulates a specialized repair scenario
        var fault = new DiagnosedFault
        {
            Id = Guid.NewGuid().ToString(),
            MachineId = "SPECIALTY-999",
            MachineName = "Specialized Equipment",
            FaultType = "unknown_specialized_fault",
            Severity = "high",
            Description = "Rare fault requiring specialized skills not available in current staff",
            RootCause = "Requires specialist technician",
            RecommendedActions = new List<string>
            {
                "Contact external specialist",
                "Arrange contractor visit"
            },
            DetectedAt = DateTime.UtcNow.AddHours(-1),
            DiagnosedAt = DateTime.UtcNow
        };

        logger.LogInformation("Testing scenario with no available technicians...");
        Console.WriteLine();

        var workOrder = await agent.PlanAndCreateWorkOrderAsync(fault);

        // Verify the behavior when no technician is available
        var hasNoAssignment = string.IsNullOrEmpty(workOrder.AssignedTo);
        var hasWarningNote = workOrder.Notes?.Contains("No qualified technician available") ?? false;

        Console.WriteLine();
        Console.WriteLine("=== Verification Results ===");
        Console.WriteLine($"Work Order: {workOrder.WorkOrderNumber}");
        Console.WriteLine($"Machine: {workOrder.MachineId}");
        Console.WriteLine($"Assigned To: {workOrder.AssignedTo ?? "Unassigned"} [{(hasNoAssignment ? "✓ PASS" : "✗ FAIL")}]");
        Console.WriteLine($"Has Warning Note: {(hasWarningNote ? "Yes" : "No")} [{(hasWarningNote ? "✓ PASS" : "✗ FAIL")}]");
        Console.WriteLine();

        if (hasNoAssignment && hasWarningNote)
        {
            logger.LogInformation("✓ No technician available test PASSED");
            Console.WriteLine("✓ Work order correctly created without assignment and with warning!");
        }
        else
        {
            logger.LogWarning("✗ No technician available test FAILED");
            Console.WriteLine("✗ Test failed - expected unassigned work order with warning note");
            if (!hasNoAssignment)
                Console.WriteLine("  - Work order was assigned when no qualified technician exists");
            if (!hasWarningNote)
                Console.WriteLine("  - Warning note was not added");
        }

        Console.WriteLine();
        if (!string.IsNullOrEmpty(workOrder.Notes))
        {
            Console.WriteLine("Notes:");
            Console.WriteLine($"  {workOrder.Notes}");
            Console.WriteLine();
        }

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    /// <summary>
    /// Processes a fault and displays the resulting work order.
    /// Common workflow for all test scenarios.
    /// </summary>
    private static async Task ProcessFaultAndDisplayResult(
        RepairPlannerAgent agent,
        ILogger logger,
        DiagnosedFault fault)
    {
        logger.LogInformation("Processing fault: {FaultType} on {MachineName}", 
            fault.FaultType, fault.MachineName);
        Console.WriteLine();

        logger.LogInformation("Generating repair plan...");
        var workOrder = await agent.PlanAndCreateWorkOrderAsync(fault);

        DisplayWorkOrder(workOrder);

        logger.LogInformation("Test scenario completed");
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
    }

    /// <summary>
    /// Displays a work order in a readable format.
    /// </summary>
    private static void DisplayWorkOrder(WorkOrder workOrder)
    {
        Console.WriteLine();
        Console.WriteLine("=== Work Order Created ===");
        Console.WriteLine($"Work Order: {workOrder.WorkOrderNumber}");
        Console.WriteLine($"Machine: {workOrder.MachineId}");
        Console.WriteLine($"Title: {workOrder.Title}");
        Console.WriteLine($"Priority: {workOrder.Priority}");
        Console.WriteLine($"Type: {workOrder.Type}");
        Console.WriteLine($"Assigned To: {workOrder.AssignedTo ?? "Unassigned"}");
        Console.WriteLine($"Estimated Duration: {workOrder.EstimatedDuration} minutes");
        Console.WriteLine($"Tasks: {workOrder.Tasks.Count}");
        Console.WriteLine($"Parts: {workOrder.PartsUsed.Count}");
        Console.WriteLine();

        if (workOrder.Tasks.Count > 0)
        {
            Console.WriteLine("Tasks:");
            foreach (var task in workOrder.Tasks.OrderBy(t => t.Sequence))
            {
                Console.WriteLine($"  {task.Sequence}. {task.Title} ({task.EstimatedDurationMinutes} min)");
            }
            Console.WriteLine();
        }

        if (workOrder.PartsUsed.Count > 0)
        {
            Console.WriteLine("Parts:");
            foreach (var part in workOrder.PartsUsed)
            {
                Console.WriteLine($"  - {part.PartNumber} (Qty: {part.Quantity})");
            }
            Console.WriteLine();
        }

        if (!string.IsNullOrEmpty(workOrder.Notes))
        {
            Console.WriteLine("Notes:");
            Console.WriteLine($"  {workOrder.Notes}");
            Console.WriteLine();
        }
    }
}
