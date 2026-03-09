using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace RehostedDesigner.Port;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string startupWorkflowPath = e.Args
            .Select(TryNormalizeStartupPath)
            .FirstOrDefault(path => path != null);

        var window = new MainWindow(startupWorkflowPath);
        MainWindow = window;
        window.Show();
    }

    private static string TryNormalizeStartupPath(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return null;
        }

        try
        {
            string fullPath = Path.GetFullPath(arg);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".xaml", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fullPath;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
