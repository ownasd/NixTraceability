using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace NixTraceability
{
    public partial class ReportForm : Window
    {
        public ReportForm()
        {
            InitializeComponent();
            LoadReport();
        }

        private void LoadReport()
        {
            try
            {
                DataTable reportTable = new DataTable();
                reportTable.Columns.Add("ID");
                reportTable.Columns.Add("DateTime");
                reportTable.Columns.Add("Operator");
                reportTable.Columns.Add("OverallResult");

                List<string> partCodes = new List<string>();

                using (SQLiteConnection con = Database.GetConnection())
                {
                    con.Open();

                    // 1. Get all distinct part codes to create columns
                    string partsQuery = "SELECT DISTINCT PartCode FROM PartsConfig ORDER BY Sequence";
                    using (SQLiteCommand cmd = new SQLiteCommand(partsQuery, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = reader["PartCode"].ToString();
                            partCodes.Add(code);
                            reportTable.Columns.Add(code);
                        }
                    }

                    // 2. Get all master records
                    DateTime? fromDate = dpFrom.SelectedDate;
                    DateTime? toDate = dpTo.SelectedDate;

                    string masterQuery = "SELECT * FROM ScanRecords WHERE 1=1";
                    if (fromDate.HasValue) masterQuery += " AND date(DateTime) >= date(@from)";
                    if (toDate.HasValue) masterQuery += " AND date(DateTime) <= date(@to)";
                    masterQuery += " ORDER BY DateTime DESC";

                    using (SQLiteCommand cmd = new SQLiteCommand(masterQuery, con))
                    {
                        if (fromDate.HasValue) cmd.Parameters.AddWithValue("@from", fromDate.Value.ToString("yyyy-MM-dd"));
                        if (toDate.HasValue) cmd.Parameters.AddWithValue("@to", toDate.Value.ToString("yyyy-MM-dd"));

                        using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
                        {
                            DataTable masterTable = new DataTable();
                            da.Fill(masterTable);

                            foreach (DataRow masterRow in masterTable.Rows)
                            {
                                long recordId = Convert.ToInt64(masterRow["Id"]);
                            DataRow newRow = reportTable.NewRow();
                            newRow["ID"] = recordId;
                            newRow["DateTime"] = masterRow["DateTime"];
                            newRow["Operator"] = masterRow["Operator"];
                            newRow["OverallResult"] = masterRow["Result"];

                            // 3. Get details for this record
                            string detailsQuery = "SELECT PartCode, Value, Result FROM ScanDetails WHERE RecordId = @id";
                            using (SQLiteCommand detailCmd = new SQLiteCommand(detailsQuery, con))
                            {
                                detailCmd.Parameters.AddWithValue("@id", recordId);
                                using (SQLiteDataReader detailReader = detailCmd.ExecuteReader())
                                {
                                    while (detailReader.Read())
                                    {
                                        string code = detailReader["PartCode"].ToString();
                                        string val = detailReader["Value"].ToString();
                                        string res = detailReader["Result"].ToString();
                                        
                                        if (reportTable.Columns.Contains(code))
                                        {
                                            newRow[code] = val;
                                        }
                                    }
                                }
                            }
                            reportTable.Rows.Add(newRow);
                        }
                    } // close SQLiteDataAdapter
                    } // close SQLiteCommand
                }

                dgRecords.ItemsSource = reportTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading report: " + ex.Message);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReport();
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataView? dv = dgRecords.ItemsSource as DataView;
                if (dv == null) return;

                DataTable dt = dv.ToTable();

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Report",
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                    DefaultExt = ".csv"
                };

                if (dialog.ShowDialog() != true) return;

                var sb = new System.Text.StringBuilder();

                // Headers
                var columnNames = System.Linq.Enumerable.Select(dt.Columns.Cast<System.Data.DataColumn>(), c => $"\"{c.ColumnName}\"");
                sb.AppendLine(string.Join(",", columnNames));

                // Data rows
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    var fields = row.ItemArray.Select(field => $"\"{field?.ToString()?.Replace("\"", "\"\"")}\"");
                    sb.AppendLine(string.Join(",", fields));
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                Logger.LogInfo("ReportForm.Export", $"Exported {dt.Rows.Count} records to {dialog.FileName}");
                MessageBox.Show($"✅ Export successful!\n{dt.Rows.Count} records saved to:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("ReportForm.btnExport_Click", ex);
                MessageBox.Show("Error exporting CSV: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
