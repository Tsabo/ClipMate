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

using System.Collections.Generic;
using System.Linq;

namespace Emoji.Wpf.Internal
{
    /// <summary>
    /// LINQ extension methods to replace Stfu library functionality.
    /// </summary>
    internal static class LinqExtensions
    {
        /// <summary>
        /// Returns a sequence of tuples containing the previous, current, and next elements.
        /// Replaces Stfu.Linq.Extensions.WithPreviousAndNext for .NET 9 compatibility.
        /// </summary>
        public static IEnumerable<(T Previous, T Current, T Next)> WithPreviousAndNext<T>(this IEnumerable<T> source)
        {
            var list = source.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var previous = i > 0 ? list[i - 1] : default(T)!;
                var current = list[i];
                var next = i < list.Count - 1 ? list[i + 1] : default(T)!;
                yield return (previous, current, next);
            }
        }
    }
}
