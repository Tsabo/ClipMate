using System.Windows;

namespace ClipMate.App.Services;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}
