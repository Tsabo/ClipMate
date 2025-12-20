using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Services;

public class QuickPasteFormattingStringTests
{
    [Test]
    public async Task ToRegistryFormat_ReturnsCorrectFormat_WithAllFields()
    {
        // Arrange
        var formattingString = new QuickPasteFormattingString
        {
            Title = "Excel",
            Preamble = "^{HOME}",
            PasteKeystrokes = "^v",
            Postamble = "{TAB}",
            TitleTrigger = "Microsoft Excel",
        };

        // Act
        var result = formattingString.ToRegistryFormat();

        // Assert
        await Assert.That(result).IsEqualTo("[Excel],[^{HOME}],[^v],[{TAB}],[Microsoft Excel]");
    }

    [Test]
    public async Task ToRegistryFormat_ReturnsCorrectFormat_WithEmptyFields()
    {
        // Arrange
        var formattingString = new QuickPasteFormattingString
        {
            Title = "Plain",
            Preamble = "",
            PasteKeystrokes = "^v",
            Postamble = "",
            TitleTrigger = "",
        };

        // Act
        var result = formattingString.ToRegistryFormat();

        // Assert
        await Assert.That(result).IsEqualTo("[Plain],[],[^v],[],[]");
    }

    [Test]
    public async Task ToRegistryFormat_HandlesComplexMetaCharacters()
    {
        // Arrange
        var formattingString = new QuickPasteFormattingString
        {
            Title = "Complex",
            Preamble = "^~@{F1}#DATE#",
            PasteKeystrokes = "~{INSERT}",
            Postamble = "#PAUSE#{ENTER}",
            TitleTrigger = "*",
        };

        // Act
        var result = formattingString.ToRegistryFormat();

        // Assert
        await Assert.That(result).IsEqualTo("[Complex],[^~@{F1}#DATE#],[~{INSERT}],[#PAUSE#{ENTER}],[*]");
    }

