using System.Configuration;
using System.Data;
using System.Windows;

namespace NixTraceability
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Load and apply the saved theme
            string savedTheme = Database.GetSetting("AppTheme", "Dark Theme");
            ThemeManager.ApplyTheme(savedTheme);
        }
    }

}
