using Microsoft.Data.SqlClient;
using SQL2Graph.Models;

namespace SQL2Graph.Services;

/// <summary>
/// Service for reading SQL Server database schema metadata
/// </summary>
public class SchemaReaderService
{
    private readonly ILogger<SchemaReaderService> _logger;

    public SchemaReaderService(ILogger<SchemaReaderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads the complete schema from a SQL Server database
    /// </summary>
    public async Task<SqlSchema> ReadSchemaAsync(string connectionString)
    {
        var schema = new SqlSchema();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        schema.DatabaseName = connection.Database;

        // Read tables and columns
        schema.Tables = await ReadTablesAsync(connection);

        // Read foreign keys
        schema.ForeignKeys = await ReadForeignKeysAsync(connection);

        // Mark FK columns
        foreach (var fk in schema.ForeignKeys)
        {
            var table = schema.Tables.FirstOrDefault(t => t.FullName == fk.FromTable || t.TableName == fk.FromTable);
            var column = table?.Columns.FirstOrDefault(c => c.ColumnName == fk.FromColumn);
            if (column != null)
            {
                column.IsForeignKey = true;
            }
        }

        _logger.LogInformation("Read schema: {TableCount} tables, {FkCount} foreign keys", 
            schema.Tables.Count, schema.ForeignKeys.Count);

        return schema;
    }

    private async Task<List<TableInfo>> ReadTablesAsync(SqlConnection connection)
    {
        var tables = new List<TableInfo>();

        const string query = @"
            SELECT 
                t.TABLE_SCHEMA,
                t.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.TABLES t
            INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
                ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                AND c.TABLE_NAME = pk.TABLE_NAME 
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

        await using var cmd = new SqlCommand(query, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        TableInfo? currentTable = null;

        while (await reader.ReadAsync())
        {
            var schemaName = reader.GetString(0);
            var tableName = reader.GetString(1);
            var fullName = $"{schemaName}.{tableName}";

            if (currentTable == null || currentTable.FullName != fullName)
            {
                currentTable = new TableInfo
                {
                    SchemaName = schemaName,
                    TableName = tableName
                };
                tables.Add(currentTable);
            }

            var column = new ColumnInfo
            {
                ColumnName = reader.GetString(2),
                DataType = reader.GetString(3),
                IsNullable = reader.GetString(4) == "YES",
                MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsPrimaryKey = reader.GetInt32(6) == 1
            };

            if (column.IsPrimaryKey)
            {
                currentTable.PrimaryKeyColumn = column.ColumnName;
            }

            currentTable.Columns.Add(column);
        }

        return tables;
    }

    private async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(SqlConnection connection)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        const string query = @"
            SELECT 
                fk.name AS ConstraintName,
                SCHEMA_NAME(tp.schema_id) + '.' + tp.name AS FromTable,
                cp.name AS FromColumn,
                SCHEMA_NAME(tr.schema_id) + '.' + tr.name AS ToTable,
                cr.name AS ToColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
            INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
            INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
            INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
            ORDER BY fk.name";

        await using var cmd = new SqlCommand(query, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                FromTable = reader.GetString(1),
                FromColumn = reader.GetString(2),
                ToTable = reader.GetString(3),
                ToColumn = reader.GetString(4)
            });
        }

        return foreignKeys;
    }

    /// <summary>
    /// Tests the connection to the database
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return (true, $"Connected to {connection.Database}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Reads sample data from tables using TABLESAMPLE for efficient random sampling
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="tables">Tables to sample from</param>
    /// <param name="sampleSize">Number of rows to sample per table (default 5)</param>
    /// <returns>Dictionary of table name to list of sample rows</returns>
    public async Task<Dictionary<string, List<Dictionary<string, object?>>>> ReadSampleDataAsync(
        string connectionString, 
        List<TableInfo> tables, 
        int sampleSize = 5)
    {
        var samples = new Dictionary<string, List<Dictionary<string, object?>>>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var table in tables)
        {
            try
            {
                // Use TABLESAMPLE for efficient random sampling
                // TABLESAMPLE works at page level, so we request more rows than needed
                // and use TOP to limit to exact count
                var query = $@"
                    SELECT TOP {sampleSize} * 
                    FROM [{table.SchemaName}].[{table.TableName}] 
                    TABLESAMPLE (1000 ROWS)
                    ORDER BY NEWID()";

                await using var cmd = new SqlCommand(query, connection);
                cmd.CommandTimeout = 10; // Don't wait too long for large tables
                
                await using var reader = await cmd.ExecuteReaderAsync();
                
                var rows = new List<Dictionary<string, object?>>();
                
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        
                        // Truncate long strings for LLM context
                        if (value is string str && str.Length > 100)
                        {
                            value = str.Substring(0, 100) + "...";
                        }
                        
                        row[columnName] = value;
                    }
                    rows.Add(row);
                }
                
                samples[table.FullName] = rows;
                _logger.LogInformation("Sampled {Count} rows from {Table}", rows.Count, table.FullName);
            }
            catch (Exception ex)
            {
                // Log but continue - some tables might be empty or have issues
                _logger.LogWarning(ex, "Failed to sample from {Table}", table.FullName);
                samples[table.FullName] = [];
            }
        }

        return samples;
    }
}

