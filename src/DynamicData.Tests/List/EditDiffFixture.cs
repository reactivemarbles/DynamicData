using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class EditDiffFixture : IDisposable
{
    private readonly SourceList<Person> _cache;

    private readonly ChangeSetAggregator<Person> _result;

    public EditDiffFixture()
    {
        _cache = new SourceList<Person>();
        _result = _cache.Connect().AsAggregator();
        _cache.AddRange(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
    }

    [Fact]
    public void Amends()
    {
        var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
        _cache.EditDiff(newList, Person.NameAgeGenderComparer);

        _cache.Count.Should().Be(3);

        var lastChange = _result.Messages.Last();
        lastChange.Adds.Should().Be(3);
        lastChange.Removes.Should().Be(10);

        _cache.Items.Should().BeEquivalentTo(newList);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _result.Dispose();
    }

    [Fact]
    public void EditWithSameData()
    {
        var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, Person.NameAgeGenderComparer);

        _cache.Count.Should().Be(10);
        _cache.Items.Should().BeEquivalentTo(newPeople);
        _result.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void New()
    {
        var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, Person.NameAgeGenderComparer);

        _cache.Count.Should().Be(15);
        _cache.Items.Should().BeEquivalentTo(newPeople);
        var lastChange = _result.Messages.Last();
        lastChange.Adds.Should().Be(5);
    }

    [Fact]
    public void Removes()
    {
        var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
        _cache.EditDiff(newList, Person.NameAgeGenderComparer);

        _cache.Count.Should().Be(7);

        var lastChange = _result.Messages.Last();
        lastChange.Adds.Should().Be(0);
        lastChange.Removes.Should().Be(3);

        _cache.Items.Should().BeEquivalentTo(newList);
    }

    [Fact]
    public void VariousChanges()
    {
        var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newList, Person.NameAgeGenderComparer);

        _cache.Count.Should().Be(10);

        var lastChange = _result.Messages.Last();
        lastChange.Adds.Should().Be(5);
        lastChange.Removes.Should().Be(5);

        _cache.Items.Should().BeEquivalentTo(newList);
    }
}
