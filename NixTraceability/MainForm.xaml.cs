using System;
using System.Windows;
using System.Windows.Threading;

namespace NixTraceability
{
    public partial class MainForm : Window
    {
        private ScanForm? _scanForm;
        private DispatcherTimer _refreshTimer;

        public MainForm()
        {
            InitializeComponent();
            LoadDashboardStats();
            LoadCompanyLogo();

            // Auto-refresh dashboard every 30 seconds
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += (s, e) => LoadDashboardStats();
            _refreshTimer.Start();
        }

        private void LoadCompanyLogo()
        {
            try
            {
                string logoPath = Database.GetSetting("LogoPath", "");
                if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgCompanyLogo.Source = bitmap;
                    imgCompanyLogo.Visibility = Visibility.Visible;
                    txtBrandName.Visibility = Visibility.Collapsed;
                }
                else
                {
                    imgCompanyLogo.Visibility = Visibility.Collapsed;
                    txtBrandName.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("MainForm.LoadCompanyLogo", ex);
            }
        }

        private void LoadDashboardStats()
        {
            try
            {
                var stats = Database.GetTodayStats();
                lblTotal.Text = stats.Total.ToString();
                lblOk.Text = stats.OK.ToString();
                lblNg.Text = stats.NG.ToString();

                double yield = stats.Total > 0 ? ((double)stats.OK / stats.Total) * 100 : 0;
                lblYield.Text = yield.ToString("0.0") + "%";
            }
            catch (Exception ex)
            {
                Logger.LogError("MainForm.LoadDashboardStats", ex);
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardStats();
        }

        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            // Singleton: prevent multiple ScanForm windows
            if (_scanForm != null && _scanForm.IsLoaded)
            {
                _scanForm.Activate();
                _scanForm.WindowState = System.Windows.WindowState.Maximized;
                return;
            }

            _scanForm = new ScanForm();
            _scanForm.Show();
        }

        private void Reports_Click(object sender, RoutedEventArgs e)
        {
            var reportForm = new ReportForm();
            reportForm.Show();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsForm = new SettingsForm();
            settingsForm.Closed += (s, args) =>
            {
                // Reload logo if settings changed
                LoadCompanyLogo();
                LoadDashboardStats();
            };
            settingsForm.Show();
        }
    }
}
