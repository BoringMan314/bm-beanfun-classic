using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Beanfun
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        public static readonly Version OSVersion = Environment.OSVersion.Version;
        public static readonly Version WinVista = new Version(6, 0);
        public static readonly Version Win7 = new Version(6, 1);
        public static readonly Version Win8 = new Version(6, 2);
        public static readonly Version Win10 = new Version(10, 0);
        public static readonly Version Win11 = new Version(10, 0, 22000, 0);

        public static MainWindow MainWnd
        {
            get
            {
                Window wnd = Current.MainWindow;
                if (wnd != null && (typeof(MainWindow) == wnd.GetType()))
                    return (MainWindow)wnd;
                else
                    return null;
            }
        }

        public static string LoginRegion = ConfigAppSettings.GetValue("loginRegion", "TW");
        public static int LoginMethod = int.Parse(ConfigAppSettings.GetValue("loginMethod", "0"));

        private void Main(object sender, StartupEventArgs e)
        {
            WindowsAPI.AttachConsole(-1);

            if (bool.Parse(ConfigAppSettings.GetValue("disableHardwareAcceleration", "false")))
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            I18n.LoadLanguage(ConfigAppSettings.GetValue("Language", null));

            StartupUri = new Uri("MainWindow.xaml", UriKind.RelativeOrAbsolute);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (MainWnd != null && MainWnd.bfClient != null)
                try
                {
                    MainWnd.bfClient.Logout();
                }
                catch { }
        }

        public static string ConvertVersion(Version version)
        {
            if (version < new Version(4, 1))
                return $"{version.Major}.{version.Minor}.{version.Build}({version.Revision})";

            DateTime buildDate = new DateTime(2000, 1, 1)
                .AddDays(version.Build)
                .AddSeconds(version.Revision * 2);

            string timestamp = buildDate.ToString("yyMMddHHmm");

            if (version.Build < 1000) // Build < 1000 視為修補號，不是日期戳
            {
                return $"{version.Major}.{version.Minor}.{version.Build}({timestamp})";
            }
            else
            {
                return $"{version.Major}.{version.Minor}({timestamp})";
            }
        }

        internal static string AssemblyVersion
        {
            get
            {
                var attr = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.InformationalVersion))
                {
                    string ver = attr.InformationalVersion;

                    int plusIndex = ver.IndexOf('+');
                    if (plusIndex > 0)
                        ver = ver.Substring(0, plusIndex);

                    return ver;
                }

                return ConvertVersion(Assembly.GetExecutingAssembly().GetName().Version);
            }
        }

        public static readonly string AppDir = Path.GetDirectoryName(
            Process.GetCurrentProcess().MainModule.FileName
        );

        public static int ReleaseResource(string file)
        {
            string path = Path.Combine(AppDir, file);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
            {
                if (stream != null)
                {
                    if (File.Exists(path))
                    {
                        var fileInfo = new FileInfo(path);
                        if (fileInfo.Length == stream.Length)
                            return 0;

                        try
                        {
                            File.Delete(path);
                        }
                        catch
                        {
                            return -1;
                        }
                    }

                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    stream.Position = 0;
                    File.WriteAllBytes(
                        path,
                        new BinaryReader(stream).ReadBytes((int)stream.Length)
                    );
                    return 1;
                }
            }
            return -1;
        }
    }
}
