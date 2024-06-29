using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class DistinctValuesFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<Person> _source;

    public DistinctValuesFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
    }

    [Fact]
    public void AddingRemovedItem()
    {
        var person = new Person("A", 20);

        _source.Add(person);
        _source.Remove(person);
        _source.Add(person);

        _results.Messages.Count.Should().Be(3, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        _results.Data.Items.Should().BeEquivalentTo(new[] { 20 });
        _results.Messages.ElementAt(0).Adds.Should().Be(1, "First message should be an add");
        _results.Messages.ElementAt(1).Removes.Should().Be(1, "Second message should be a remove");
        _results.Messages.ElementAt(2).Adds.Should().Be(1, "Third message should be an add");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void DuplicatedResultsResultInNoAdditionalMessage()
    {
        _source.Edit(
            list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 update message");
        _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
        _results.Data.Items[0].Should().Be(20, "Should 20");
    }

    [Fact]
    public void FiresAddWhenaNewItemIsAdded()
    {
        _source.Add(new Person("Person1", 20));

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(20, "Should 20");
    }

    [Fact]
    public void FiresBatchResultOnce()
    {
        _source.Edit(
            list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person2", 21));
                list.Add(new Person("Person3", 22));
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(3, "Should be 3 items in the cache");

        _results.Data.Items.Should().BeEquivalentTo(new[] { 20, 21, 22 });
        _results.Data.Items[0].Should().Be(20, "Should 20");
    }

    [Fact]
    public void RemovingAnItemRemovesTheDistinct()
    {
        var person = new Person("Person1", 20);

        _source.Add(person);
        _source.Remove(person);
        _results.Messages.Count.Should().Be(2, "Should be 1 update message");
        _results.Data.Count.Should().Be(0, "Should be 1 items in the cache");

        _results.Messages.First().Adds.Should().Be(1, "First message should be an add");
        _results.Messages.Skip(1).First().Removes.Should().Be(1, "Second messsage should be a remove");
    }

    [Fact]
    public void Replacing()
    {
        var person = new Person("A", 20);
        var replaceWith = new Person("A", 21);

        _source.Add(person);
        _source.Replace(person, replaceWith);
        _results.Messages.Count.Should().Be(2, "Should be 1 update message");
        _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");

        _results.Messages.First().Adds.Should().Be(1, "First message should be an add");
        _results.Messages.Skip(1).First().Count.Should().Be(2, "Second messsage should be an add an a remove");
    }
}
