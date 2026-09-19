using DynamicData.Tests.Domain;

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

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");

        var transformed = await _transformFactory(person);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(transformed).Because("Should be same person");
    }

    [Test]
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.Add(newperson);
        _source.Add(updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[0].Replaced).IsEqualTo(0).Because("Should be 1 update");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddRange(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var tasks = people.Select(_transformFactory);
        var result = await Task.WhenAll(tasks);

        var transformed = result.OrderBy(p => p.Age).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(_results.Data.Items.OrderBy(p => p.Age)).Because("Incorrect transform result");
    }

    [Test]
    public async Task SameKeyChanges()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        _source.AddRange(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(10).Because("Should return 10 adds");
        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should result in 10 records");
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddRange(people);
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(100).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    /// <summary>
    /// This test is disabled as it was flaky.
    /// https://github.com/reactivemarbles/DynamicData/pull/625
    /// </summary>
    [Test]
    public async Task TransformOnRefresh()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age)
            .TransformAsync(Task.FromResult, transformOnRefresh: true).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        items[0].Age = 10;
        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(2);

        await Assert.That(results.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Replace);

        //remove an item and check no change is fired
        var toRemove = items[1];
        list.Remove(toRemove);
        await Assert.That(results.Data.Count).IsEqualTo(99);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        toRemove.Age = 100;
        await Assert.That(results.Messages.Count).IsEqualTo(3);

        //add it back in and check it updates
        list.Add(toRemove);
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        toRemove.Age = 101;
        await Assert.That(results.Messages.Count).IsEqualTo(5);

        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Replace);
    }

    [Test]
    public async Task TransformAsyncCancelsTokenOnUnSubscribe()
    {
        using var source = new SourceList<Person>();
        var tcs = new TaskCompletionSource<Person>();
        using var sub = source.Connect()
            .TransformAsync<Person, Person>(async (c, p, count, cancel) =>
            {
                using (cancel.Register(() => tcs.SetCanceled(), useSynchronizationContext: false))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            })
            .Subscribe();

        source.Add(new Person());

        sub.Dispose();
        await Assert.That(tcs.Task.IsCanceled).IsTrue();
    }
}
