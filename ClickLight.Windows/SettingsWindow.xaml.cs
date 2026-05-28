using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace ClickLight.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settingsStore;
    private readonly OverlayCoordinator _overlayCoordinator;
    private bool _suppressEvents;

    public SettingsWindow(SettingsStore settingsStore, OverlayCoordinator overlayCoordinator)
    {
        _settingsStore = settingsStore;
        _overlayCoordinator = overlayCoordinator;
        InitializeComponent();

        LoadSettings();
        WireEvents();
    }

    private void LoadSettings()
    {
        _suppressEvents = true;
        var s = _settingsStore.Settings;

        EnabledCheck.IsChecked = s.IsEnabled;
        LaunchAtLoginCheck.IsChecked = LaunchAtLoginController.IsEnabled;
        ShowPressCheck.IsChecked = s.ShowPress;
        ShowReleaseCheck.IsChecked = s.ShowRelease;
        ShowRightClickCheck.IsChecked = s.ShowRightClick;
        ShowDragCheck.IsChecked = s.ShowDrag;

        SizeSlider.Value = s.Size;
        SizeLabel.Text = $"{s.Size:F0}";

        DurationSlider.Value = s.Duration;
        DurationLabel.Text = $"{s.Duration:F2}s";

        IntensitySlider.Value = s.Intensity;
        IntensityLabel.Text = $"{s.Intensity:F2}";

        ColorCombo.Items.Clear();
        foreach (ColorPreset preset in Enum.GetValues<ColorPreset>())
        {
            if (preset == ColorPreset.Custom) continue;
            ColorCombo.Items.Add(new ComboBoxItem { Content = preset.DisplayName(), Tag = preset });
        }
        for (int i = 0; i < ColorCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)ColorCombo.Items[i]).Tag is ColorPreset p && p == s.ColorPreset)
            {
                ColorCombo.SelectedIndex = i;
                break;
            }
        }

        _suppressEvents = false;
    }

    private void WireEvents()
    {
        EnabledCheck.Checked += (_, _) => SaveIfReady(s => s.IsEnabled = true);
        EnabledCheck.Unchecked += (_, _) => SaveIfReady(s => s.IsEnabled = false);

        LaunchAtLoginCheck.Checked += (_, _) => { if (!_suppressEvents) LaunchAtLoginController.IsEnabled = true; };
        LaunchAtLoginCheck.Unchecked += (_, _) => { if (!_suppressEvents) LaunchAtLoginController.IsEnabled = false; };

        ShowPressCheck.Checked += (_, _) => SaveIfReady(s => s.ShowPress = true);
        ShowPressCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowPress = false);
        ShowReleaseCheck.Checked += (_, _) => SaveIfReady(s => s.ShowRelease = true);
        ShowReleaseCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowRelease = false);
        ShowRightClickCheck.Checked += (_, _) => SaveIfReady(s => s.ShowRightClick = true);
        ShowRightClickCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowRightClick = false);
        ShowDragCheck.Checked += (_, _) => SaveIfReady(s => s.ShowDrag = true);
        ShowDragCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowDrag = false);

        SizeSlider.ValueChanged += (_, e) =>
        {
            SizeLabel.Text = $"{e.NewValue:F0}";
            SaveIfReady(s => s.Size = e.NewValue);
        };

        DurationSlider.ValueChanged += (_, e) =>
        {
            DurationLabel.Text = $"{e.NewValue:F2}s";
            SaveIfReady(s => s.Duration = e.NewValue);
        };

        IntensitySlider.ValueChanged += (_, e) =>
        {
            IntensityLabel.Text = $"{e.NewValue:F2}";
            SaveIfReady(s => s.Intensity = e.NewValue);
        };

        ColorCombo.SelectionChanged += (_, _) =>
        {
            if (ColorCombo.SelectedItem is ComboBoxItem item && item.Tag is ColorPreset preset)
                SaveIfReady(s => s.ColorPreset = preset);
        };

        CustomColorButton.Click += (_, _) => PickCustomColor();

        TestButton.Click += (_, _) => _overlayCoordinator.TestPulse();
        ResetButton.Click += (_, _) =>
        {
            _settingsStore.Reset();
            LoadSettings();
        };
    }

    private void PickCustomColor()
    {
        var current = _settingsStore.Settings.CustomColor;
        var dialog = new ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
            FullOpen = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var picked = dialog.Color;
            _settingsStore.Update(s =>
            {
                s.ColorPreset = ColorPreset.Custom;
                s.CustomColor = System.Windows.Media.Color.FromRgb(picked.R, picked.G, picked.B);
            });
            // Update combo to deselect presets (custom isn't in the list)
            _suppressEvents = true;
            ColorCombo.SelectedIndex = -1;
            _suppressEvents = false;
        }
    }

    private void SaveIfReady(Action<ClickSettings> mutate)
    {
        if (_suppressEvents) return;
        _settingsStore.Update(mutate);
    }
}
