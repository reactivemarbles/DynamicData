#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class SortMutableFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<IComparer<Person>> _changeComparer;

    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

    private readonly RandomPersonGenerator _generator = new();

    private readonly ReactiveUI.Primitives.Signals.ISignal<Unit> _resort;

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public SortMutableFixture()
    {
        _source = new SourceList<Person>();
        _changeComparer = new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>>(_comparer);
        _resort = new ReactiveUI.Primitives.Signals.Signal<Unit>();

        _results = _source.Connect().Sort(_changeComparer, resetThreshold: 25, resort: _resort).AsAggregator();
    }

    [Test]
    public async Task ChangeComparer()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var newComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

        _changeComparer.OnNext(newComparer);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, newComparer);
        var actualResult = _results.Data.Items;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
        _changeComparer.Dispose();
        _resort.Dispose();
    }

    [Test]
    public async Task Insert()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbeLast = new Person("__A", 10000);
        _source.Add(shouldbeLast);

        await Assert.That(_results.Data.Count).IsEqualTo(101);

        await Assert.That(_results.Data.Items[^1]).IsEqualTo(shouldbeLast);
    }

    [Test]
    public async Task Remove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var toRemove = people.ElementAt(20);
        people.RemoveAt(20);
        _source.RemoveAt(20);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");
        await Assert.That(_results.Messages[1].First().Item.Current).IsEqualTo(toRemove).Because("Incorrect item removed");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyOdds()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var odd = people.Select((p, idx) => new { p, idx }).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

        _source.RemoveMany(odd);

        await Assert.That(_results.Data.Count).IsEqualTo(50).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task RemoveManyReverseOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 99 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task Replace()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbeLast = new Person("__A", 999);
        _source.ReplaceAt(10, shouldbeLast);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        await Assert.That(_results.Data.Items[^1]).IsEqualTo(shouldbeLast);
    }

    [Test]
    public async Task Resort()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        people.OrderBy(_ => Guid.NewGuid()).ForEach((person, index) => { person.Age = index; });

        _resort.OnNext(Unit.Default);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task ResortOnInlineChanges()
    {
        var people = _generator.Take(10).ToList();
        _source.AddRange(people);

        people[0].Age = -1;
        people[1].Age = -10;
        people[2].Age = -12;
        people[3].Age = -5;
        people[4].Age = -7;
        people[5].Age = -6;

        var comparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

        _changeComparer.OnNext(comparer);

        var expectedResult = people.OrderBy(p => p, comparer).ToArray();
        var actualResult = _results.Data.Items.ToArray();

        //actualResult.(expectedResult);
        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100);

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task UpdateMoreThanThreshold()
    {
        var allPeople = _generator.Take(1100).ToList();
        var people = allPeople.Take(100).ToArray();
        _source.AddRange(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var morePeople = allPeople.Skip(100).ToArray();
        _source.AddRange(morePeople);

        await Assert.That(_results.Data.Count).IsEqualTo(1100).Because("Should be 1100 people in the cache");
        var expectedResult = people.Union(morePeople).OrderBy(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 messages");

        var lastMessage = _results.Messages.Last();
        await Assert.That(lastMessage.First().Range.Count).IsEqualTo(100).Because("Should be 100 in the range");
        await Assert.That(lastMessage.First().Reason).IsEqualTo(ListChangeReason.Clear);

        await Assert.That(lastMessage.Last().Range.Count).IsEqualTo(1100).Because("Should be 1100 in the range");
        await Assert.That(lastMessage.Last().Reason).IsEqualTo(ListChangeReason.AddRange);
    }
}
