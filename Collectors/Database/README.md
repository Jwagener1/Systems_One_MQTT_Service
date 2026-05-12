# Database Metrics — Collection Rules

This file is the single source of truth for **how each metric is collected** per customer schema. Edit the cells in plain English to tell the collector exactly what you want — the implementation in `ItemLogQuery.cs`, `SnowsoftItemLogQuery.cs`, and `MadibanaItemLogQuery.cs` should match what is written here.

**Window:** every metric is calculated over the rows whose timestamp falls inside `[Now − 10 min, Now − 5 min)` in local PC time. The filter column is `ItemDateTime` for the Default schema and `Item_Date_Time` for Snowsoft and Madibana.

**Tables:** `Default → ItemLog`, `Snowsoft → tbl_Scanned_Items`, `Madibana → tbl_Measurement`.

A cell of `—` means the metric is **not collected** for that customer.

| Metric             | Default (`ItemLog`)                                                                 | Snowsoft (`tbl_Scanned_Items`)                                          | Madibana (`tbl_Measurement`)                                            |
|--------------------|-------------------------------------------------------------------------------------|-------------------------------------------------------------------------|-------------------------------------------------------------------------|
| `Total_Items`      | Count every row in the window.                                                       | Count every row in the window.                                           | Count every row in the window.                                           |
| `No_Read`          | Count rows where `Barcode` equals the literal string `NOREAD`.                       | Count rows where the `No_Read` bit column is `1`.                        | Count rows where the `No_Read` bit column is `1`.                        |
| `Good_Read`        | `Total_Items − No_Read`.                                                             | `Total_Items − No_Read`.                                                 | `Total_Items − No_Read`.                                                 |
| `No_Dimension`     | Count rows where the `NoDimension` bit column is `1`.                                | —                                                                       | Count rows where the `No_Dimension` bit column is `1`.                   |
| `No_Weight`        | Count rows where the `NoWeight` bit column is `1`.                                   | —                                                                       | Count rows where the `No_Weight` bit column is `1`.                      |
| `Hand_Scanned`     | —                                                                                   | —                                                                       | Count rows where the `Hand_Scanned` bit column is `1`.                   |
| `Data_Sent`        | Count rows where the `Sent` bit column is `1`.                                       | Count rows where the `Sent` bit column is `1`.                           | Count rows where the `Sent` bit column is `1`.                           |
| `Not_Sent`         | `Total_Items − Data_Sent`.                                                           | `Total_Items − Data_Sent`.                                               | `Total_Items − Data_Sent`.                                               |
| `Image_Sent`       | Count rows where the `ImageSent` bit column is `1`.                                  | Count rows where the `Image_Sent` bit column is `1`.                     | Count rows where the `Image_Sent` bit column is `1`.                     |
| `Image_Not_Sent`   | `Total_Items − Image_Sent`.                                                          | `Total_Items − Image_Sent`.                                              | `Total_Items − Image_Sent`.                                              |
| `Item_Out_Of_Spec` | Count rows where `ItemSpec` is non-null and not equal to `0`.                        | —                                                                       | —                                                                       |
| `More_Than_1_Item` | Count rows where `ItemCount` is non-null and not equal to `1`.                       | —                                                                       | —                                                                       |
| `Complete`         | —                                                                                   | Count rows where the `Complete` bit column is `1`.                       | Count rows where the `Complete` bit column is `1`.                       |
