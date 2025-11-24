using System;
using System.Windows;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.App.Controls;

/// <summary>
/// Control for editing Monaco Editor configuration options.
/// </summary>
public partial class EditorOptionsControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ConfigurationProperty =
        DependencyProperty.Register(
            nameof(Configuration),
            typeof(MonacoEditorConfiguration),
            typeof(EditorOptionsControl),
            new PropertyMetadata(null, OnConfigurationChanged));

    public MonacoEditorConfiguration? Configuration
    {
        get => (MonacoEditorConfiguration?)GetValue(ConfigurationProperty);
        set => SetValue(ConfigurationProperty, value);
    }

    public event EventHandler? ApplyClicked;
    public event EventHandler? ResetClicked;

    public EditorOptionsControl()
    {
        InitializeComponent();
        
        // Set default theme selection
        ThemeComboBox.SelectedIndex = 0;
    }

    private static void OnConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EditorOptionsControl control && e.NewValue is MonacoEditorConfiguration config)
        {
            control.DataContext = config;
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (Configuration != null)
        {
            // Reset to defaults
            var defaults = new MonacoEditorConfiguration();
            Configuration.Theme = defaults.Theme;
            Configuration.FontFamily = defaults.FontFamily;
            Configuration.FontSize = defaults.FontSize;
            Configuration.TabSize = defaults.TabSize;
            Configuration.WordWrap = defaults.WordWrap;
            Configuration.ShowLineNumbers = defaults.ShowLineNumbers;
            Configuration.ShowMinimap = defaults.ShowMinimap;
            Configuration.SmoothScrolling = defaults.SmoothScrolling;
            Configuration.DisplayWordAndCharacterCounts = defaults.DisplayWordAndCharacterCounts;
            Configuration.ShowToolbar = defaults.ShowToolbar;

            // Update ComboBox selection
            switch (Configuration.Theme)
            {
                case "vs-dark":
                    ThemeComboBox.SelectedIndex = 0;
                    break;
                case "vs":
                    ThemeComboBox.SelectedIndex = 1;
                    break;
                case "hc-black":
                    ThemeComboBox.SelectedIndex = 2;
                    break;
            }
        }

        ResetClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        ApplyClicked?.Invoke(this, EventArgs.Empty);
    }
}
