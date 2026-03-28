using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NixTraceability
{
    public partial class ScanForm : Window
    {
        private List<TextBox> textBoxes = new List<TextBox>();
        private List<string> partImagePaths = new List<string>();
        private System.Windows.Threading.DispatcherTimer timer;

        public ScanForm()
        {
            InitializeComponent();

            // Load station name from settings
            lblStationName.Text = Database.GetSetting("StationName", "DICV CCU CHILD PARTS TRACEABILITY");

            // Set up real-time clock
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
            UpdateDateTime();

            // Load product/assembly image
            LoadProductImage();

            LoadParts();
        }

        private void LoadProductImage()
        {
            try
            {
                string imgPath = Database.GetSetting("ProductImagePath", "");
                if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
                {
                    productImage.Source = LoadBitmap(imgPath);
                    txtProductPlaceholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    productImage.Source = null;
                    txtProductPlaceholder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ScanForm.LoadProductImage", ex);
            }
        }

        private void Timer_Tick(object sender, EventArgs e) => UpdateDateTime();

        private void UpdateDateTime()
        {
            lblDate.Text = DateTime.Now.ToString("dd-MM-yyyy");
            lblTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
        }

        private void LoadParts()
        {
            try
            {
                partsContainer.Children.Clear();
                textBoxes.Clear();
                partImagePaths.Clear();

                using (SQLiteConnection con = Database.GetConnection())
                {
                    con.Open();
                    string query = "SELECT * FROM PartsConfig ORDER BY Sequence";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var p = new PartInfo
                            {
                                Name = reader["PartName"].ToString() ?? "",
                                Code = reader["PartCode"].ToString() ?? "",
                                Validation = reader["ValidationText"].ToString() ?? "",
                                CheckDuplicate = Convert.ToInt32(reader["CheckDuplicate"]) == 1,
                                ImagePath = reader["ImagePath"]?.ToString() ?? ""
                            };
                            partImagePaths.Add(p.ImagePath);

                            // Row Container — star-based widths for no text cutoff
                            Grid row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

                            SolidColorBrush yellowBg = new SolidColorBrush(Color.FromRgb(255, 235, 59));
                            SolidColorBrush gridBorder = new SolidColorBrush(Color.FromRgb(43, 62, 170));

                            // Name Label
                            Border b1 = new Border { Background = yellowBg, BorderBrush = gridBorder, BorderThickness = new Thickness(1, 1, 0, 1) };
                            TextBlock lblName = new TextBlock
                            {
                                Text = p.Name,
                                Foreground = Brushes.Black,
                                FontSize = 18,
                                FontWeight = FontWeights.Bold,
                                Padding = new Thickness(10, 8, 10, 8),
                                VerticalAlignment = VerticalAlignment.Center,
                                TextWrapping = TextWrapping.Wrap
                            };
                            b1.Child = lblName;
                            Grid.SetColumn(b1, 0);
                            row.Children.Add(b1);

                            // Expected Code Label
                            Border b2 = new Border { Background = yellowBg, BorderBrush = gridBorder, BorderThickness = new Thickness(1, 1, 0, 1) };
                            TextBlock lblCode = new TextBlock
                            {
                                Text = p.Code,
                                Foreground = Brushes.Black,
                                FontSize = 18,
                                FontWeight = FontWeights.Bold,
                                Padding = new Thickness(10, 8, 10, 8),
                                VerticalAlignment = VerticalAlignment.Center,
                                TextWrapping = TextWrapping.Wrap
                            };
                            b2.Child = lblCode;
                            Grid.SetColumn(b2, 1);
                            row.Children.Add(b2);

                            // Scanned Value TextBox
                            Border b3 = new Border { Background = Brushes.White, BorderBrush = gridBorder, BorderThickness = new Thickness(1) };
                            TextBox txt = new TextBox
                            {
                                FontSize = 20,
                                FontWeight = FontWeights.Bold,
                                Padding = new Thickness(10, 8, 10, 8),
                                Background = Brushes.Transparent,
                                Foreground = Brushes.Black,
                                BorderThickness = new Thickness(0),
                                VerticalContentAlignment = VerticalAlignment.Center,
                                Tag = p
                            };
                            txt.KeyDown += Scan_KeyDown;
                            txt.GotFocus += Txt_GotFocus;
                            b3.Child = txt;
                            Grid.SetColumn(b3, 2);
                            row.Children.Add(b3);

                            partsContainer.Children.Add(row);
                            textBoxes.Add(txt);
                        }
                    }
                }

                if (textBoxes.Count == 0)
                {
                    MessageBox.Show("No parts configured! Please add parts in Settings.", "Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                textBoxes[0].Focus();
                LoadPartImage(0);
            }
            catch (Exception ex)
            {
                Logger.LogError("ScanForm.LoadParts", ex);
                MessageBox.Show("Error loading parts: " + ex.Message);
            }
        }

        private void Txt_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                int index = textBoxes.IndexOf(txt);
                if (index >= 0) LoadPartImage(index);
            }
        }

        private BitmapImage? LoadBitmap(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }

        private void LoadPartImage(int index)
        {
            try
            {
                if (index < 0 || index >= partImagePaths.Count) return;
                string imgPath = partImagePaths[index];

                // Update part label in the panel header
                string partName = index < textBoxes.Count
                    ? (textBoxes[index].Tag as PartInfo)?.Name ?? "Part"
                    : "Part";
                lblCurrentPartName.Text = $"🔩  {partName.ToUpper()}";

                if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
                {
                    partImage.Source = LoadBitmap(imgPath);
                    txtImagePlaceholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    partImage.Source = null;
                    txtImagePlaceholder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ScanForm.LoadPartImage", ex);
            }
        }

        private void PlaySound(bool isOk)
        {
            try
            {
                string soundKey = isOk ? "OkSoundPath" : "NgSoundPath";
                string soundPath = Database.GetSetting(soundKey, "");
                if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                {
                    using var player = new SoundPlayer(soundPath);
                    player.Play();
                }
                else
                {
                    if (isOk) SystemSounds.Beep.Play();
                    else SystemSounds.Hand.Play();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ScanForm.PlaySound", ex);
                if (isOk) SystemSounds.Beep.Play();
                else SystemSounds.Hand.Play();
            }
        }

        private void Scan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            TextBox? current = sender as TextBox;
            if (current == null) return;

            PartInfo? p = current.Tag as PartInfo;
            if (p == null) return;

            int index = textBoxes.IndexOf(current);
            string value = current.Text.Trim();
            if (string.IsNullOrEmpty(value)) return;

            // DUPLICATE PREVENTION
            if (p.CheckDuplicate)
            {
                // Sequence Level Check
                if (textBoxes.Any(t => t != current && string.Equals(t.Text.Trim(), value, StringComparison.OrdinalIgnoreCase)))
                {
                    if (current.Parent is Border bDup) bDup.Background = new SolidColorBrush(Color.FromRgb(180, 30, 30));
                    PlaySound(false);
                    MessageBox.Show("❌ DUPLICATE SCAN!\nThis barcode has already been scanned in this sequence.", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    current.Clear(); current.Focus();
                    if (current.Parent is Border bDupReset) bDupReset.Background = Brushes.White;
                    return;
                }

                // Global Level Check
                string dupScope = Database.GetSetting("DuplicateScope", "Sequence Only");
                if (dupScope != "Sequence Only" && Database.IsDuplicateScan(p.Code, value, dupScope))
                {
                    if (current.Parent is Border bGlob) bGlob.Background = new SolidColorBrush(Color.FromRgb(180, 30, 30));
                    PlaySound(false);
                    MessageBox.Show($"❌ DUPLICATE SCAN ({dupScope})!\nThis barcode was already scanned within the {dupScope.ToLower()}.", "Global Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    current.Clear(); current.Focus();
                    if (current.Parent is Border bGlobReset) bGlobReset.Background = Brushes.White;
                    return;
                }
            }

            if (Validate(value, p.Validation))
            {
                if (current.Parent is Border bOk)
                    bOk.Background = new SolidColorBrush(Color.FromRgb(100, 220, 120));
                PlaySound(true);
                lblFooter.Text = "✅ OK — SCAN NEXT PART";

                if (index < textBoxes.Count - 1)
                {
                    textBoxes[index + 1].Focus();
                    LoadPartImage(index + 1);
                }
                else
                {
                    SaveAllData();
                }
            }
            else
            {
                if (current.Parent is Border bNg)
                    bNg.Background = new SolidColorBrush(Color.FromRgb(255, 180, 180));
                PlaySound(false);
                lblFooter.Text = "❌ WRONG PART — SCAN AGAIN";

                MessageBox.Show($"❌ WRONG PART SCANNED!\nRequired: {p.Name}\nExpected match: {p.Validation}\nScanned: {value}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);

                current.Clear(); current.Focus();
                if (current.Parent is Border bNgReset) bNgReset.Background = Brushes.White;
                lblFooter.Text = "SCAN THE BARCODE";
            }
        }

        private bool Validate(string scanned, string validationText)
        {
            if (string.IsNullOrEmpty(validationText)) return true;
            return scanned.Contains(validationText);
        }

        private void SaveAllData()
        {
            try
            {
                bool allValid = textBoxes.All(t => !string.IsNullOrWhiteSpace(t.Text) && Validate(t.Text, ((PartInfo)t.Tag).Validation));
                if (!allValid)
                {
                    MessageBox.Show("❌ Cannot Save. Please scan all correct parts first.", "Industrial Rule Violation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string opId = string.IsNullOrWhiteSpace(txtOperatorId.Text) ? "Unknown" : txtOperatorId.Text.Trim();
                string shift = (cmbShift.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Morning";
                string batch = string.IsNullOrWhiteSpace(txtBatch.Text) ? "" : txtBatch.Text.Trim();

                long recordId = Database.InsertScanMaster("OK", opId, shift, batch);

                foreach (TextBox txt in textBoxes)
                {
                    PartInfo? part = txt.Tag as PartInfo;
                    if (part == null) continue;
                    Database.InsertScanDetail(recordId, part.Code, txt.Text, "OK");
                }

                Logger.LogInfo("ScanForm.SaveAllData", $"Record saved. Op={opId}, Shift={shift}, Batch={batch}");

                lblOverallStatus.Text = "✅ SAVED: OK";
                lblOverallStatus.Foreground = Brushes.LightGreen;
                lblOverallStatus.Visibility = Visibility.Visible;
                lblFooter.Text = "✅ ALL PARTS SCANNED — RECORD SAVED";

                MessageBox.Show("✅ All parts validated and record saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetForm();
            }
            catch (Exception ex)
            {
                Logger.LogError("ScanForm.SaveAllData", ex);
                MessageBox.Show("Error saving data: " + ex.Message);
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e) => ResetForm();

        private void ResetForm()
        {
            foreach (var txt in textBoxes)
            {
                txt.Text = "";
                txt.Background = Brushes.Transparent;
                if (txt.Parent is Border b) b.Background = Brushes.White;
            }
            if (textBoxes.Count > 0)
            {
                textBoxes[0].Focus();
                LoadPartImage(0);
            }
            lblOverallStatus.Text = "";
            lblOverallStatus.Visibility = Visibility.Collapsed;
            lblFooter.Text = "SCAN THE BARCODE";
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            this.Close();
        }
    }

    public class PartInfo
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Validation { get; set; } = "";
        public bool CheckDuplicate { get; set; }
        public string ImagePath { get; set; } = "";
    }
}
