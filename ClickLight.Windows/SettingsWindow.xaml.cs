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
        ShowLaserPointerCheck.IsChecked = s.ShowLaserPointer;
        ShowPressCheck.IsChecked = s.ShowPress;
        ShowReleaseCheck.IsChecked = s.ShowRelease;
        ShowRightClickCheck.IsChecked = s.ShowRightClick;
        ShowMiddleClickCheck.IsChecked = s.ShowMiddleClick;
        ShowDragCheck.IsChecked = s.ShowDrag;
        ShowDragCheck.IsEnabled = !s.ShowLaserPointer;

        SizeSlider.Value = s.Size;
        SizeLabel.Text = $"{s.Size:F0}";

        DurationSlider.Value = s.Duration;
        DurationLabel.Text = $"{s.Duration:F2}s";

        IntensitySlider.Value = s.Intensity;
        IntensityLabel.Text = $"{s.Intensity:F2}";

        ColorCombo.Items.Clear();
        foreach (ColorPreset preset in Enum.GetValues<ColorPreset>())
        {
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

        // Custom color mode
        CustomColorModeCombo.Items.Clear();
        foreach (CustomColorMode mode in Enum.GetValues<CustomColorMode>())
            CustomColorModeCombo.Items.Add(new ComboBoxItem { Content = mode.DisplayName(), Tag = mode });
        for (int i = 0; i < CustomColorModeCombo.Items.Count; i++)
        {
            if (((ComboBoxItem)CustomColorModeCombo.Items[i]).Tag is CustomColorMode m && m == s.CustomColorMode)
            {
                CustomColorModeCombo.SelectedIndex = i;
                break;
            }
        }

        UpdateCustomColorVisibility(s);
        UpdatePerClickColorButtons(s);

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
        ShowMiddleClickCheck.Checked += (_, _) => SaveIfReady(s => s.ShowMiddleClick = true);
        ShowMiddleClickCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowMiddleClick = false);
        ShowDragCheck.Checked += (_, _) => SaveIfReady(s => s.ShowDrag = true);
        ShowDragCheck.Unchecked += (_, _) => SaveIfReady(s => s.ShowDrag = false);

        ShowLaserPointerCheck.Checked += (_, _) =>
        {
            SaveIfReady(s => s.ShowLaserPointer = true);
            ShowDragCheck.IsEnabled = false;
        };
        ShowLaserPointerCheck.Unchecked += (_, _) =>
        {
            SaveIfReady(s => s.ShowLaserPointer = false);
            ShowDragCheck.IsEnabled = true;
        };

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
            if (_suppressEvents) return;
            if (ColorCombo.SelectedItem is ComboBoxItem item && item.Tag is ColorPreset preset)
            {
                SaveIfReady(s => s.ColorPreset = preset);
                UpdateCustomColorVisibility(_settingsStore.Settings);
            }
        };

        CustomColorModeCombo.SelectionChanged += (_, _) =>
        {
            if (_suppressEvents) return;
            if (CustomColorModeCombo.SelectedItem is ComboBoxItem item && item.Tag is CustomColorMode mode)
            {
                SaveIfReady(s => s.CustomColorMode = mode);
                PerClickColorPanel.Visibility = mode == CustomColorMode.ByClick
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        };

        CustomColorButton.Click += (_, _) => PickCustomColor();

        LeftColorButton.Click += (_, _) => PickPerClickColor("Left Click", c => _settingsStore.Update(s => s.CustomLeftColor = c));
        RightColorButton.Click += (_, _) => PickPerClickColor("Right Click", c => _settingsStore.Update(s => s.CustomRightColor = c));
        MiddleColorButton.Click += (_, _) => PickPerClickColor("Middle Click", c => _settingsStore.Update(s => s.CustomMiddleColor = c));
        DragColorButton.Click += (_, _) => PickPerClickColor("Drag", c => _settingsStore.Update(s => s.CustomDragColor = c));

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
            _suppressEvents = true;
            ColorCombo.SelectedIndex = -1;
            _suppressEvents = false;
            UpdateCustomColorVisibility(_settingsStore.Settings);
        }
    }

    private void PickPerClickColor(string label, Action<System.Windows.Media.Color> apply)
    {
        var dialog = new ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var picked = dialog.Color;
            apply(System.Windows.Media.Color.FromRgb(picked.R, picked.G, picked.B));
            UpdatePerClickColorButtons(_settingsStore.Settings);
        }
    }

    private void UpdateCustomColorVisibility(ClickSettings s)
    {
        var isCustom = s.ColorPreset == ColorPreset.Custom;
        CustomColorModePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        if (isCustom)
        {
            PerClickColorPanel.Visibility = s.CustomColorMode == CustomColorMode.ByClick
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdatePerClickColorButtons(ClickSettings s)
    {
        LeftColorButton.Background = new System.Windows.Media.SolidColorBrush(s.CustomLeftColor);
        RightColorButton.Background = new System.Windows.Media.SolidColorBrush(s.CustomRightColor);
        MiddleColorButton.Background = new System.Windows.Media.SolidColorBrush(s.CustomMiddleColor);
        DragColorButton.Background = new System.Windows.Media.SolidColorBrush(s.CustomDragColor);
    }

    private void SaveIfReady(Action<ClickSettings> mutate)
    {
        if (_suppressEvents) return;
        _settingsStore.Update(mutate);
    }
}
