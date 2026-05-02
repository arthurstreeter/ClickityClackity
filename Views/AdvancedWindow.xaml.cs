using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClickityClackity.Models;
using ClickityClackity.Services;
using Button              = System.Windows.Controls.Button;
using Color               = System.Windows.Media.Color;
using ComboBox            = System.Windows.Controls.ComboBox;
using FontFamily          = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation         = System.Windows.Controls.Orientation;

namespace ClickityClackity.Views;

public partial class AdvancedWindow : Window
{
    private readonly SoundManager     _manager;
    private readonly InputHookService _hooks;
    private readonly MainWindow       _main;

    private readonly List<(KeyOverrideEntry Entry, StackPanel DownPanel, StackPanel HoldPanel)> _overridePanels = [];

    public AdvancedWindow(SoundManager manager, InputHookService hooks, MainWindow main)
    {
        _manager = manager;
        _hooks   = hooks;
        _main    = main;

        InitializeComponent();

        _hooks.KeyCaptured += OnKeyCaptured;

        BuildVolumeRows();
        BuildExistingOverrides();
        SyncPitchSliders();
    }

    // ── Per-event volume rows ─────────────────────────────────────────────────

    private void BuildVolumeRows()
    {
        foreach (InputEvent evt in Enum.GetValues<InputEvent>())
        {
            float initial = _manager.EventVolumes.TryGetValue(evt, out var v) ? v : 1f;

            var row = MakeGrid3(150, -1, 52);

            var label      = MakeLabel(evt.DisplayName());
            var valueLabel = MakeLabel($"{(int)(initial * 100)}%");
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;

            var slider = new Slider
            {
                Minimum           = 0,
                Maximum           = 200,
                Value             = initial * 100,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var capturedEvt = evt;
            slider.ValueChanged += (_, e) =>
            {
                _manager.EventVolumes[capturedEvt] = (float)(e.NewValue / 100.0);
                valueLabel.Text = $"{(int)e.NewValue}%";
                _manager.SaveProfile();
            };

            Grid.SetColumn(label,      0);
            Grid.SetColumn(slider,     1);
            Grid.SetColumn(valueLabel, 2);
            row.Children.Add(label);
            row.Children.Add(slider);
            row.Children.Add(valueLabel);

            VolumesPanel.Children.Add(row);
        }
    }

    // ── Per-key override rows ─────────────────────────────────────────────────

    private void BuildExistingOverrides()
    {
        foreach (var entry in _manager.KeyOverrides)
            AddOverrideRow(entry);
        UpdateNoOverridesLabel();
    }

    private void AddOverrideRow(KeyOverrideEntry entry)
    {
        var outerPanel = new StackPanel { Tag = entry, Margin = new Thickness(0, 2, 0, 6) };

        // ── Header: key name + modifier toggles + volume + delete ─────────────
        var header = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                  // key + mods
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // vol slider
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });               // vol%
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });               // ×

        // Key label + modifier buttons (horizontal)
        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };

        var keyLabel = MakeLabel(entry.KeyName);
        keyLabel.FontWeight = FontWeights.SemiBold;
        keyLabel.Margin     = new Thickness(0, 0, 6, 0);
        leftStack.Children.Add(keyLabel);

        var ctrlBtn  = MakeModToggle("Ctrl",  entry.RequireCtrl);
        var altBtn   = MakeModToggle("Alt",   entry.RequireAlt);
        var shiftBtn = MakeModToggle("Shift", entry.RequireShift);

        ctrlBtn.Click  += (_, _) => { entry.RequireCtrl  = !entry.RequireCtrl;  UpdateModToggle(ctrlBtn,  entry.RequireCtrl);  _manager.SaveProfile(); };
        altBtn.Click   += (_, _) => { entry.RequireAlt   = !entry.RequireAlt;   UpdateModToggle(altBtn,   entry.RequireAlt);   _manager.SaveProfile(); };
        shiftBtn.Click += (_, _) => { entry.RequireShift = !entry.RequireShift; UpdateModToggle(shiftBtn, entry.RequireShift); _manager.SaveProfile(); };

        leftStack.Children.Add(ctrlBtn);
        leftStack.Children.Add(altBtn);
        leftStack.Children.Add(shiftBtn);

        // Volume
        var volSlider = new Slider
        {
            Minimum           = 0,
            Maximum           = 200,
            Value             = entry.Volume * 100,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 2, 0),
        };
        var volLabel = MakeLabel($"{(int)(entry.Volume * 100)}%");
        volLabel.HorizontalAlignment = HorizontalAlignment.Right;

        volSlider.ValueChanged += (_, e) =>
        {
            entry.Volume  = (float)(e.NewValue / 100.0);
            volLabel.Text = $"{(int)e.NewValue}%";
            _manager.SaveProfile();
        };

        // Delete
        var del = new Button
        {
            Content           = "✕",
            Padding           = new Thickness(0),
            FontSize          = 11,
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
            _manager.KeyOverrides.Remove(entry);
            _overridePanels.RemoveAll(t => t.Entry == entry);
            OverrideRowsPanel.Children.Remove(outerPanel);
            _manager.SaveProfile();
            UpdateNoOverridesLabel();
        };

        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(volSlider, 1);
        Grid.SetColumn(volLabel,  2);
        Grid.SetColumn(del,       3);
        header.Children.Add(leftStack);
        header.Children.Add(volSlider);
        header.Children.Add(volLabel);
        header.Children.Add(del);

        outerPanel.Children.Add(header);

        // ── Down sounds row ───────────────────────────────────────────────────
        var downRow = MakeSoundSection("↓ DOWN", entry, false, out var downPanel);
        outerPanel.Children.Add(downRow);

        // ── Hold sounds row ───────────────────────────────────────────────────
        var holdRow = MakeSoundSection("⟳ HOLD", entry, true, out var holdPanel);
        outerPanel.Children.Add(holdRow);

        _overridePanels.Add((entry, downPanel, holdPanel));

        OverrideRowsPanel.Children.Add(outerPanel);
        UpdateNoOverridesLabel();
    }

    private Grid MakeSoundSection(string sectionLabel, KeyOverrideEntry entry, bool isHold, out StackPanel soundPanel)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = MakeLabel(sectionLabel);
        label.VerticalAlignment = VerticalAlignment.Top;
        label.Margin            = new Thickness(0, 4, 6, 0);
        label.FontSize          = 10;
        label.Foreground        = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

        soundPanel = new StackPanel();
        PopulateOverrideSoundPanel(entry, soundPanel, isHold);

        Grid.SetColumn(label,      0);
        Grid.SetColumn(soundPanel, 1);
        row.Children.Add(label);
        row.Children.Add(soundPanel);
        return row;
    }

    private void PopulateOverrideSoundPanel(KeyOverrideEntry entry, StackPanel panel, bool isHold)
    {
        panel.Children.Clear();

        var sounds = (isHold ? entry.HoldSounds : entry.Sounds);
        var display = sounds.Count > 0 ? sounds.Cast<SoundEntry?>().ToList() : (List<SoundEntry?>)[null];
        foreach (var s in display)
            panel.Children.Add(MakeOverrideSoundRow(entry, panel, s, isHold));

        panel.Children.Add(MakeOverrideAddButton(entry, panel, isHold));
    }

    private UIElement MakeOverrideSoundRow(KeyOverrideEntry entry, StackPanel panel, SoundEntry? current, bool isHold)
    {
        var row = new Grid { Margin = new Thickness(0, 1, 0, 1), Tag = "sound-row" };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // pitch toggle
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // delete

        var combo = MakeCombo(current?.File);
        combo.SelectionChanged += (_, _) => SyncOverrideSoundsAndSave(entry, panel, isHold);

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
            pitchBtn.Tag      = (object)rp;
            pitchBtn.Content  = rp ? "±" : "–";
            pitchBtn.Foreground = new SolidColorBrush(rp
                ? Color.FromRgb(0xCB, 0xA6, 0xF7)
                : Color.FromRgb(0x6C, 0x70, 0x86));
            SyncOverrideSoundsAndSave(entry, panel, isHold);
        };

        var del = new Button
        {
            Content           = "×",
            Padding           = new Thickness(0),
            FontSize          = 13,
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
                panel.Children.Insert(0, MakeOverrideSoundRow(entry, panel, null, isHold));
            SyncOverrideSoundsAndSave(entry, panel, isHold);
        };

        Grid.SetColumn(combo,    0);
        Grid.SetColumn(pitchBtn, 1);
        Grid.SetColumn(del,      2);
        row.Children.Add(combo);
        row.Children.Add(pitchBtn);
        row.Children.Add(del);
        return row;
    }

    private UIElement MakeOverrideAddButton(KeyOverrideEntry entry, StackPanel panel, bool isHold)
    {
        var btn = new Button
        {
            Content             = "+ add sound",
            Padding             = new Thickness(5, 1, 5, 1),
            FontSize            = 10,
            Margin              = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background          = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x25)),
            Foreground          = new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
            BorderBrush         = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness     = new Thickness(1),
            Tag                 = "add-btn",
        };
        btn.Click += (_, _) =>
        {
            int idx = panel.Children.IndexOf(btn);
            panel.Children.Insert(idx, MakeOverrideSoundRow(entry, panel, null, isHold));
        };
        return btn;
    }

    private void SyncOverrideSoundsAndSave(KeyOverrideEntry entry, StackPanel panel, bool isHold)
    {
        var sounds = panel.Children
            .OfType<Grid>()
            .Where(g => g.Tag as string == "sound-row")
            .Select(g =>
            {
                var file = g.Children.OfType<ComboBox>().FirstOrDefault()?.SelectedItem as string;
                if (string.IsNullOrEmpty(file) || file == "(none)") return null;
                bool rp = g.Children.OfType<Button>().FirstOrDefault(b => b.Tag is bool)?.Tag is bool v ? v : true;
                return new SoundEntry { File = file, RandomPitch = rp };
            })
            .OfType<SoundEntry>()
            .ToList();

        if (isHold) entry.HoldSounds = sounds;
        else        entry.Sounds     = sounds;
        _manager.SaveProfile();
    }

    public void RefreshSoundLists()
    {
        foreach (var (entry, downPanel, holdPanel) in _overridePanels)
        {
            PopulateOverrideSoundPanel(entry, downPanel, false);
            PopulateOverrideSoundPanel(entry, holdPanel, true);
        }
    }

    private void UpdateNoOverridesLabel() =>
        NoOverridesLabel.Visibility = _manager.KeyOverrides.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

    // ── Modifier toggle helpers ───────────────────────────────────────────────

    private static Button MakeModToggle(string label, bool active) => new()
    {
        Content           = label,
        Width             = 38,
        Height            = 20,
        Padding           = new Thickness(2, 0, 2, 0),
        FontSize          = 10,
        Margin            = new Thickness(2, 0, 0, 0),
        Background        = new SolidColorBrush(active
            ? Color.FromRgb(0x58, 0x3E, 0x8A)
            : Color.FromRgb(0x31, 0x32, 0x44)),
        Foreground        = new SolidColorBrush(active
            ? Color.FromRgb(0xCB, 0xA6, 0xF7)
            : Color.FromRgb(0x6C, 0x70, 0x86)),
        BorderBrush       = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
        BorderThickness   = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip           = $"Require {label} key to be held",
    };

    private static void UpdateModToggle(Button btn, bool active)
    {
        btn.Background = new SolidColorBrush(active
            ? Color.FromRgb(0x58, 0x3E, 0x8A)
            : Color.FromRgb(0x31, 0x32, 0x44));
        btn.Foreground = new SolidColorBrush(active
            ? Color.FromRgb(0xCB, 0xA6, 0xF7)
            : Color.FromRgb(0x6C, 0x70, 0x86));
    }

    // ── Key capture ───────────────────────────────────────────────────────────

    private void AddKeyOverride_Click(object sender, RoutedEventArgs e)
    {
        if (_hooks.IsCapturingKey) return;
        _hooks.IsCapturingKey   = true;
        AddKeyButton.Content    = "⬤ Press any key...";
        AddKeyButton.Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF));
        CaptureHint.Text        = "Waiting for key press...";
    }

    private void OnKeyCaptured(uint vkCode)
    {
        Dispatcher.InvokeAsync(() =>
        {
            AddKeyButton.Content    = "+ Add Key Override";
            AddKeyButton.Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));
            CaptureHint.Text = "Click the button above, then press any key to create an override for it.";

            if (_manager.KeyOverrides.Any(k => k.VkCode == vkCode)) return;

            var entry = new KeyOverrideEntry { VkCode = vkCode };
            _manager.KeyOverrides.Add(entry);
            _manager.SaveProfile();
            AddOverrideRow(entry);
        });
    }

    // ── Pitch sliders ─────────────────────────────────────────────────────────

    private void SyncPitchSliders()
    {
        DragPitchSlider.Value   = _manager.DragPitchSemitones;
        RandomPitchSlider.Value = _manager.RandomPitchSemitones;
        DragPitchLabel.Text     = $"±{(int)_manager.DragPitchSemitones} st";
        RandomPitchLabel.Text   = $"±{(int)_manager.RandomPitchSemitones} st";
    }

    private void DragPitchSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_manager == null) return;
        _manager.DragPitchSemitones = (float)e.NewValue;
        if (DragPitchLabel != null) DragPitchLabel.Text = $"±{(int)e.NewValue} st";
        _manager.SaveProfile();
    }

    private void RandomPitchSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_manager == null) return;
        _manager.RandomPitchSemitones = (float)e.NewValue;
        if (RandomPitchLabel != null) RandomPitchLabel.Text = $"±{(int)e.NewValue} st";
        _manager.SaveProfile();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _hooks.KeyCaptured    -= OnKeyCaptured;
        _hooks.IsCapturingKey  = false;
        base.OnClosed(e);
    }

    // ── Layout / widget helpers ───────────────────────────────────────────────

    private ComboBox MakeCombo(string? currentFile)
    {
        var combo = new ComboBox();
        combo.Items.Add("(none)");
        foreach (var f in _manager.GetSoundFiles()) combo.Items.Add(f);
        combo.SelectedItem = currentFile != null
            ? combo.Items.Cast<string>().FirstOrDefault(x => x == currentFile) ?? "(none)"
            : "(none)";
        return combo;
    }

    private static Grid MakeGrid3(double c0, double c1Star, double c2)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(c0) });
        g.ColumnDefinitions.Add(new ColumnDefinition
            { Width = c1Star < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(c1Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(c2) });
        return g;
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text              = text,
        Foreground        = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
        VerticalAlignment = VerticalAlignment.Center,
        FontSize          = 12,
        FontFamily        = new FontFamily("Segoe UI"),
    };
}
