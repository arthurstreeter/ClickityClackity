using System.Reflection;
using System.Windows;
using Application = System.Windows.Application;

namespace ClickityClackity;

public partial class App : Application
{
    internal static bool IsExiting { get; private set; }

    private System.Windows.Forms.NotifyIcon?       _tray;
    private System.Windows.Forms.ToolStripMenuItem? _enableItem;
    private System.Drawing.Icon? _iconEnabled;
    private System.Drawing.Icon? _iconDisabled;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _iconEnabled  = LoadIcon("ClickityClackity.Assets.tray_enabled.ico");
        _iconDisabled = LoadIcon("ClickityClackity.Assets.tray_disabled.ico");

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text    = "ClickityClackity",
            Icon    = _iconEnabled,
            Visible = true,
        };

        _enableItem = new System.Windows.Forms.ToolStripMenuItem("● Enabled");
        _enableItem.Click += (_, _) =>
        {
            if (MainWindow is not MainWindow mw) return;
            bool newState = !mw.InputEnabled;
            mw.Dispatcher.Invoke(() => mw.SetInputEnabled(newState));
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            if (_enableItem == null || MainWindow is not MainWindow mw) return;
            _enableItem.Text = mw.InputEnabled ? "● Enabled" : "○ Disabled";
        };

        menu.Items.Add("Show", null, (_, _) => ShowMain());
        menu.Items.Add(_enableItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { IsExiting = true; Shutdown(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    internal void UpdateTrayIcon(bool enabled)
    {
        if (_tray       != null) _tray.Icon       = enabled ? _iconEnabled : _iconDisabled;
        if (_enableItem != null) _enableItem.Text  = enabled ? "● Enabled" : "○ Disabled";
    }

    private static System.Drawing.Icon LoadIcon(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)!;
        return new System.Drawing.Icon(stream);
    }

    private void ShowMain()
    {
        MainWindow?.Show();
        MainWindow?.Activate();
        if (MainWindow?.WindowState == WindowState.Minimized)
            MainWindow.WindowState = WindowState.Normal;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _iconEnabled?.Dispose();
        _iconDisabled?.Dispose();
        base.OnExit(e);
    }
}
