using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// Draws the sample image clip, a simple bar chart, from plain pixels, so that it is the same PNG on every machine.
/// </summary>
public static class SampleChart
{
    private const int _width = 480;
    private const int _height = 300;
    private const int _bytesPerPixel = 4;
    private const int _margin = 30;

    private static readonly (int Percent, byte Blue, byte Green, byte Red)[] _bars =
    [
        (72, 0xC4, 0x72, 0x2E),
        (54, 0x3C, 0xA0, 0x4C),
        (88, 0x2B, 0x8C, 0xE6),
        (38, 0xA0, 0x4F, 0x8E),
        (63, 0xB8, 0xA8, 0x2A),
    ];

    /// <summary>
    /// The chart as a PNG file's bytes.
    /// </summary>
    public static byte[] CreatePng()
    {
        var pixels = new byte[_width * _height * _bytesPerPixel];
        Fill(pixels, 0, 0, _width, _height, 0xFF, 0xFF, 0xFF);

        // Grid lines every quarter of the height, then the axis
        var plotHeight = _height - 2 * _margin;
        for (var i = 0; i < 4; i++)
            Fill(pixels, _margin, _margin + i * plotHeight / 4, _width - _margin, _margin + i * plotHeight / 4 + 1, 0xE4, 0xE4, 0xE4);

        var slot = (_width - 2 * _margin) / _bars.Length;
        for (var i = 0; i < _bars.Length; i++)
        {
            var (percent, blue, green, red) = _bars[i];
            var left = _margin + i * slot + slot / 5;
            Fill(pixels, left, _height - _margin - plotHeight * percent / 100, left + slot * 3 / 5, _height - _margin, blue, green, red);
        }

        Fill(pixels, _margin, _height - _margin, _width - _margin, _height - _margin + 2, 0x60, 0x60, 0x60);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(_width, _height, 96, 96, PixelFormats.Bgr32, null, pixels, _width * _bytesPerPixel)));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void Fill(byte[] pixels, int left, int top, int right, int bottom, byte blue, byte green, byte red)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = (y * _width + x) * _bytesPerPixel;
                pixels[offset] = blue;
                pixels[offset + 1] = green;
                pixels[offset + 2] = red;
            }
        }
    }
}
