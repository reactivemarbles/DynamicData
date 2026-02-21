using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

namespace DynamicData.Tests.Utilities;

public static class ListChangeSetAssertions
{
    public static void ShouldHaveRefreshed<T>(
                this    IChangeSet<T>   changeSet,
                        IEnumerable<T>  expectedItems,
                        string          because = "")
            where T : notnull
        => changeSet
            .Where(static change => change.Reason is ListChangeReason.Refresh)
            .Select(static change => change.Item.Current)
            .Should().BeEquivalentTo(expectedItems, because);
}
