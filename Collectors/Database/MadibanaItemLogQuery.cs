using System.Data;
using Microsoft.Data.SqlClient;

namespace Systems_One_MQTT_Service.Collectors.Database;

/// <summary>
/// Query implementation for the Madibana database schema (tbl_Measurement).
/// Columns: ID, Item_Date_Time, Barcode, Length, Width, Height, Weight, Volume,
///          Sent, Image_Sent, No_Dimension, No_Weight, No_Read, Hand_Scanned, Complete
/// </summary>
public static class MadibanaItemLogQuery
{
    private static readonly System.Text.RegularExpressions.Regex SafeTableName =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || !SafeTableName.IsMatch(tableName))
            throw new ArgumentException($"Invalid table name: '{tableName}'. Only alphanumeric characters and underscores are allowed.");
        return tableName;
    }

    public static async Task<Dictionary<string, int>> ExecuteWindowSummaryAsync(
        SqlConnection connection,
        string tableName,
        DateTime startLocal,
        DateTime endLocal,
        CancellationToken cancellationToken)
    {
        var safeTable = ValidateTableName(tableName);
        using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"
            SELECT
                COUNT(*) AS Total_Items,
                SUM(CASE WHEN CAST(No_Read AS INT) = 1 THEN 1 ELSE 0 END) AS No_Read,
                SUM(CASE WHEN CAST(No_Dimension AS INT) = 1 THEN 1 ELSE 0 END) AS No_Dimension,
                SUM(CASE WHEN CAST(No_Weight AS INT) = 1 THEN 1 ELSE 0 END) AS No_Weight,
                SUM(CASE WHEN CAST(Hand_Scanned AS INT) = 1 THEN 1 ELSE 0 END) AS Hand_Scanned,
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
            int total       = reader["Total_Items"]  is DBNull ? 0 : Convert.ToInt32(reader["Total_Items"]);
            int noRead      = reader["No_Read"]       is DBNull ? 0 : Convert.ToInt32(reader["No_Read"]);
            int noDim       = reader["No_Dimension"]  is DBNull ? 0 : Convert.ToInt32(reader["No_Dimension"]);
            int noWeight    = reader["No_Weight"]     is DBNull ? 0 : Convert.ToInt32(reader["No_Weight"]);
            int handScanned = reader["Hand_Scanned"]  is DBNull ? 0 : Convert.ToInt32(reader["Hand_Scanned"]);
            int dataSent    = reader["Data_Sent"]     is DBNull ? 0 : Convert.ToInt32(reader["Data_Sent"]);
            int imgSent     = reader["Image_Sent"]    is DBNull ? 0 : Convert.ToInt32(reader["Image_Sent"]);
            int complete    = reader["Complete"]      is DBNull ? 0 : Convert.ToInt32(reader["Complete"]);

            result["Total_Items"]    = total;
            result["No_Read"]        = noRead;
            result["Good_Read"]      = total - noRead;
            result["No_Dimension"]   = noDim;
            result["No_Weight"]      = noWeight;
            result["Hand_Scanned"]   = handScanned;
            result["Data_Sent"]      = dataSent;
            result["Not_Sent"]       = total - dataSent;
            result["Image_Sent"]     = imgSent;
            result["Image_Not_Sent"] = total - imgSent;
            result["Complete"]       = complete;
        }

        return result;
    }
}
