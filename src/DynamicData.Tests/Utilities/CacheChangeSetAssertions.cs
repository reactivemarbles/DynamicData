using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

namespace DynamicData.Tests.Utilities;

public static class CacheChangeSetAssertions
{
    public static void ShouldHaveRefreshed<TObject, TKey>(
            this    IChangeSet<TObject, TKey>   changeSet,
                    IEnumerable<TObject>        expectedItems,
                    string                      because = "")
        => changeSet
            .Where(static change => change.Reason is ChangeReason.Refresh)
            .Select(static change => change.Current)
            .Should().BeEquivalentTo(expectedItems, because);
}
