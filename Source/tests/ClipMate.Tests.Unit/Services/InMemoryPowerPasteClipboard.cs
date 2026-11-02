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
    /// An application reads the offered Formats: the application the user works in when <paramref name="isPaste" />.
    /// </summary>
    public void ReadBy(bool isPaste) =>
        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs { IsPaste = isPaste, ReaderName = isPaste ? "notepad" : "clipboard history" });

    /// <summary>
    /// Another program puts something on the clipboard.
    /// </summary>
    public void TakeAway()
    {
        IsHeld = false;
        Lost?.Invoke(this, EventArgs.Empty);
    }
}
