using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Power Paste's clipboard in memory: records what is offered, and lets a test read it or take it away. Like Windows, a
/// read that is given the data renders the offer, and later reads of it are not reported until the next offer.
/// </summary>
internal sealed class InMemoryPowerPasteClipboard : IPowerPasteClipboard
{
    /// <summary>
    /// Every set of Formats offered, in order.
    /// </summary>
    public List<IReadOnlyList<ClipboardFormatData>> Offers { get; } = [];

    /// <summary>
    /// How many more offers fail, as when another program keeps the clipboard open.
    /// </summary>
    public int OffersToFail { get; set; }

    /// <summary>
    /// How many times the clipboard was released.
    /// </summary>
    public int Releases { get; private set; }

    /// <summary>
    /// True while Formats are offered and not released.
    /// </summary>
    public bool IsHeld { get; private set; }

    /// <summary>
    /// True when the current offer was given to a reader, so the clipboard holds its data.
    /// </summary>
    public bool IsRendered { get; private set; }

    /// <summary>
    /// How many reads were refused.
    /// </summary>
    public int Refusals { get; private set; }

    /// <inheritdoc />
    public event EventHandler<PowerPasteClipboardReadEventArgs>? Read;

    /// <inheritdoc />
    public event EventHandler? Lost;

    /// <inheritdoc />
    public bool Offer(IReadOnlyList<ClipboardFormatData> formats)
    {
        if (OffersToFail > 0)
        {
            OffersToFail--;

            return false;
        }

        Offers.Add(formats);
        IsHeld = true;
        IsRendered = false;

        return true;
    }

    /// <inheritdoc />
    public void Release()
    {
        Releases++;
        IsHeld = false;
    }

    /// <summary>
    /// The user pastes into the program they are working in, whose window reads.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadByTheTarget() => ReadAs(new PowerPasteClipboardReadEventArgs { IsByForegroundApplication = true, ReaderName = "notepad" });

    /// <summary>
    /// A named program in the background reads: Remote Desktop's client, clipboard history, another clipboard manager.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadInTheBackground(string readerName = "msrdc") => ReadAs(new PowerPasteClipboardReadEventArgs { ReaderName = readerName });

    /// <summary>
    /// A read without a window, as a browser, an Electron program or Windows Terminal pastes.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadWithoutAWindow() => ReadAs(new PowerPasteClipboardReadEventArgs { IsWithoutWindow = true, ReaderName = "(no window)" });

    /// <summary>
    /// A Windows service reads while a Store app is in the foreground, as when pasting into Calculator.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadForAStoreApp() => ReadAs(new PowerPasteClipboardReadEventArgs { IsStoreAppInForeground = true, ReaderName = "svchost" });

    /// <summary>
    /// ClipMate itself reads.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadByClipMate() =>
        ReadAs(new PowerPasteClipboardReadEventArgs { IsByClipMate = true, IsClipMateInForeground = true, ReaderName = "ClipMate.App" });

    /// <summary>
    /// A read without a window while ClipMate's own window is in the foreground.
    /// </summary>
    /// <returns>False when the reader got no data.</returns>
    public bool ReadWithoutAWindowWithClipMateInTheForeground() =>
        ReadAs(new PowerPasteClipboardReadEventArgs { IsWithoutWindow = true, IsClipMateInForeground = true, ReaderName = "(no window)" });

    /// <summary>
    /// Another program puts something on the clipboard.
    /// </summary>
    public void TakeAway()
    {
        IsHeld = false;
        Lost?.Invoke(this, EventArgs.Empty);
    }

    private bool ReadAs(PowerPasteClipboardReadEventArgs read)
    {
        // Windows asks the owner only for data it doesn't have yet
        if (!IsHeld || IsRendered)
            return IsHeld;

        var offer = Offers.Count;
        Read?.Invoke(this, read);
        if (read.IsRefused)
        {
            Refusals++;

            return false;
        }

        // The data goes to the offer that was read; a handler may already have offered the next clip
        if (Offers.Count == offer)
            IsRendered = true;

        return true;
    }
}
