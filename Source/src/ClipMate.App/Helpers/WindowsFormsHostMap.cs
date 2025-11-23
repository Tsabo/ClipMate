using System.Windows;
using System.Windows.Forms.Integration;
using ScintillaNET;

namespace ClipMate.App.Helpers;

public static class WindowsFormsHostMap
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(WindowsFormsHostMap),
            new PropertyMetadata(OnTextChanged));

    public static string GetText(WindowsFormsHost element)
        => (string)element.GetValue(TextProperty);

    public static void SetText(WindowsFormsHost element, string value)
        => element.SetValue(TextProperty, value);

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.RegisterAttached("Font", typeof(string), typeof(WindowsFormsHostMap),
            new PropertyMetadata(OnFontChanged));

    public static void SetFont(DependencyObject element, string value)
        => element.SetValue(FontProperty, value);

    public static string GetFont(DependencyObject element)
        => (string)element.GetValue(FontProperty);

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is WindowsFormsHost { Child: Scintilla scintillaControl })
        {
            scintillaControl.Text = Convert.ToString(e.NewValue) ?? string.Empty;
        }
    }

    private static void OnFontChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is WindowsFormsHost { Child: Scintilla scintilla })
        {
            var parts = ((string)e.NewValue)?.Split(',');
            if (parts?.Length == 2 && float.TryParse(parts[1].Replace("pt", "").Trim(), out float size))
            {
                scintilla.Font = new Font(parts[0].Trim(), size);
            }
        }
    }
}
