using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickityClackity.Models;
using ClickityClackity.Services;
using ClickityClackity.Views;
using Button             = System.Windows.Controls.Button;
using Color              = System.Windows.Media.Color;
using FontFamily         = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ClickityClackity;

public partial class MainWindow : Window
{
    private readonly SoundManager     _manager;
    private readonly InputHookService _hooks;
    private readonly Dictionary<InputEvent, StackPanel> _soundPanels = [];
    private bool _enabled = true;

    private AdvancedWindow? _advancedWindow;

    private static readonly Dictionary<string, InputEvent[]> GroupedEvents = new()
    {
        ["KEYBOARD"]       = [InputEvent.KeyDown, InputEvent.KeyUp, InputEvent.KeyHold],
        ["MOUSE BUTTONS"]  = [InputEvent.MouseLeftDown,   InputEvent.MouseLeftUp,
                              InputEvent.MouseRightDown,  InputEvent.MouseRightUp,
                              InputEvent.MouseMiddleDown, InputEvent.MouseMiddleUp],
        ["MOUSE MOVEMENT"] = [InputEvent.MouseScrollUp,    InputEvent.MouseScrollDown,
                              InputEvent.MouseLeftDrag,    InputEvent.MouseRightDrag,
                              InputEvent.MouseMiddleDrag],
    };

    public bool InputEnabled => _enabled;

    public void SetInputEnabled(bool enabled)
    {
        _enabled         = enabled;
        EnableToggle.IsChecked = enabled;
        // Checked/Unchecked handlers will update the label
    }

    public MainWindow()
    {
        InitializeComponent();

        var baseDir = Path.GetDirectoryName(Environment.ProcessPath)
                   ?? AppContext.BaseDirectory;
        _manager = new SoundManager(
            soundsFolder: Path.Combine(baseDir, "sounds"),
            profilePath:  Path.Combine(baseDir, "profile.json")
        );
        _manager.LoadProfile();

        VolumeSlider.Value        = _manager.Volume * 100;
        SoundsFolderLabel.Text    = _manager.SoundsFolder;

        _hooks = new InputHookService();
        _hooks.InputDetected += data => { if (_enabled) _manager.Play(data); };
        _hooks.Install();

        BuildEventRows();
        PopulateSoundPanels();
    }

    // ── Build static structure ─────────────────────────────────────────────────

    private void BuildEventRows()
    {
        foreach (var (group, events) in GroupedEvents)
        {
            EventsPanel.Children.Add(new TextBlock
            {
                Text  = group,
                Style = (Style)FindResource("SectionHeader"),
            });

            foreach (var evt in events)
            {
                var outer = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                outer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var label = new TextBlock
                {
                    Text              = evt.DisplayName(),
                    Foreground        = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin            = new Thickness(0, 4, 0, 0),
                    FontSize          = 12,
                    FontFamily        = new FontFamily("Segoe UI"),
                };

                var panel = new StackPanel();
                _soundPanels[evt] = panel;

                Grid.SetColumn(panel, 1);
                outer.Children.Add(label);
                outer.Children.Add(panel);
                EventsPanel.Children.Add(outer);
            }
        }
    }

    // ── Populate/refresh sound panels ────────────────────────────────────────

    public void PopulateSoundPanels()
    {
        foreach (var (evt, panel) in _soundPanels)
            RebuildSoundPanel(panel, evt);
    }

    private void RebuildSoundPanel(StackPanel panel, InputEvent evt)
    {
        panel.Children.Clear();

        var sounds = _manager.GetSounds(evt).ToList();
        if (sounds.Count == 0) sounds.Add(null!); // always show at least one slot

        foreach (var entry in sounds)
            panel.Children.Add(MakeSoundRow(panel, evt, entry));

        panel.Children.Add(MakeAddButton(panel, evt));
    }

