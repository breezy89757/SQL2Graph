namespace SQL2Graph.Models;

/// <summary>
/// Represents a Graph data model with nodes and relationships
/// </summary>
public class GraphModel
{
    public List<NodeType> Nodes { get; set; } = [];
    public List<RelationshipType> Relationships { get; set; } = [];
}

/// <summary>
/// Represents a node type (label) in the graph
/// </summary>
public class NodeType
{
    public string Label { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PropertyMapping> Properties { get; set; } = [];
    
    // For visualization
    public double X { get; set; }
    public double Y { get; set; }
    public string Color { get; set; } = "#3b82f6";
}

/// <summary>
/// Represents a property mapping from SQL column to Graph property
/// </summary>
public class PropertyMapping
{
    public string SqlColumn { get; set; } = string.Empty;
    public string GraphProperty { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsKey { get; set; }
}

/// <summary>
/// Represents a relationship type in the graph
/// </summary>
public class RelationshipType
{
    public string Type { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PropertyMapping> Properties { get; set; } = [];
    public bool IsJoinTable { get; set; }
}

/// <summary>
/// LLM analysis result containing the graph model and reasoning
/// </summary>
public class AnalysisResult
{
    public GraphModel GraphModel { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
    public string CypherDDL { get; set; } = string.Empty;
    public string CypherETL { get; set; } = string.Empty;
}
