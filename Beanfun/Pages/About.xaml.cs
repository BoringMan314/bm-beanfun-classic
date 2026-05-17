using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Beanfun
{
    /// <summary>
    /// About.xaml 的交互逻辑
    /// </summary>
    public partial class About : Page
    {
        public About()
        {
            InitializeComponent();
            version.Text = App.AssemblyVersion;
            initThemeColor(App.MainWnd.isLightColor());
        }

        public void initThemeColor(bool isLightMode)
        {
            Brush brush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(isLightMode ? "Black" : "White")
            );
            t_AppName.Foreground = brush;
            t_Author.Foreground = brush;
            t_Version.Foreground = brush;
            version.Foreground = brush;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWnd == null)
                return;
            if (App.MainWnd.return_page == null || App.MainWnd.return_page == App.MainWnd.loginPage)
                App.MainWnd.NavigateLoginPage();
            else
                App.MainWnd.frame.Content = App.MainWnd.return_page;
            App.MainWnd.return_page = null;
        }

        private void UpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            App.MainWnd.CheckUpdates(true);
        }

        private void MailContact_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "http://exnormal.com:81/",
                        UseShellExecute = true,
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open contact URL: " + ex.Message);
            }
        }

        private void Github_Click(object sender, RoutedEventArgs e)
        {
            // Fix for .NET 8
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/BoringMan314/bm-beanfun-classic/issues/new",
                    UseShellExecute = true,
                }
            );
        }
    }
}
