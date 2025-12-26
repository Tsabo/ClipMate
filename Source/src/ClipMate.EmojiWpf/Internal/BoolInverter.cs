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

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Emoji.Wpf.Internal
{
    /// <summary>
    /// A value converter that inverts a boolean value.
    /// Replaces Stfu.Wpf.BoolInverter for .NET 9 compatibility.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class BoolInverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
