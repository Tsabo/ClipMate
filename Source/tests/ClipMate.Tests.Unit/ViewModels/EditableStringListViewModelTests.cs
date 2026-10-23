using ClipMate.App.ViewModels;

namespace ClipMate.Tests.Unit.ViewModels;

public class EditableStringListViewModelTests
{
    [Test]
    public async Task Add_AppendsTheTrimmedValue_AndSkipsDuplicatesAndCancels()
    {
        // Arrange: the prompt answers "  NOTEPAD:  ", then a duplicate in other case, then cancel
        var answers = new Queue<string?>(["  NOTEPAD:  ", "notepad:", null]);
        var list = new EditableStringListViewModel(_ => answers.Dequeue());
        list.Load(["WINWORD:OPUSAPP"]);

        // Act
        list.AddCommand.Execute(null);
        list.AddCommand.Execute(null);
        list.AddCommand.Execute(null);

        // Assert
        await Assert.That(list.ToList()).IsEquivalentTo(new[] { "WINWORD:OPUSAPP", "NOTEPAD:" });
    }

    [Test]
    public async Task Edit_PromptsWithTheSelectedValue_AndReplacesIt()
    {
        // Arrange
        string? prompted = null;
        var list = new EditableStringListViewModel(current =>
        {
            prompted = current;
            return "DEFAULT IME";
        });
        list.Load(["INTELLIFORMPROXY", "DEFAULT"]);
        list.SelectedIndex = 1;

        // Act
        list.EditCommand.Execute(null);

        // Assert
        await Assert.That(prompted).IsEqualTo("DEFAULT");
        await Assert.That(list.ToList()).IsEquivalentTo(new[] { "INTELLIFORMPROXY", "DEFAULT IME" });
    }

    [Test]
    public async Task Delete_RemovesTheSelectedValue_AndDoesNothingWithoutASelection()
    {
        // Arrange
        var list = new EditableStringListViewModel(_ => null);
        list.Load(["A:", "B:", "C:"]);

        // Act
        list.DeleteCommand.Execute(null);
        list.SelectedIndex = 1;
        list.DeleteCommand.Execute(null);

        // Assert
        await Assert.That(list.ToList()).IsEquivalentTo(new[] { "A:", "C:" });
    }
}
