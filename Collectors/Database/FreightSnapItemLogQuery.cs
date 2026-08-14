using System.Data;
using Microsoft.Data.SqlClient;

namespace Systems_One_MQTT_Service.Collectors.Database;

/// <summary>
/// Query implementation for the Freight Snap Ingest Service database schema (FreightScans).
/// Columns: Id, Barcode, ScanDateTime, Length, Width, Height, Volume, Weight, CsvPath, ProcessedAt.
/// Related table: FreightScanImages (FreightScanId FK) — has no completion/sent flags of its own,
/// so "image sent" here means "at least one matched image row exists for the scan".
/// Date column for windowing: ScanDateTime.
/// </summary>
public static class FreightSnapItemLogQuery
{
    public static async Task<Dictionary<string, int>> ExecuteWindowSummaryAsync(
        SqlConnection connection,
        string tableName,
        DateTime startLocal,
        DateTime endLocal,
        CancellationToken cancellationToken)
    {
        var safeTable = ItemLogQuery.ValidateTableName(tableName);
        using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"
            SELECT
                COUNT(DISTINCT fs.Id) AS Total_Items,
                COUNT(DISTINCT CASE WHEN fs.Length IS NULL OR fs.Width IS NULL OR fs.Height IS NULL THEN fs.Id END) AS No_Dimension,
                COUNT(DISTINCT CASE WHEN fs.Weight IS NULL THEN fs.Id END) AS No_Weight,
                COUNT(DISTINCT img.FreightScanId) AS Image_Sent
            FROM [{safeTable}] fs
            LEFT JOIN FreightScanImages img ON img.FreightScanId = fs.Id
            WHERE fs.ScanDateTime >= @StartLocal AND fs.ScanDateTime < @EndLocal";

        cmd.Parameters.Add(new SqlParameter("@StartLocal", SqlDbType.DateTime) { Value = startLocal });
        cmd.Parameters.Add(new SqlParameter("@EndLocal", SqlDbType.DateTime) { Value = endLocal });

        var result = new Dictionary<string, int>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            int total     = reader["Total_Items"]  is DBNull ? 0 : Convert.ToInt32(reader["Total_Items"]);
            int noDim     = reader["No_Dimension"] is DBNull ? 0 : Convert.ToInt32(reader["No_Dimension"]);
            int noWeight  = reader["No_Weight"]    is DBNull ? 0 : Convert.ToInt32(reader["No_Weight"]);
            int imgSent   = reader["Image_Sent"]   is DBNull ? 0 : Convert.ToInt32(reader["Image_Sent"]);

            result["Total_Items"]    = total;
            result["No_Dimension"]   = noDim;
            result["No_Weight"]      = noWeight;
            result["Image_Sent"]     = imgSent;
            result["Image_Not_Sent"] = total - imgSent;
        }

        return result;
    }
}
