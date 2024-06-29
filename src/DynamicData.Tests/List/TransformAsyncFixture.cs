using System;
using System.Linq;
using System.Threading.Tasks;

using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List;

[Obsolete("Not obsolete - test commented out due to test run freezing on Appveyor")]
public class TransformAsyncFixture : IDisposable
{
    private readonly Func<Person, Task<PersonWithGender>> _transformFactory = p =>
    {
        var gender = p.Age % 2 == 0 ? "M" : "F";
        var transformed = new PersonWithGender(p, gender);
        return Task.FromResult(transformed);
    };

    private readonly ChangeSetAggregator<PersonWithGender> _results;

    private readonly ISourceList<Person> _source;

    public TransformAsyncFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<PersonWithGender>(_source.Connect().TransformAsync(_transformFactory));
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    /*

     */

    [Fact]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var transformed = await _transformFactory(person);
        _results.Data.Items[0].Should().Be(transformed, "Should be same person");
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.Add(person);
        _source.Remove(person);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.Add(newperson);
        _source.Add(updated);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        _results.Messages[0].Replaced.Should().Be(0, "Should be 1 update");
    }

    [Fact]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddRange(people);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var tasks = people.Select(_transformFactory);
        var result = await Task.WhenAll(tasks);

        var transformed = result.OrderBy(p => p.Age).ToArray();
        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(_results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
    }

    [Fact]
    public void SameKeyChanges()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        _source.AddRange(people);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(10, "Should return 10 adds");
        _results.Data.Count.Should().Be(10, "Should result in 10 records");
    }

    [Fact]
    public void Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddRange(people);
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    /// <summary>
    /// This test is disabled as it was flaky.
    /// https://github.com/reactivemarbles/DynamicData/pull/625
    /// </summary>
    [Fact]
    private void TransformOnRefresh()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age)
            .TransformAsync(Task.FromResult, transformOnRefresh: true).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(1);

        items[0].Age = 10;
        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(2);

        results.Messages[1].First().Reason.Should().Be(ListChangeReason.Replace);

        //remove an item and check no change is fired
        var toRemove = items[1];
        list.Remove(toRemove);
        results.Data.Count.Should().Be(99);
        results.Messages.Count.Should().Be(3);
        toRemove.Age = 100;
        results.Messages.Count.Should().Be(3);

        //add it back in and check it updates
        list.Add(toRemove);
        results.Messages.Count.Should().Be(4);
        toRemove.Age = 101;
        results.Messages.Count.Should().Be(5);

        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Replace);
    }
}