    private UIElement MakeSoundRow(StackPanel panel, InputEvent evt, ClickityClackity.Models.SoundEntry? current)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1), Tag = "sound-row" };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // pitch toggle
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // delete

        var combo = MakeSoundCombo(current?.File);
        combo.SelectionChanged += (_, _) => SyncAndSave(panel, evt);

        bool rp = current?.RandomPitch ?? true;
        var pitchBtn = new Button
        {
            Content           = rp ? "±" : "–",
            Padding           = new Thickness(0),
            FontSize          = 11,
            Width             = 20,
            Height            = 20,
            Margin            = new Thickness(2, 0, 0, 0),
            Background        = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground        = new SolidColorBrush(rp
                ? Color.FromRgb(0xCB, 0xA6, 0xF7)
                : Color.FromRgb(0x6C, 0x70, 0x86)),
            BorderBrush       = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness   = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip           = "Toggle random pitch for this sound",
            Tag               = (object)rp,
        };
        pitchBtn.Click += (_, _) =>
        {
            rp = !rp;
            pitchBtn.Tag       = (object)rp;
            pitchBtn.Content   = rp ? "±" : "–";
            pitchBtn.Foreground = new SolidColorBrush(rp
                ? Color.FromRgb(0xCB, 0xA6, 0xF7)
                : Color.FromRgb(0x6C, 0x70, 0x86));
            SyncAndSave(panel, evt);
        };

        var del = new Button
        {
            Content           = "×",
            Padding           = new Thickness(0),
            FontSize          = 14,
            Width             = 20,
            Height            = 20,
            Margin            = new Thickness(2, 0, 0, 0),
            Background        = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground        = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
            BorderBrush       = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness   = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        del.Click += (_, _) =>
        {
            panel.Children.Remove(row);
            if (!panel.Children.OfType<Grid>().Any(g => g.Tag as string == "sound-row"))
                panel.Children.Insert(0, MakeSoundRow(panel, evt, null));
            SyncAndSave(panel, evt);
        };

        Grid.SetColumn(combo,    0);
        Grid.SetColumn(pitchBtn, 1);
        Grid.SetColumn(del,      2);
        row.Children.Add(combo);
        row.Children.Add(pitchBtn);
        row.Children.Add(del);
        return row;
    }

    private UIElement MakeAddButton(StackPanel panel, InputEvent evt)
    {
        var btn = new Button
        {
            Content         = "+ add sound",
            Padding         = new Thickness(6, 2, 6, 2),
            FontSize        = 11,
            Margin          = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background      = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x25)),
            Foreground      = new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness = new Thickness(1),
            Tag             = "add-btn",
        };
        btn.Click += (_, _) =>
        {
            // Insert new sound row before the add button
            int addIdx = panel.Children.IndexOf(btn);
            panel.Children.Insert(addIdx, MakeSoundRow(panel, evt, null));
        };
        return btn;
    }

    private System.Windows.Controls.ComboBox MakeSoundCombo(string? currentFile)
    {
        var combo = new System.Windows.Controls.ComboBox();
        combo.Items.Add("(none)");
        foreach (var f in _manager.GetSoundFiles()) combo.Items.Add(f);
        combo.SelectedItem = currentFile != null
            ? combo.Items.Cast<string>().FirstOrDefault(x => x == currentFile) ?? "(none)"
            : "(none)";
        return combo;
    }

    private void SyncAndSave(StackPanel panel, InputEvent evt)
    {
        var sounds = panel.Children
            .OfType<Grid>()
            .Where(g => g.Tag as string == "sound-row")
            .Select(g =>
            {
                var file = g.Children.OfType<System.Windows.Controls.ComboBox>()
                    .FirstOrDefault()?.SelectedItem as string;
                if (string.IsNullOrEmpty(file) || file == "(none)") return null;
                bool rp = g.Children.OfType<Button>().FirstOrDefault(b => b.Tag is bool)?.Tag is bool v ? v : true;
                return new ClickityClackity.Models.SoundEntry { File = file, RandomPitch = rp };
            })
            .Where(s => s != null)
            .ToList();

        _manager.SetSounds(evt, sounds!);
        _manager.SaveProfile();
    }

    // Re-used by RefreshSounds button and AdvancedWindow
    public void RefreshAllSounds()
    {
        _manager.InvalidateAudioCache();
        PopulateSoundPanels();
        _advancedWindow?.RefreshSoundLists();
    }

    // ── Header controls ───────────────────────────────────────────────────────

    private void EnableToggle_Checked(object sender, RoutedEventArgs e)
    {
        _enabled = true;
        if (EnableLabel is null) return;
        EnableLabel.Text       = "● Active";
        EnableLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
        ((App)System.Windows.Application.Current).UpdateTrayIcon(true);
    }

    private void EnableToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _enabled = false;
        if (EnableLabel is null) return;
        EnableLabel.Text       = "○ Inactive";
        EnableLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));
        ((App)System.Windows.Application.Current).UpdateTrayIcon(false);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_manager == null) return;
        _manager.Volume = (float)(e.NewValue / 100.0);
        if (VolumeLabel != null) VolumeLabel.Text = $"{(int)e.NewValue}%";
        _manager.SaveProfile();
    }

    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        if (_advancedWindow == null || !_advancedWindow.IsLoaded)
        {
            _advancedWindow = new AdvancedWindow(_manager, _hooks, this) { Owner = this };
            _advancedWindow.Show();
        }
        else _advancedWindow.Activate();
    }

    private void BrowseSoundsFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description          = "Select sounds folder",
            UseDescriptionForTitle = true,
            SelectedPath         = _manager.SoundsFolder,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _manager.SetSoundsFolder(dlg.SelectedPath);
            SoundsFolderLabel.Text = dlg.SelectedPath;
            _manager.SaveProfile();
            RefreshAllSounds();
        }
    }

    private void OpenSoundsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_manager.SoundsFolder);
        System.Diagnostics.Process.Start("explorer.exe", _manager.SoundsFolder);
    }

    private void RefreshSounds_Click(object sender, RoutedEventArgs e) => RefreshAllSounds();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsExiting) { e.Cancel = true; Hide(); return; }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hooks.Dispose();
        _manager.Dispose();
        base.OnClosed(e);
    }
}
