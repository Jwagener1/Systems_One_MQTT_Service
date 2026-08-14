using System.Data;
using Microsoft.Data.SqlClient;

namespace Systems_One_MQTT_Service.Collectors.Database;

/// <summary>
/// Query implementation for the Twinsaver database schema (tbl_Line_Data).
/// Columns: ID, Barcode_Scanned, Item_No, Pallet_No, Quantity, Description,
///          Print_Date, Line_No, Product_GTIN, Uploaded, Printed, No_read, No_Data
/// Date column for windowing: Print_Date.
/// </summary>
public static class TwinsaverItemLogQuery
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
                COUNT(*) AS Total_Items,
                SUM(CASE WHEN CAST(No_read AS INT) = 1 THEN 1 ELSE 0 END) AS No_Read,
                SUM(CASE WHEN CAST(Printed AS INT) = 1 THEN 1 ELSE 0 END) AS Printed,
                SUM(CASE WHEN CAST(Uploaded AS INT) = 1 THEN 1 ELSE 0 END) AS Data_Sent,
                SUM(CASE WHEN CAST(No_Data AS INT) = 1 THEN 1 ELSE 0 END) AS No_Data
            FROM [{safeTable}]
            WHERE Print_Date >= @StartLocal AND Print_Date < @EndLocal";

        cmd.Parameters.Add(new SqlParameter("@StartLocal", SqlDbType.DateTime) { Value = startLocal });
        cmd.Parameters.Add(new SqlParameter("@EndLocal", SqlDbType.DateTime) { Value = endLocal });

        var result = new Dictionary<string, int>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            int total    = reader["Total_Items"] is DBNull ? 0 : Convert.ToInt32(reader["Total_Items"]);
            int noRead   = reader["No_Read"]     is DBNull ? 0 : Convert.ToInt32(reader["No_Read"]);
            int printed  = reader["Printed"]     is DBNull ? 0 : Convert.ToInt32(reader["Printed"]);
            int dataSent = reader["Data_Sent"]   is DBNull ? 0 : Convert.ToInt32(reader["Data_Sent"]);
            int noData   = reader["No_Data"]     is DBNull ? 0 : Convert.ToInt32(reader["No_Data"]);

            result["Total_Items"]    = total;
            result["No_Read"]        = noRead;
            result["Good_Read"]      = total - noRead;
            result["Printed"]        = printed;
            result["Not_Printed"]    = total - printed;
            result["Data_Sent"]      = dataSent;
            result["Not_Sent"]       = total - dataSent;
            result["No_Data"]        = noData;
        }

        return result;
    }
}
