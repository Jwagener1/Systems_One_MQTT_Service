using System.Data;
using Microsoft.Data.SqlClient;

namespace Systems_One_MQTT_Service.Collectors.Database;

public static class ItemLogQuery
{
    private static readonly System.Text.RegularExpressions.Regex SafeTableName =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || !SafeTableName.IsMatch(tableName))
            throw new ArgumentException($"Invalid table name: '{tableName}'. Only alphanumeric characters and underscores are allowed.");
        return tableName;
    }

    public static async Task<List<Dictionary<string, object?>>> ExecuteWindowAsync(
        SqlConnection connection,
        string tableName,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var safeTable = ValidateTableName(tableName);
        using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"SELECT Id, ItemDateTime, Barcode, Length, Width, Height, Weight, BoxVolume, LiquidVolume, NoDimension, NoWeight, Sent, ImageSent, Valid, Complete, ItemSpec, ItemCount, LegacyId
                             FROM [{safeTable}]
                             WHERE ItemDateTime >= @StartUtc AND ItemDateTime < @EndUtc";

        cmd.Parameters.Add(new SqlParameter("@StartUtc", SqlDbType.DateTime) { Value = startUtc });
        cmd.Parameters.Add(new SqlParameter("@EndUtc", SqlDbType.DateTime) { Value = endUtc });

        var rows = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>
            {
                ["Id"] = reader["Id"],
                ["ItemDateTime"] = reader["ItemDateTime"],
                ["Barcode"] = reader["Barcode"],
                ["Length"] = reader["Length"],
                ["Width"] = reader["Width"],
                ["Height"] = reader["Height"],
                ["Weight"] = reader["Weight"],
                ["BoxVolume"] = reader["BoxVolume"],
                ["LiquidVolume"] = reader["LiquidVolume"],
                ["NoDimension"] = reader["NoDimension"],
                ["NoWeight"] = reader["NoWeight"],
                ["Sent"] = reader["Sent"],
                ["ImageSent"] = reader["ImageSent"],
                ["Valid"] = reader["Valid"],
                ["Complete"] = reader["Complete"],
                ["ItemSpec"] = reader["ItemSpec"],
                ["ItemCount"] = reader["ItemCount"],
                ["LegacyId"] = reader["LegacyId"],
            };
            rows.Add(row);
        }
        return rows;
    }

    public static async Task<Dictionary<string, int>> ExecuteWindowSummaryAsync(
        SqlConnection connection,
        string tableName,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var safeTable = ValidateTableName(tableName);
        using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = $@"
            SELECT
                COUNT(*) AS Total_Items,
                SUM(CASE WHEN Barcode = 'NOREAD' THEN 1 ELSE 0 END) AS No_Read,
                SUM(CASE WHEN CAST(NoDimension AS INT) = 1 THEN 1 ELSE 0 END) AS No_Dimension,
                SUM(CASE WHEN CAST(NoWeight AS INT) = 1 THEN 1 ELSE 0 END) AS No_Weight,
                SUM(CASE WHEN CAST(Sent AS INT) = 1 THEN 1 ELSE 0 END) AS Data_Sent,
                SUM(CASE WHEN CAST(ImageSent AS INT) = 1 THEN 1 ELSE 0 END) AS Image_Sent,
                SUM(CASE WHEN ISNULL(ItemSpec, 0) <> 0 THEN 1 ELSE 0 END) AS Item_Out_Of_Spec,
                SUM(CASE WHEN ISNULL(ItemCount, 0) <> 1 THEN 1 ELSE 0 END) AS More_Than_1_Item
            FROM [{safeTable}]
            WHERE ItemDateTime >= @StartUtc AND ItemDateTime < @EndUtc";

        cmd.Parameters.Add(new SqlParameter("@StartUtc", SqlDbType.DateTime) { Value = startUtc });
        cmd.Parameters.Add(new SqlParameter("@EndUtc", SqlDbType.DateTime) { Value = endUtc });

        var result = new Dictionary<string, int>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            int total = reader["Total_Items"] is DBNull ? 0 : Convert.ToInt32(reader["Total_Items"]);
            int noRead = reader["No_Read"] is DBNull ? 0 : Convert.ToInt32(reader["No_Read"]);
            int noDim = reader["No_Dimension"] is DBNull ? 0 : Convert.ToInt32(reader["No_Dimension"]);
            int noWeight = reader["No_Weight"] is DBNull ? 0 : Convert.ToInt32(reader["No_Weight"]);
            int dataSent = reader["Data_Sent"] is DBNull ? 0 : Convert.ToInt32(reader["Data_Sent"]);
            int imageSent = reader["Image_Sent"] is DBNull ? 0 : Convert.ToInt32(reader["Image_Sent"]);
            int itemOutSpec = reader["Item_Out_Of_Spec"] is DBNull ? 0 : Convert.ToInt32(reader["Item_Out_Of_Spec"]);
            int moreThan1 = reader["More_Than_1_Item"] is DBNull ? 0 : Convert.ToInt32(reader["More_Than_1_Item"]);

            result["Total_Items"] = total;
            result["No_Read"] = noRead;
            result["Good_Read"] = total - noRead;
            result["No_Dimension"] = noDim;
            result["No_Weight"] = noWeight;
            result["Data_Sent"] = dataSent;
            result["Not_Sent"] = total - dataSent;
            result["Image_Sent"] = imageSent;
            result["Image_Not_Sent"] = total - imageSent;
            result["Item_Out_Of_Spec"] = itemOutSpec;
            result["More_Than_1_Item"] = moreThan1;
        }

        return result;
    }
}
