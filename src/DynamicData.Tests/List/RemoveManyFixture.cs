using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class RemoveManyFixture
{
    private readonly List<int> _list;

    public RemoveManyFixture() => _list = new List<int>();

    [Fact]
    public void DoesNotRemoveDuplicates()
    {
        _list.AddRange(new[] { 1, 1, 1, 5, 6, 7 });
        _list.RemoveMany(new[] { 1, 1, 7 });
        _list.Should().BeEquivalentTo(new[] { 1, 5, 6 });
    }

    [Fact]
    public void RemoveLargeBatch()
    {
        var toAdd = Enumerable.Range(1, 10000).ToArray();
        _list.AddRange(toAdd);

        var toRemove = _list.Take(_list.Count / 2).OrderBy(x => Guid.NewGuid()).ToArray();
        _list.RemoveMany(toRemove);
        _list.Should().BeEquivalentTo(toAdd.Except(toRemove));
    }

    [Fact]
    public void RemoveManyWillRemoveARange()
    {
        _list.AddRange(Enumerable.Range(1, 10));
        _list.RemoveMany(Enumerable.Range(2, 8));
        _list.Should().BeEquivalentTo(new[] { 1, 10 });
    }
}
