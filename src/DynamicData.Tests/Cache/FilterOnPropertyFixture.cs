using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class FilterOnPropertyFixture
{
    [Fact]
    public void ChangeAValueSoItIsStillInTheFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddOrUpdate(people);

        people[50].Age = 100;

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Data.Count.Should().Be(82);
    }

    [Fact]
    public void ChangeAValueToMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddOrUpdate(people);

        people[20].Age = 10;

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Data.Count.Should().Be(81);
    }

    [Fact]
    public void ChangeAValueToNoLongerMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddOrUpdate(people);

        people[10].Age = 20;

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Data.Count.Should().Be(83);
    }

    [Fact]
    public void InitialValues()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddOrUpdate(people);

        stub.Results.Messages.Count.Should().Be(1);
        stub.Results.Data.Count.Should().Be(82);

        stub.Results.Data.Items.Should().BeEquivalentTo(people.Skip(18));
    }

    private class FilterPropertyStub : IDisposable
    {
        public FilterPropertyStub() => Results = new ChangeSetAggregator<Person, string>(Source.Connect().FilterOnProperty(p => p.Age, p => p.Age > 18));

        public ChangeSetAggregator<Person, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
