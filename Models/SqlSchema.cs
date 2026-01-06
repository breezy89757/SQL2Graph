namespace SQL2Graph.Models;

/// <summary>
/// Represents the complete schema of a SQL database
/// </summary>
public class SqlSchema
{
    public string DatabaseName { get; set; } = string.Empty;
    public List<TableInfo> Tables { get; set; } = [];
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = [];
}

/// <summary>
/// Represents a table in the SQL database
/// </summary>
public class TableInfo
{
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = [];
    public string? PrimaryKeyColumn { get; set; }
    
    public string FullName => $"{SchemaName}.{TableName}";
}

/// <summary>
/// Represents a column in a table
/// </summary>
public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public int? MaxLength { get; set; }
}

/// <summary>
/// Represents a foreign key relationship
/// </summary>
public class ForeignKeyInfo
{
    public string ConstraintName { get; set; } = string.Empty;
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
}
