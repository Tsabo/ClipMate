using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Services;

public partial class TextTransformServiceTests
{
    private static TextCleanupConfiguration NoCleanup() => new() { Leading = new TextCleanupCharacters() };

    [Test]
    [Arguments("one\r\ntwo\r\nthree", "one two three")]
    [Arguments("one\ntwo", "one two")]
    [Arguments("one \r\ntwo", "one two")]
    [Arguments("first\r\nline\r\n\r\nnext\r\npara", "first line\r\n\r\nnext para")]
    [Arguments("first\r\n\r\n\r\nnext", "first\r\n\r\n\r\nnext")]
    [Arguments("  one\r\n   two", "one two")]
    [Arguments("a   b", "a b")]
    [Arguments("End.   Next", "End.  Next")]
    [Arguments("one\r\n", "one ")]
    public async Task RemoveLineBreaks_WithinParagraphs_JoinsLinesAndKeepsParagraphs(string input, string expected) =>
        await Assert.That(_service.RemoveLineBreaks(input, LineBreakMode.PreserveParagraphs)).IsEqualTo(expected);

    [Test]
    public async Task RemoveLineBreaks_All_JoinsParagraphsToo() =>
        await Assert.That(_service.RemoveLineBreaks("one\r\ntwo\r\n\r\nthree", LineBreakMode.RemoveAll)).IsEqualTo("one two  three");

    [Test]
    [Arguments("http://example.com/\r\nlong/path", "http://example.com/long/path")]
    [Arguments("one \r\ntwo", "onetwo")]
    public async Task RemoveLineBreaks_JoinWithoutSpaces_LeavesNothingBetweenLines(string input, string expected) =>
        await Assert.That(_service.RemoveLineBreaks(input, LineBreakMode.PreserveParagraphs, true)).IsEqualTo(expected);

    [Test]
    public async Task CleanUpText_Defaults_StripLeadingQuoteMarksAndWhiteSpaceFromEachLine() =>
        await Assert.That(_service.CleanUpText("> > one\r\n\t>two  \r\n", new TextCleanupConfiguration())).IsEqualTo("one\r\ntwo  \r\n");

    [Test]
    public async Task CleanUpText_NothingChosen_LeavesTextAlone() =>
        await Assert.That(_service.CleanUpText("  > one \r\n", NoCleanup())).IsEqualTo("  > one \r\n");

    [Test]
    public async Task CleanUpText_StripsTrailingAndAnywhereCharacters()
    {
        var options = NoCleanup();
        options.Trailing.Spaces = true;
        options.Trailing.Pipe = true;
        options.Anywhere.Other = true;
        options.Anywhere.OtherText = "*";

        await Assert.That(_service.CleanUpText("a*b | \r\nc*d", options)).IsEqualTo("ab\r\ncd");
    }

    [Test]
    public async Task CleanUpText_RunsTheStepsInOrder()
    {
        // Strip, join lines, upper case, wrap, replace (matching the upper-cased text), then start each line
        var options = new TextCleanupConfiguration
        {
            LineBreaks = TextCleanupLineBreaks.WithinParagraphs,
            Case = TextCleanupCase.Upper,
            ReBreak = true,
            ReBreakColumn = 8,
            FindText = "THREE",
            ReplaceText = "3",
            MatchCase = true,
            ReQuote = true,
            ReQuoteText = "| ",
        };

        await Assert.That(_service.CleanUpText("> one two\r\n> three", options)).IsEqualTo("| ONE TWO \r\n| 3");
    }

    [Test]
    public async Task CleanUpText_ReBreak_KeepsLongWordsAndCollapsesBlankLines()
    {
        var options = NoCleanup();
        options.ReBreak = true;
        options.ReBreakColumn = 5;

        await Assert.That(_service.CleanUpText("abcdefgh ij\r\n\r\n\r\nk", options)).IsEqualTo("abcdefgh \r\nij\r\n\r\nk");
    }

    [Test]
    public async Task CleanUpText_ReQuote_DoesNotAddALineAfterTheLastLineBreak()
    {
        var options = NoCleanup();
        options.ReQuote = true;

        await Assert.That(_service.CleanUpText("a\r\n\r\nb\r\n", options)).IsEqualTo(">a\r\n>\r\n>b\r\n");
    }

    [Test]
    [Arguments(true, "x-a\r\nx-b")]
    [Arguments(false, "x-a\r\nb")]
    public async Task CleanUpText_RegEx_RunsLineByLineOrOnTheWholeText(bool lineByLine, string expected)
    {
        var options = NoCleanup();
        options.FindText = "^";
        options.ReplaceText = "x-";
        options.RegExFind = true;
        options.RegExLineByLine = lineByLine;

        await Assert.That(_service.CleanUpText("a\r\nb", options)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false, "$1\\x09")]
    [Arguments(true, "b\t")]
    public async Task CleanUpText_RegExReplace_ControlsWhetherTheReplacementIsLiteral(bool regExReplace, string expected)
    {
        var options = NoCleanup();
        options.FindText = "a(b)";
        options.ReplaceText = "$1\\x09";
        options.RegExFind = true;
        options.RegExReplace = regExReplace;

        await Assert.That(_service.CleanUpText("ab", options)).IsEqualTo(expected);
    }

    [Test]
    public async Task CleanUpText_InvalidRegEx_Throws()
    {
        var options = NoCleanup();
        options.FindText = "(";
        options.RegExFind = true;

        await Assert.That(() => _service.CleanUpText("a", options)).Throws<ArgumentException>();
    }

    [Test]
    public async Task CleanUpTitle_ChangesOnlyCaseAndFindReplace()
    {
        var options = new TextCleanupConfiguration { Case = TextCleanupCase.Upper, LineBreaks = TextCleanupLineBreaks.All, FindText = "B", ReplaceText = "z", ReQuote = true };

        await Assert.That(_service.CleanUpTitle("> a b", options)).IsEqualTo("> A z");
    }
}
