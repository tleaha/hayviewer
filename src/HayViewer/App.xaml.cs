using System.IO;
using System.Windows;

namespace HayViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Handle command-line file argument: HayViewer.exe file.json
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            var win = new MainWindow();
            win.Show();
            win.OpenFileFromDrop(e.Args[0]);
        }
    }
}
