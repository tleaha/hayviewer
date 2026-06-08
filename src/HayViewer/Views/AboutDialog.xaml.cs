using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace HayViewer.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        VersionText.Text = $"Version {version}";
    }

    private void OK_Click(object sender, RoutedEventArgs e) => Close();

    private void NoticesLink_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
