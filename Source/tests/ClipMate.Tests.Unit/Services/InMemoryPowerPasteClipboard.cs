using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Power Paste's clipboard in memory: records what is offered, and lets a test read it or take it away.
/// </summary>
internal sealed class InMemoryPowerPasteClipboard : IPowerPasteClipboard
{
    /// <summary>
    /// Every set of Formats offered, in order.
    /// </summary>
    public List<IReadOnlyList<ClipboardFormatData>> Offers { get; } = [];

    /// <summary>
    /// How many times the Formats were offered again after a read that was not a paste.
    /// </summary>
    public int OffersAgain { get; private set; }

    /// <summary>
    /// How many times the clipboard was released.
    /// </summary>
    public int Releases { get; private set; }

    /// <summary>
    /// True while Formats are offered and not released.
    /// </summary>
    public bool IsHeld { get; private set; }

    /// <inheritdoc />
    public event EventHandler<PowerPasteClipboardReadEventArgs>? Read;

    /// <inheritdoc />
    public event EventHandler? Lost;

    /// <inheritdoc />
    public bool Offer(IReadOnlyList<ClipboardFormatData> formats)
    {
        Offers.Add(formats);
        IsHeld = true;

        return true;
    }

    /// <inheritdoc />
    public bool OfferAgain()
    {
        if (!IsHeld)
            return false;

        OffersAgain++;

        return true;
    }

    /// <inheritdoc />
    public void Release()
    {
        Releases++;
        IsHeld = false;
    }

    /// <summary>
    /// An application reads the offered Formats: the application the user works in when <paramref name="isPaste" />,
    /// otherwise a program in the background while the user does nothing.
    /// </summary>
    public void ReadBy(bool isPaste) =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs
        {
            IsByForegroundApplication = isPaste,
            SinceUserInput = isPaste ? TimeSpan.Zero : TimeSpan.FromHours(1),
            ReaderName = isPaste ? "notepad" : "clipboard history",
        });

    /// <summary>
    /// A read that cannot be tied to a process, <paramref name="sinceUserInput" /> after the user's last input: a browser
    /// or Windows Terminal reads without a window, and a Store app's read comes from a Windows service.
    /// </summary>
    public void ReadWithoutAProcess(TimeSpan sinceUserInput) =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs { MayBeForForegroundApplication = true, SinceUserInput = sinceUserInput, ReaderName = "(no window)" });

    /// <summary>
    /// A named program in the background reads, just after the user's input.
    /// </summary>
    public void ReadByABackgroundProgramWhileTheUserTypes() =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs { SinceUserInput = TimeSpan.Zero, ReaderName = "msrdc" });

    /// <summary>
    /// A named program in the background reads while the user does nothing.
    /// </summary>
    /// <returns>False when the program was refused the data.</returns>
    public bool ReadInTheBackground(string readerName = "msrdc")
    {
        var read = new PowerPasteClipboardReadEventArgs { SinceUserInput = TimeSpan.FromHours(1), ReaderName = readerName };
        Read?.Invoke(this, read);

        return !read.IsRefused;
    }

    /// <summary>
    /// ClipMate itself reads, just after the user's input.
    /// </summary>
    public void ReadByClipMate() =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs { IsByClipMate = true, IsClipMateInForeground = true, SinceUserInput = TimeSpan.Zero, ReaderName = "ClipMate.App" });

    /// <summary>
    /// A read that cannot be tied to a process, just after the user's input in ClipMate's own window.
    /// </summary>
    public void ReadWithClipMateInTheForeground() =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs { IsClipMateInForeground = true, MayBeForForegroundApplication = true, SinceUserInput = TimeSpan.Zero, ReaderName = "(no window)" });

    /// <summary>
    /// Another program puts something on the clipboard.
    /// </summary>
    public void TakeAway()
    {
        IsHeld = false;
        Lost?.Invoke(this, EventArgs.Empty);
    }
}
