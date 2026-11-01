using ClipMate.App.Models;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// A "Search in" entry is found again by value in the dialog's combo, so drawing its icon when it is first shown must
/// not change how it compares: a changed hash made choosing "All Databases" or a Database snap back to the default.
/// </summary>
public class SearchInOptionTests
{
    [Test]
    public async Task IconImage_Drawn_LeavesTheEntryEqualToACopyWithTheSameHash()
    {
        // Arrange
        var option = new SearchInOption(SearchTarget.AllDatabases, "All Databases", 0, true) { Path = "All Databases", Icon = "🗃️" };
        var copy = option with { };
        var hashBefore = option.GetHashCode();

        // Act
        _ = option.IconImage;

        // Assert
        await Assert.That(option.GetHashCode()).IsEqualTo(hashBefore);
        await Assert.That(option).IsEqualTo(copy);
    }
}
