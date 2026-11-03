using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// <see cref="Collection.NotVirtual" /> is the queryable copy of <see cref="Collection.IsVirtual" />, so the two must never disagree.
/// </summary>
public class CollectionNotVirtualTests
{
    public static IEnumerable<Func<(CollectionLmType, CollectionListType)>> Combinations()
    {
        foreach (var lmType in Enum.GetValues<CollectionLmType>())
        {
            // 2 is not a named ListType but can be stored
            foreach (var listType in Enum.GetValues<CollectionListType>().Append((CollectionListType)2))
                yield return () => (lmType, listType);
        }
    }

    [Test]
    [MethodDataSource(nameof(Combinations))]
    public async Task NotVirtual_EveryLmTypeAndListType_IsTheOppositeOfIsVirtual(CollectionLmType lmType, CollectionListType listType)
    {
        // Arrange
        var collection = new Collection { LmType = lmType, ListType = listType };

        // Act
        var notVirtual = Collection.NotVirtual.Compile()(collection);

        // Assert
        await Assert.That(notVirtual).IsEqualTo(!collection.IsVirtual);
    }
}
