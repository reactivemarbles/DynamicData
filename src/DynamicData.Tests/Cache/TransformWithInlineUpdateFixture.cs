using System;
using System.Linq;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformWithInlineUpdateFixture
{
    [Fact]
    public void InlineUpdate()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        var transformedPerson = stub.Results.Data.Items[0];

        var personUpdate = new Person("Adult1", 51);
        stub.Source.AddOrUpdate(personUpdate);

        var updatedTransform = stub.Results.Data.Items[0];

        updatedTransform.Age.Should().Be(personUpdate.Age, "Age should be updated from 50 to 51.");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        transformedPerson.Should().Be(stub.Results.Data.Items[0], "Should be same transformed person instance.");
    }

    [Fact]
    public void BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformWithInlineUpdateFixtureStub();
        stub.Source.AddOrUpdate(people);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var transformed = people.Select(stub.TransformFactory).OrderBy(p => p.Age).ToArray();
        stub.Results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(transformed, "Incorrect transform result");
    }

    [Fact]
    public void Clear()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should be 80 addes");
        stub.Results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformWithInlineUpdateFixtureStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        stub.Results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }


    [Fact]
    public void TransformOnRefresh()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub(true);
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        var transformedPerson = stub.Results.Data.Items[0];

        person.Age = 51;
        stub.Source.Refresh(person);

        var updatedTransform = stub.Results.Data.Items[0];

        updatedTransform.Age.Should().Be(51, "Age should be updated from 50 to 51.");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        transformedPerson.Should().Be(stub.Results.Data.Items[0], "Should be same transformed person instance.");
    }

    private class TransformWithInlineUpdateFixtureStub : IDisposable
    {
        public TransformWithInlineUpdateFixtureStub(bool transformOnRefresh = false)
        {
            Results = new ChangeSetAggregator<Person, string>(Source.Connect()
                .TransformWithInlineUpdate(TransformFactory, UpdateAction, transformOnRefresh));
        }

        public ChangeSetAggregator<Person, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Action<Person, Person> UpdateAction { get; } = (transformed, current) => transformed.Age=current.Age;

        public Func<Person, Person> TransformFactory { get; } = (p) => new Person(p.Name, p.Age);

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