    [Test]
    public async Task FromRegistryFormat_ParsesCorrectly_WithAllFields()
    {
        // Arrange
        const string registryFormat = "[Excel],[^{HOME}],[^v],[{TAB}],[Microsoft Excel]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Excel");
        await Assert.That(result.Preamble).IsEqualTo("^{HOME}");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("{TAB}");
        await Assert.That(result.TitleTrigger).IsEqualTo("Microsoft Excel");
    }

    [Test]
    public async Task FromRegistryFormat_ParsesCorrectly_WithEmptyFields()
    {
        // Arrange
        const string registryFormat = "[Plain],[],[^v],[],[]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Plain");
        await Assert.That(result.Preamble).IsEqualTo("");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("");
        await Assert.That(result.TitleTrigger).IsEqualTo("");
    }

    [Test]
    public async Task FromRegistryFormat_HandlesComplexMetaCharacters()
    {
        // Arrange
        const string registryFormat = "[Complex],[^~@{F1}#DATE#],[~{INSERT}],[#PAUSE#{ENTER}],[*]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Complex");
        await Assert.That(result.Preamble).IsEqualTo("^~@{F1}#DATE#");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("~{INSERT}");
        await Assert.That(result.Postamble).IsEqualTo("#PAUSE#{ENTER}");
        await Assert.That(result.TitleTrigger).IsEqualTo("*");
    }

    [Test]
    public async Task FromRegistryFormat_ReturnsEmptyInstance_WhenInvalidFormat()
    {
        // Arrange
        const string invalidFormat = "[Excel],[^v]"; // Only 2 parts instead of 5

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(invalidFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("");
        await Assert.That(result.Preamble).IsEqualTo("");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("");
        await Assert.That(result.Postamble).IsEqualTo("");
        await Assert.That(result.TitleTrigger).IsEqualTo("");
    }

    [Test]
    public async Task FromRegistryFormat_ReturnsEmptyInstance_WhenEmptyString()
    {
        // Arrange
        const string emptyFormat = "";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(emptyFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("");
        await Assert.That(result.Preamble).IsEqualTo("");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("");
        await Assert.That(result.Postamble).IsEqualTo("");
        await Assert.That(result.TitleTrigger).IsEqualTo("");
    }

    [Test]
    public async Task FromRegistryFormat_HandlesFieldsWithCommas()
    {
        // Arrange - Note: Real registry format shouldn't have commas within fields,
        // but testing that commas WITHIN brackets don't break parsing
        const string registryFormat = "[Title with, comma],[Pre],[^v],[Post],[Trigger]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert - The split on "],["preserves commas within fields
        await Assert.That(result.Title).IsEqualTo("Title with, comma");
        await Assert.That(result.Preamble).IsEqualTo("Pre");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("Post");
        await Assert.That(result.TitleTrigger).IsEqualTo("Trigger");
    }

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        // Arrange
        var original = new QuickPasteFormattingString
        {
            Title = "Word Document",
            Preamble = "^{HOME}#DATE#{ENTER}",
            PasteKeystrokes = "^v",
            Postamble = "{ENTER}{ENTER}#SEQUENCE#",
            TitleTrigger = "Microsoft Word",
        };

        // Act
        var registryFormat = original.ToRegistryFormat();
        var roundTripped = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(roundTripped.Title).IsEqualTo(original.Title);
        await Assert.That(roundTripped.Preamble).IsEqualTo(original.Preamble);
        await Assert.That(roundTripped.PasteKeystrokes).IsEqualTo(original.PasteKeystrokes);
        await Assert.That(roundTripped.Postamble).IsEqualTo(original.Postamble);
        await Assert.That(roundTripped.TitleTrigger).IsEqualTo(original.TitleTrigger);
    }

    [Test]
    public async Task FromRegistryFormat_HandlesActualRegistryExample_Default()
    {
        // Arrange - Real example from ClipMate 7.5 registry
        const string registryFormat = "[Default - Ctrl+V],[],[^v],[],[*]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Default - Ctrl+V");
        await Assert.That(result.Preamble).IsEqualTo("");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("");
        await Assert.That(result.TitleTrigger).IsEqualTo("*");
    }

    [Test]
    public async Task FromRegistryFormat_HandlesActualRegistryExample_Excel()
    {
        // Arrange - Real example from ClipMate 7.5 registry
        const string registryFormat = "[Excel - Fill Column],[^{HOME}^@{DOWN}],[^v],[{DOWN}],[Microsoft Excel]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Excel - Fill Column");
        await Assert.That(result.Preamble).IsEqualTo("^{HOME}^@{DOWN}");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("{DOWN}");
        await Assert.That(result.TitleTrigger).IsEqualTo("Microsoft Excel");
    }

    [Test]
    public async Task FromRegistryFormat_HandlesActualRegistryExample_CommandPrompt()
    {
        // Arrange - Real example from ClipMate 7.5 registry
        const string registryFormat = "[Command Prompt],[],[~{INSERT}],[{ENTER}],[]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Command Prompt");
        await Assert.That(result.Preamble).IsEqualTo("");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("~{INSERT}");
        await Assert.That(result.Postamble).IsEqualTo("{ENTER}");
        await Assert.That(result.TitleTrigger).IsEqualTo("");
    }

    [Test]
    public async Task FromRegistryFormat_HandlesActualRegistryExample_WordDate()
    {
        // Arrange - Real example from ClipMate 7.5 registry
        const string registryFormat = "[Word with Date],[#CURRENTDATE# - ],[^v],[],[Microsoft Word]";

        // Act
        var result = QuickPasteFormattingString.FromRegistryFormat(registryFormat);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Word with Date");
        await Assert.That(result.Preamble).IsEqualTo("#CURRENTDATE# - ");
        await Assert.That(result.PasteKeystrokes).IsEqualTo("^v");
        await Assert.That(result.Postamble).IsEqualTo("");
        await Assert.That(result.TitleTrigger).IsEqualTo("Microsoft Word");
    }

    [Test]
    public async Task Constructor_InitializesEmptyStrings()
    {
        // Act
        var formattingString = new QuickPasteFormattingString();

        // Assert
        await Assert.That(formattingString.Title).IsEqualTo("");
        await Assert.That(formattingString.Preamble).IsEqualTo("");
        await Assert.That(formattingString.PasteKeystrokes).IsEqualTo("");
        await Assert.That(formattingString.Postamble).IsEqualTo("");
        await Assert.That(formattingString.TitleTrigger).IsEqualTo("");
    }
}
