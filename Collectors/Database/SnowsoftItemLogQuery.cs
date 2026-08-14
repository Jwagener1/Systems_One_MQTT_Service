using System.Data;
using Microsoft.Data.SqlClient;

namespace Systems_One_MQTT_Service.Collectors.Database;

/// <summary>
/// Query implementation for the Snowsoft database schema (tbl_Scanned_Items).
/// Columns: ID, Item_Date_Time, Barcode, Sent, Image_Sent, No_Read, Complete
/// </summary>
public static class SnowsoftItemLogQuery
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
                SUM(CASE WHEN CAST(No_Read AS INT) = 1 THEN 1 ELSE 0 END) AS No_Read,
                SUM(CASE WHEN CAST(Sent AS INT) = 1 THEN 1 ELSE 0 END) AS Data_Sent,
                SUM(CASE WHEN CAST(Image_Sent AS INT) = 1 THEN 1 ELSE 0 END) AS Image_Sent,
                SUM(CASE WHEN CAST(Complete AS INT) = 1 THEN 1 ELSE 0 END) AS Complete
            FROM [{safeTable}]
            WHERE Item_Date_Time >= @StartLocal AND Item_Date_Time < @EndLocal";

        cmd.Parameters.Add(new SqlParameter("@StartLocal", SqlDbType.DateTime) { Value = startLocal });
        cmd.Parameters.Add(new SqlParameter("@EndLocal", SqlDbType.DateTime) { Value = endLocal });

        var result = new Dictionary<string, int>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            int total    = reader["Total_Items"] is DBNull ? 0 : Convert.ToInt32(reader["Total_Items"]);
            int noRead   = reader["No_Read"]     is DBNull ? 0 : Convert.ToInt32(reader["No_Read"]);
            int dataSent = reader["Data_Sent"]   is DBNull ? 0 : Convert.ToInt32(reader["Data_Sent"]);
            int imgSent  = reader["Image_Sent"]  is DBNull ? 0 : Convert.ToInt32(reader["Image_Sent"]);
            int complete = reader["Complete"]    is DBNull ? 0 : Convert.ToInt32(reader["Complete"]);

            result["Total_Items"]    = total;
            result["No_Read"]        = noRead;
            result["Good_Read"]      = total - noRead;
            result["Data_Sent"]      = dataSent;
            result["Not_Sent"]       = total - dataSent;
            result["Image_Sent"]     = imgSent;
            result["Image_Not_Sent"] = total - imgSent;
            result["Complete"]       = complete;
        }

        return result;
    }
}
