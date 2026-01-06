using System.IO.Compression;
using System.Text;
using Microsoft.Data.SqlClient;
using SQL2Graph.Models;

namespace SQL2Graph.Services;

public class CsvExportService
{
    private readonly ILogger<CsvExportService> _logger;

    public CsvExportService(ILogger<CsvExportService> logger)
    {
        _logger = logger;
    }

    public async Task<Stream> ExportTablesToZipAsync(string connectionString, List<TableInfo> tables)
    {
        var memoryStream = new MemoryStream();
        
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var table in tables)
            {
                var entryName = $"{table.SchemaName}_{table.TableName}.csv";
                var entry = archive.CreateEntry(entryName);

                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false)); // UTF-8 without BOM

                try
                {
                    await WriteTableToCsvAsync(connectionString, table, writer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error exporting table {TableName}", table.FullName);
                    // Continue with other tables, or write error to CSV? 
                    // Let's just log and continue for now.
                }
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private async Task WriteTableToCsvAsync(string connectionString, TableInfo table, StreamWriter writer)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = $"SELECT * FROM {table.FullName}"; // Be mindful of SQL injection if TableInfo comes from untrusted source. Here it comes from SchemaReader which reads DB metadata.

        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        // Write Headers
        var columnCount = reader.FieldCount;
        var headers = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            headers[i] = EscapeCsv(reader.GetName(i));
        }
        await writer.WriteLineAsync(string.Join(",", headers));

        // Write Rows
        while (await reader.ReadAsync())
        {
            var line = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                var value = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();
                line[i] = EscapeCsv(value);
            }
            await writer.WriteLineAsync(string.Join(",", line));
        }
    }

    private string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            // Escape quotes by doubling them
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        
        return field;
    }
}
