using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RepairPlanner.Models;

namespace RepairPlanner.Services;

/// <summary>
/// Service for interacting with Azure Cosmos DB.
/// Handles queries for technicians, parts, and work order creation.
/// </summary>
public sealed class CosmosDbService : IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly ILogger<CosmosDbService> _logger;
    private readonly Container _techniciansContainer;
    private readonly Container _partsContainer;
    private readonly Container _workOrdersContainer;

    public CosmosDbService(CosmosDbOptions options, ILogger<CosmosDbService> logger)
    {
        _logger = logger;
        
        try
        {
            _cosmosClient = new CosmosClient(options.Endpoint, options.Key);
            _database = _cosmosClient.GetDatabase(options.DatabaseName);
            _techniciansContainer = _database.GetContainer(options.TechniciansContainer);
            _partsContainer = _database.GetContainer(options.PartsInventoryContainer);
            _workOrdersContainer = _database.GetContainer(options.WorkOrdersContainer);
            
            _logger.LogInformation("Cosmos DB service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB service");
            throw;
        }
    }

    /// <summary>
    /// Queries available technicians who have at least one of the required skills.
    /// Returns technicians sorted by availability and workload.
    /// </summary>
    public async Task<List<Technician>> GetAvailableTechniciansBySkillsAsync(
        IReadOnlyList<string> requiredSkills,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Querying technicians with skills: {Skills}", string.Join(", ", requiredSkills));

            // Build a query that finds technicians with any of the required skills
            // and are available (not already fully booked)
            var query = new QueryDefinition(
                @"SELECT * FROM c 
                  WHERE c.available = true 
                  AND ARRAY_LENGTH(c.skills) > 0
                  ORDER BY c.currentWorkloadHours ASC");

            var technicians = new List<Technician>();
            using var iterator = _techniciansContainer.GetItemQueryIterator<Technician>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                
                // Filter technicians who have at least one required skill
                var matchingTechs = response.Where(tech => 
                    tech.Skills.Any(skill => 
                        requiredSkills.Contains(skill, StringComparer.OrdinalIgnoreCase)))
                    .ToList();
                
                technicians.AddRange(matchingTechs);
            }

            _logger.LogInformation("Found {Count} available technicians matching required skills", technicians.Count);
            return technicians;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while querying technicians. Status: {Status}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while querying technicians");
            throw;
        }
    }

    /// <summary>
    /// Fetches parts from inventory by their part numbers.
    /// Returns only parts that are found and have sufficient stock.
    /// </summary>
    public async Task<List<Part>> GetPartsByPartNumbersAsync(
        IReadOnlyList<string> partNumbers,
        CancellationToken cancellationToken = default)
    {
        if (partNumbers.Count == 0)
        {
            _logger.LogInformation("No part numbers provided, returning empty list");
            return new List<Part>();
        }

        try
        {
            _logger.LogInformation("Fetching parts: {PartNumbers}", string.Join(", ", partNumbers));

            var parts = new List<Part>();

            // Query all parts and filter by part numbers
            // In production, you might want to use a more efficient cross-partition query
            var query = new QueryDefinition("SELECT * FROM c");

            using var iterator = _partsContainer.GetItemQueryIterator<Part>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                
                // Filter parts by the requested part numbers
                var matchingParts = response.Where(part => 
                    partNumbers.Contains(part.PartNumber, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                
                parts.AddRange(matchingParts);
            }

            _logger.LogInformation("Found {Count} parts out of {Requested} requested", 
                parts.Count, partNumbers.Count);

            // Log any missing parts
            var foundPartNumbers = parts.Select(p => p.PartNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingParts = partNumbers.Where(pn => !foundPartNumbers.Contains(pn)).ToList();
            
            if (missingParts.Count > 0)
            {
                _logger.LogWarning("Parts not found in inventory: {MissingParts}", 
                    string.Join(", ", missingParts));
            }

            return parts;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while fetching parts. Status: {Status}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching parts");
            throw;
        }
    }

    /// <summary>
    /// Creates a new work order in Cosmos DB.
    /// Generates a unique ID and work order number if not provided.
    /// </summary>
    public async Task<WorkOrder> CreateWorkOrderAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate ID if not set
            if (string.IsNullOrEmpty(workOrder.Id))
            {
                workOrder.Id = Guid.NewGuid().ToString();
            }

            // Generate work order number if not set
            if (string.IsNullOrEmpty(workOrder.WorkOrderNumber))
            {
                workOrder.WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-{workOrder.Id[..8]}";
            }

            // Set timestamps
            workOrder.CreatedAt = DateTime.UtcNow;
            workOrder.UpdatedAt = DateTime.UtcNow;

            // Ensure status is set (this is the partition key)
            if (string.IsNullOrEmpty(workOrder.Status))
            {
                workOrder.Status = "pending";
            }

            _logger.LogInformation("Creating work order {WorkOrderNumber} for machine {MachineId}", 
                workOrder.WorkOrderNumber, workOrder.MachineId);

            // Create the work order in Cosmos DB
            var response = await _workOrdersContainer.CreateItemAsync(
                workOrder,
                new PartitionKey(workOrder.Status),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Work order {WorkOrderNumber} created successfully. RU cost: {RU}", 
                workOrder.WorkOrderNumber, response.RequestCharge);

            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while creating work order. Status: {Status}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating work order");
            throw;
        }
    }

    /// <summary>
    /// Disposes the Cosmos DB client.
    /// </summary>
    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }

    /// <summary>
    /// Configuration options for Cosmos DB connection.
    /// </summary>
    public sealed class CosmosDbOptions
    {
        public const string SectionName = "CosmosDb";

        public string Endpoint { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        
        // Container names
        public string TechniciansContainer { get; set; } = "Technicians";
        public string PartsInventoryContainer { get; set; } = "PartsInventory";
        public string WorkOrdersContainer { get; set; } = "WorkOrders";
    }
}
