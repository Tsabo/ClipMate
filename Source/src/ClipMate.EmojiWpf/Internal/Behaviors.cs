//
//  Emoji.Wpf — Emoji support for WPF
//
//  Copyright © 2017–2022 Sam Hocevar <sam@hocevar.net>
//
//  This library is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Emoji.Wpf.Internal
{
    /// <summary>
    /// Attached behaviors for WPF controls.
    /// Replaces Stfu.Wpf.Behaviors for .NET 9 compatibility.
    /// </summary>
    public static class Behaviors
    {
        public static readonly DependencyProperty SmoothScrollingProperty =
            DependencyProperty.RegisterAttached(
                "SmoothScrolling",
                typeof(bool),
                typeof(Behaviors),
                new PropertyMetadata(false, OnSmoothScrollingChanged));

        public static bool GetSmoothScrolling(DependencyObject obj)
        {
            return (bool)obj.GetValue(SmoothScrollingProperty);
        }

        public static void SetSmoothScrolling(DependencyObject obj, bool value)
        {
            obj.SetValue(SmoothScrollingProperty, value);
        }

        private static void OnSmoothScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListView listView && e.NewValue is bool enable)
            {
                if (enable)
                {
                    listView.PreviewMouseWheel += OnPreviewMouseWheel;
                }
                else
                {
                    listView.PreviewMouseWheel -= OnPreviewMouseWheel;
                }
            }
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ListView listView)
            {
                var scrollViewer = FindScrollViewer(listView);
                if (scrollViewer != null)
                {
                    double offset = scrollViewer.VerticalOffset - (e.Delta / 3.0);
                    scrollViewer.ScrollToVerticalOffset(offset);
                    e.Handled = true;
                }
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}
