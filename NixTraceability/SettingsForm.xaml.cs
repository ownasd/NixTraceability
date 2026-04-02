using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace NixTraceability
{
    public partial class SettingsForm : Window
    {
        public ObservableCollection<PartConfig> Parts { get; set; } = new ObservableCollection<PartConfig>();
        private int _editingPartId = -1; // -1 = add mode, >0 = edit mode

        public SettingsForm()
        {
            InitializeComponent();
            LoadParts();
            LoadAppConfiguration();
        }

        private void LoadAppConfiguration()
        {
            txtStationName.Text = Database.GetSetting("StationName", "DICV CCU CHILD PARTS TRACEABILITY");
            cmbOperatorMode.SelectedIndex = Database.GetSetting("OpMode", "Single Operator") == "Single Operator" ? 0 : 1;

            string dupScope = Database.GetSetting("DuplicateScope", "Sequence Only");
            cmbDuplicateScope.SelectedIndex = dupScope switch
            {
                "Whole Day" => 1,
                "Whole Month" => 2,
                _ => 0
            };

            cmbTheme.SelectedIndex = Database.GetSetting("AppTheme", "Dark Theme") == "Dark Theme" ? 0 : 1;
            txtExportPath.Text = Database.GetSetting("DataExportPath", @"C:\Exports\");
            chkAutoBackup.IsChecked = Database.GetSetting("AutoBackup", "1") == "1";

            // Load Logo path
            txtLogoPath.Text = Database.GetSetting("LogoPath", "");
            UpdateLogoPreview(txtLogoPath.Text);

            // Load Product image path
            txtProductImagePath.Text = Database.GetSetting("ProductImagePath", "");
            UpdateImagePreview(imgProductPreview, txtProductImagePath.Text);

            // Load Sound paths
            txtOkSoundPath.Text = Database.GetSetting("OkSoundPath", "");
            txtNgSoundPath.Text = Database.GetSetting("NgSoundPath", "");
        }

        private void UpdateLogoPreview(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgLogoPreview.Source = bmp;
                    imgLogoPreview.Visibility = Visibility.Visible;
                }
                else
                {
                    imgLogoPreview.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsForm.UpdateLogoPreview", ex);
                imgLogoPreview.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadParts()
        {
            try
            {
                Parts.Clear();
                using (SQLiteConnection con = Database.GetConnection())
                {
                    con.Open();
                    string query = "SELECT * FROM PartsConfig ORDER BY Sequence";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Parts.Add(new PartConfig
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                PartName = reader["PartName"].ToString() ?? "",
                                PartCode = reader["PartCode"].ToString() ?? "",
                                ValidationText = reader["ValidationText"].ToString() ?? "",
                                Sequence = Convert.ToInt32(reader["Sequence"]),
                                CheckDuplicate = Convert.ToInt32(reader["CheckDuplicate"]) == 1,
                                IsRequired = Convert.ToInt32(reader["IsRequired"]) == 1,
                                ImagePath = reader["ImagePath"]?.ToString() ?? "",
                                QrRect = reader["QrRect"]?.ToString() ?? ""
                            });
                        }
                    }
                }
                dgParts.ItemsSource = Parts;
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsForm.LoadParts", ex);
                MessageBox.Show("Error loading parts: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Company Logo Handlers ─────────────────────────────────────────────
        private void btnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Company Logo",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico"
            };
            if (dlg.ShowDialog() == true)
            {
                txtLogoPath.Text = dlg.FileName;
                UpdateLogoPreview(dlg.FileName);
            }
        }

        private void btnClearLogo_Click(object sender, RoutedEventArgs e)
        {
            txtLogoPath.Text = "";
            imgLogoPreview.Visibility = Visibility.Collapsed;
        }

        // ── Product Image Handlers ────────────────────────────────────────────
        private void btnBrowseProductImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Product / Assembly Reference Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dlg.ShowDialog() == true)
            {
                txtProductImagePath.Text = dlg.FileName;
                UpdateImagePreview(imgProductPreview, dlg.FileName);
            }
        }

        private void btnClearProductImage_Click(object sender, RoutedEventArgs e)
        {
            txtProductImagePath.Text = "";
            imgProductPreview.Visibility = Visibility.Collapsed;
        }

        private void UpdateImagePreview(System.Windows.Controls.Image imgControl, string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgControl.Source = bmp;
                    imgControl.Visibility = Visibility.Visible;
                }
                else
                {
                    imgControl.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsForm.UpdateImagePreview", ex);
            }
        }

        // ── Sound Handlers ────────────────────────────────────────────────────
        private string? BrowseWavFile(string title)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = "WAV Audio (*.wav)|*.wav|All Files (*.*)|*.*"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        private void btnBrowseOkSound_Click(object sender, RoutedEventArgs e)
        {
            string? path = BrowseWavFile("Select OK Scan Sound");
            if (path != null) txtOkSoundPath.Text = path;
        }

        private void btnClearOkSound_Click(object sender, RoutedEventArgs e) => txtOkSoundPath.Text = "";

        private void btnBrowseNgSound_Click(object sender, RoutedEventArgs e)
        {
            string? path = BrowseWavFile("Select NG Error Sound");
            if (path != null) txtNgSoundPath.Text = path;
        }

        private void btnClearNgSound_Click(object sender, RoutedEventArgs e) => txtNgSoundPath.Text = "";

        // ── Part Image Browse ─────────────────────────────────────────────────
        private void btnBrowsePartImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Part Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dlg.ShowDialog() == true)
                txtNewImagePath.Text = dlg.FileName;
        }

        private void btnSetQrArea_Click(object sender, RoutedEventArgs e)
        {
            string imgPath = txtNewImagePath.Text.Trim();
            if (string.IsNullOrEmpty(imgPath) || !File.Exists(imgPath))
            {
                MessageBox.Show("Please select a valid Part Image first before setting QR Area.", "Missing Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            QrLocationWindow qrWin = new QrLocationWindow(imgPath, txtNewQrRect.Text);
            qrWin.Owner = this;
            if (qrWin.ShowDialog() == true)
            {
                txtNewQrRect.Text = qrWin.ResultQrRect;
            }
        }

        // ── Add/Delete Parts ──────────────────────────────────────────────────
        private void btnAddPart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = txtNewPartName.Text.Trim();
                string code = txtNewPartCode.Text.Trim();
                string validation = txtNewValidation.Text.Trim();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code))
                {
                    MessageBox.Show("Part Name and Part Code are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int.TryParse(txtNewSequence.Text, out int seq);
                int checkDup = chkNewCheckDuplicate.IsChecked == true ? 1 : 0;
                string imagePath = txtNewImagePath.Text.Trim();
                string qrRect = txtNewQrRect.Text.Trim();

                using (SQLiteConnection con = Database.GetConnection())
                {
                    con.Open();

                    if (_editingPartId > 0)
                    {
                        // UPDATE existing part
                        string query = @"UPDATE PartsConfig SET PartName=@n, PartCode=@c, ValidationText=@v, 
                            Sequence=@s, CheckDuplicate=@cd, ImagePath=@img, QrRect=@qr WHERE Id=@id";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@n", name);
                            cmd.Parameters.AddWithValue("@c", code);
                            cmd.Parameters.AddWithValue("@v", validation);
                            cmd.Parameters.AddWithValue("@s", seq);
                            cmd.Parameters.AddWithValue("@cd", checkDup);
                            cmd.Parameters.AddWithValue("@img", imagePath);
                            cmd.Parameters.AddWithValue("@qr", qrRect);
                            cmd.Parameters.AddWithValue("@id", _editingPartId);
                            cmd.ExecuteNonQuery();
                        }
                        CancelEdit();
                    }
                    else
                    {
                        // INSERT new part
                        string query = @"INSERT INTO PartsConfig 
                            (PartName, PartCode, ValidationText, Sequence, CheckDuplicate, IsRequired, ImagePath, QrRect) 
                            VALUES (@n, @c, @v, @s, @cd, 1, @img, @qr)";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@n", name);
                            cmd.Parameters.AddWithValue("@c", code);
                            cmd.Parameters.AddWithValue("@v", validation);
                            cmd.Parameters.AddWithValue("@s", seq);
                            cmd.Parameters.AddWithValue("@cd", checkDup);
                            cmd.Parameters.AddWithValue("@img", imagePath);
                            cmd.Parameters.AddWithValue("@qr", qrRect);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                txtNewPartName.Clear(); txtNewPartCode.Clear();
                txtNewValidation.Clear(); txtNewSequence.Clear();
                txtNewImagePath.Clear(); txtNewQrRect.Clear();
                chkNewCheckDuplicate.IsChecked = true;
                LoadParts();
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsForm.btnAddPart_Click", ex);
                MessageBox.Show("Error saving part: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEditPart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var part = button?.DataContext as PartConfig;
            if (part == null) return;

            // Load part data into the form
            txtNewPartName.Text = part.PartName;
            txtNewPartCode.Text = part.PartCode;
            txtNewValidation.Text = part.ValidationText;
            txtNewSequence.Text = part.Sequence.ToString();
            chkNewCheckDuplicate.IsChecked = part.CheckDuplicate;
            txtNewImagePath.Text = part.ImagePath;
            txtNewQrRect.Text = part.QrRect;

            // Switch to edit mode
            _editingPartId = part.Id;
            btnAddPart.Content = "💾 Update Part";
            btnAddPart.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 123, 204));

            // Scroll to top so user sees the form
            txtNewPartName.Focus();
        }

        private void CancelEdit()
        {
            _editingPartId = -1;
            btnAddPart.Content = "➕ Add Part";
            btnAddPart.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(40, 167, 69));
        }

        private void btnDeletePart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var part = button?.DataContext as PartConfig;
                if (part == null) return;

                if (MessageBox.Show($"Are you sure you want to delete '{part.PartName}'?", "Confirm Delete",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (SQLiteConnection con = Database.GetConnection())
                    {
                        con.Open();
                        string query = "DELETE FROM PartsConfig WHERE Id = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@id", part.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadParts();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsForm.btnDeletePart_Click", ex);
                MessageBox.Show("Error deleting part: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save Handlers ─────────────────────────────────────────────────────
        private void btnSaveGeneral_Click(object sender, RoutedEventArgs e)
        {
            Database.SaveSetting("StationName", txtStationName.Text);
            Database.SaveSetting("OpMode", (cmbOperatorMode.SelectedItem as ComboBoxItem)?.Content.ToString());
            Database.SaveSetting("DuplicateScope", (cmbDuplicateScope.SelectedItem as ComboBoxItem)?.Content.ToString());
            Database.SaveSetting("LogoPath", txtLogoPath.Text);
            Database.SaveSetting("ProductImagePath", txtProductImagePath.Text);
            MessageBox.Show("✅ General Settings Saved!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void cmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                string? theme = (cmbTheme.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (!string.IsNullOrEmpty(theme))
                    ThemeManager.ApplyTheme(theme);
            }
        }

        private void btnSaveAppearance_Click(object sender, RoutedEventArgs e)
        {
            string? theme = (cmbTheme.SelectedItem as ComboBoxItem)?.Content.ToString();
            Database.SaveSetting("AppTheme", theme);
            Database.SaveSetting("OkSoundPath", txtOkSoundPath.Text);
            Database.SaveSetting("NgSoundPath", txtNgSoundPath.Text);
            MessageBox.Show("✅ Appearance & Audio Settings Saved!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnSaveDataSettings_Click(object sender, RoutedEventArgs e)
        {
            Database.SaveSetting("DataExportPath", txtExportPath.Text);
            Database.SaveSetting("AutoBackup", chkAutoBackup.IsChecked == true ? "1" : "0");
            MessageBox.Show("✅ Data & Backup Settings Saved!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnSaveAllSettings_Click(object sender, RoutedEventArgs e)
        {
            Database.SaveSetting("StationName", txtStationName.Text);
            Database.SaveSetting("OpMode", (cmbOperatorMode.SelectedItem as ComboBoxItem)?.Content.ToString());
            Database.SaveSetting("DuplicateScope", (cmbDuplicateScope.SelectedItem as ComboBoxItem)?.Content.ToString());
            Database.SaveSetting("LogoPath", txtLogoPath.Text);

            string? theme = (cmbTheme.SelectedItem as ComboBoxItem)?.Content.ToString();
            Database.SaveSetting("AppTheme", theme);
            Database.SaveSetting("OkSoundPath", txtOkSoundPath.Text);
            Database.SaveSetting("NgSoundPath", txtNgSoundPath.Text);

            Database.SaveSetting("DataExportPath", txtExportPath.Text);
            Database.SaveSetting("AutoBackup", chkAutoBackup.IsChecked == true ? "1" : "0");

            // Save Parts Grid changes
            using (SQLiteConnection con = Database.GetConnection())
            {
                con.Open();
                string query = "UPDATE PartsConfig SET CheckDuplicate = @cd WHERE Id = @id";
                using (SQLiteCommand cmd = new SQLiteCommand(query, con))
                {
                    cmd.Parameters.Add("@cd", DbType.Int32);
                    cmd.Parameters.Add("@id", DbType.Int32);
                    foreach (var part in Parts)
                    {
                        cmd.Parameters["@cd"].Value = part.CheckDuplicate ? 1 : 0;
                        cmd.Parameters["@id"].Value = part.Id;
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            Logger.LogInfo("SettingsForm", "All settings saved by user.");
            // NOTE: No this.Close() — user stays in settings
            MessageBox.Show("✅ All settings saved successfully!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class PartConfig
    {
        public int Id { get; set; }
        public string PartName { get; set; } = string.Empty;
        public string PartCode { get; set; } = string.Empty;
        public string ValidationText { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public bool CheckDuplicate { get; set; }
        public bool IsRequired { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string QrRect { get; set; } = string.Empty;
    }
}
