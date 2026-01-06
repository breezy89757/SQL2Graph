using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using SQL2Graph.Models;
using System.Text.Json;

namespace SQL2Graph.Services;

/// <summary>
/// Service for LLM-based schema analysis and graph model generation
/// </summary>
public class LlmAnalysisService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LlmAnalysisService> _logger;
    private ChatClient? _chatClient;

    public LlmAnalysisService(IConfiguration config, ILogger<LlmAnalysisService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private ChatClient GetChatClient()
    {
        if (_chatClient != null) return _chatClient;

        var endpoint = _config["AzureOpenAI:Endpoint"];
        var apiKey = _config["AzureOpenAI:ApiKey"];
        var deploymentName = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(
                "請在 appsettings.json 中設定 AzureOpenAI:Endpoint 和 AzureOpenAI:ApiKey");
        }

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(deploymentName);
        return _chatClient;
    }

    /// <summary>
    /// Analyzes a SQL schema and generates a Graph model recommendation
    /// </summary>
    public async Task<AnalysisResult> AnalyzeSchemaAsync(SqlSchema schema)
    {
        return await AnalyzeSchemaAsync(schema, null);
    }

    /// <summary>
    /// Analyzes a SQL schema with sample data for better relationship inference
    /// </summary>
    public async Task<AnalysisResult> AnalyzeSchemaAsync(
        SqlSchema schema, 
        Dictionary<string, List<Dictionary<string, object?>>>? sampleData)
    {
        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var schemaJson = JsonSerializer.Serialize(schema, serializeOptions);
        
        var userPrompt = $"Analyze this SQL schema and generate a Neo4j graph model:\n\n## Schema\n{schemaJson}";

        // Add sample data if available
        if (sampleData != null && sampleData.Count > 0)
        {
            var sampleJson = JsonSerializer.Serialize(sampleData, serializeOptions);
            userPrompt += $"\n\n## Sample Data (use this to infer hidden relationships)\n{sampleJson}";
            _logger.LogInformation("Including sample data from {Count} tables", sampleData.Count);
        }

        var systemPrompt = GetSystemPrompt(sampleData != null);

        _logger.LogInformation("Sending schema to LLM for analysis...");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var chatClient = GetChatClient();
        var response = await chatClient.CompleteChatAsync(messages, options);
        var json = response.Value.Content[0].Text;

        _logger.LogInformation("Received LLM response");

        var deserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<AnalysisResult>(json, deserializeOptions)
            ?? throw new InvalidOperationException("Failed to parse LLM response");

        // Assign colors to nodes
        AssignNodeColors(result.GraphModel);

        return result;
    }

    private string GetSystemPrompt(bool hasSampleData = false)
    {
        var sampleDataRules = hasSampleData ? """

            ## Sample Data Analysis Rules:
            6. Examine sample data to find potential relationships NOT defined by FK constraints
            7. If column values in one table match values in another table's column, infer a relationship
            8. Look for patterns like "user_id", "product_code" that suggest foreign key-like relationships
            9. Note any inferred relationships in the reasoning, explaining how you discovered them
            """ : "";

        return $$"""
            You are an expert database architect specializing in graph database modeling.
            Your task is to analyze SQL schemas and convert them to Neo4j graph models.
            
            **IMPORTANT: All descriptions and reasoning MUST be in Traditional Chinese (繁體中文).**

            ## Analysis Rules:
            1. Tables with business meaning become Nodes (labels)
            2. Foreign Keys become Relationships
            3. Junction/Join tables (tables with mainly 2 FKs) become Relationships with properties
            4. Use semantic naming: 
               - Node labels should be singular PascalCase (e.g., "Person", "Product")
               - Relationship types should be SCREAMING_SNAKE_CASE verbs (e.g., "PURCHASED", "BELONGS_TO")
            5. Infer meaning from table/column names (e.g., "tbl_usr" -> "User", "created_at" -> "createdAt")
            {{sampleDataRules}}

            ## Response Format:
            Return a JSON object with this structure:
            {
                "graphModel": {
                    "nodes": [
                        {
                            "label": "Person",
                            "sourceTable": "dbo.Users",
                            "description": "代表系統中的使用者",
                            "properties": [
                                {"sqlColumn": "id", "graphProperty": "id", "dataType": "int", "isKey": true},
                                {"sqlColumn": "user_name", "graphProperty": "name", "dataType": "string", "isKey": false}
                            ]
                        }
                    ],
                    "relationships": [
                        {
                            "type": "PURCHASED",
                            "fromNode": "Person",
                            "toNode": "Product",
                            "sourceTable": "dbo.Orders",
                            "description": "使用者購買了產品",
                            "properties": [
                                {"sqlColumn": "quantity", "graphProperty": "quantity", "dataType": "int", "isKey": false}
                            ],
                            "isJoinTable": true
                        }
                    ]
                },
                "reasoning": "Explanation of your design decisions...",
                "cypherDDL": "CREATE CONSTRAINT ... CREATE INDEX ...",
                "cypherETL": "LOAD CSV ... CREATE (n:Person) ..."
            }

            Be thorough and professional. Generate production-ready Cypher scripts.
            """;
    }

    private void AssignNodeColors(GraphModel model)
    {
        var colors = new[]
        {
            "#3b82f6", // blue
            "#10b981", // green
            "#f59e0b", // amber
            "#ef4444", // red
            "#8b5cf6", // purple
            "#ec4899", // pink
            "#06b6d4", // cyan
            "#84cc16"  // lime
        };

        for (int i = 0; i < model.Nodes.Count; i++)
        {
            model.Nodes[i].Color = colors[i % colors.Length];
            // Initial positions in a circle
            var angle = 2 * Math.PI * i / model.Nodes.Count;
            model.Nodes[i].X = 400 + 200 * Math.Cos(angle);
            model.Nodes[i].Y = 300 + 200 * Math.Sin(angle);
        }
    }
}
