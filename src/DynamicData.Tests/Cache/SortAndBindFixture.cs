#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

// Bind to a list
[InheritsTests]
public sealed class SortByAndBindToList : SortAndBindFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new List<Person>(100);
        var aggregator = _source.Connect().SortAndBind(list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a list
[InheritsTests]
public sealed class SortAndBindToList : SortAndBindFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new List<Person>(100);
        var aggregator = _source.Connect().SortAndBind(list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a list using default comparer
[InheritsTests]
public sealed class SortAndBindToListDefaultComparer : SortAndBindFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new List<Person>(100);
        var aggregator = _source.Connect().SortAndBind(list).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to an observable collection
[InheritsTests]
public sealed class SortAndBindToObservableCollection : SortAndBindFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new ObservableCollection<Person>(new List<Person>(100));
        var aggregator = _source.Connect().SortAndBind(list, _comparer).AsAggregator();
        return (aggregator, list);
    }
}

// Bind to a binding list
[InheritsTests]
public sealed class SortAndBindToBindingList : SortAndBindFixture

{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var list = new ObservableCollection<Person>(new BindingList<Person>());
        var aggregator = _source.Connect().SortAndBind(list, _comparer).AsAggregator();
        return (aggregator, list);
    }
}

// Bind to a readonly observable collection
[InheritsTests]
public sealed class SortAndBindToReadOnlyObservableCollection : SortAndBindFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var aggregator = _source.Connect().SortAndBind(out var list, _comparer).AsAggregator();

        return (aggregator, list);
    }
}

// Bind to a readonly observable collection using binary search
[InheritsTests]
public sealed class SortAndBindWithBinarySearch1 : SortAndBindFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var options = new SortAndBindOptions { UseBinarySearch = true, UseReplaceForUpdates = false };
        var aggregator = _source.Connect().SortAndBind(out var list, _comparer, options).AsAggregator();

        return (aggregator, list);
    }
}

[InheritsTests]
public sealed class SortAndBindWithBinarySearch2 : SortAndBindFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var options = new SortAndBindOptions { UseBinarySearch = true, UseReplaceForUpdates = true };
        var aggregator = _source.Connect().SortAndBind(out var list, _comparer, options).AsAggregator();

        return (aggregator, list);
    }
}

public class SortAndBindBinarySearch_ForSameKeyAndObjectValues : IDisposable
{
    private readonly List<int> _target = new();
    private readonly SourceCache<int, int> _strings = new(i => i);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UpdateAnyWhereShouldNotBreak(bool useReplaceForUpdates)
    {
        var options = new SortAndBindOptions { UseBinarySearch = true, UseReplaceForUpdates = useReplaceForUpdates };

        using var subscription = _strings.Connect().SortAndBind(_target, SortExpressionComparer<int>.Ascending(i => i), options).Subscribe();

        var items = Enumerable.Range(1, 10).ToList();

        _strings.AddOrUpdate(items);
        _strings.AddOrUpdate(1);
        _strings.AddOrUpdate(5);
        _strings.AddOrUpdate(10);

        await Assert.That(_target.SequenceEqual(items)).IsTrue();
    }

    public void Dispose() => _strings.Dispose();
}

// Bind to a readonly observable collection - using default comparer
[InheritsTests]
public sealed class SortAndBindToReadOnlyObservableCollectionDefaultComparer : SortAndBindFixture
{
    protected override (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests()
    {
        var aggregator = _source.Connect().SortAndBind(out var list).AsAggregator();

        return (aggregator, list);
    }
}

public sealed class SortAndBindWithResetOptions : IDisposable
{

    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
    private readonly ISourceCache<Person, string> _source = new SourceCache<Person, string>(p => p.Key);

    private readonly List<NotifyCollectionChangedEventArgs> _collectionChangedEventArgs = new();

    [Test]
    [Description("Check reset is fired  when below threshold only.  Historically first time load always fired reset for first time load.")]
    public async Task FiresResetWhenThresholdIsMet()
    {
        var options = new SortAndBindOptions { ResetThreshold = 10 };

        using var sorted = _source.Connect().SortAndBind(out var list, _comparer, options).Subscribe();
        using var collectionChangedEvents = list.ObserveCollectionChanges().Select(e => e.EventArgs).Subscribe(_collectionChangedEventArgs.Add);

        // fire 5 changes, should always reset because it's below the threshold
        _source.AddOrUpdate(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(5);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Add)).IsTrue();

        _collectionChangedEventArgs.Clear();

        // fire 15 changes, we should get a refresh event
        _source.AddOrUpdate(Enumerable.Range(10, 15).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(1);
        await Assert.That(_collectionChangedEventArgs[0].Action).IsEqualTo(NotifyCollectionChangedAction.Reset);

        _collectionChangedEventArgs.Clear();

        // fires further 5 changes, should result individual notifications
        _source.AddOrUpdate(Enumerable.Range(-10, 5).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(5);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Add)).IsTrue();

        await Assert.That(list.Count).IsEqualTo(25);

    }

    [Test]
    [Description("Check reset is not fired")]
    public async Task NeverFireReset()
    {
        var options = new SortAndBindOptions { ResetThreshold = int.MaxValue };

        using var sorted = _source.Connect().SortAndBind(out var list, _comparer, options).Subscribe();
        using var collectionChangedEvents = list.ObserveCollectionChanges().Select(e => e.EventArgs).Subscribe(_collectionChangedEventArgs.Add);

        // fire 5 changes, should not reset because it's below the threshold
        _source.AddOrUpdate(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(5);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Add)).IsTrue();

        _collectionChangedEventArgs.Clear();

        // fire 15 changes, we should get a refresh event
        _source.AddOrUpdate(Enumerable.Range(10, 15).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(15);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Add)).IsTrue();

        await Assert.That(list.Count).IsEqualTo(20);

    }

    [Test]
    [Description("Check reset is fired  on first time load. This checks historic first time load opt-in.")]
    public async Task FireResetOnFirstTimeLoad()
    {
        var options = new SortAndBindOptions { ResetThreshold = 10, ResetOnFirstTimeLoad = true };

        using var sorted = _source.Connect().SortAndBind(out var list, _comparer, options).Subscribe();
        using var collectionChangedEvents = list.ObserveCollectionChanges().Select(e => e.EventArgs).Subscribe(_collectionChangedEventArgs.Add);

        // fire 5 changes, should always reset even though it's below the threshold
        _source.AddOrUpdate(Enumerable.Range(0, 5).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(1);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Reset)).IsTrue();

        _collectionChangedEventArgs.Clear();

        // fire 15 changes, we should get a refresh event
        _source.AddOrUpdate(Enumerable.Range(10, 15).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(1);
        await Assert.That(_collectionChangedEventArgs[0].Action).IsEqualTo(NotifyCollectionChangedAction.Reset);

        _collectionChangedEventArgs.Clear();

        // fires further 5 changes, should result individual notifications
        _source.AddOrUpdate(Enumerable.Range(-10, 5).Select(i => new Person($"P{i}", i)));
        await Assert.That(_collectionChangedEventArgs.Count).IsEqualTo(5);
        await Assert.That(_collectionChangedEventArgs.All(a => a.Action == NotifyCollectionChangedAction.Add)).IsTrue();

        await Assert.That(list.Count).IsEqualTo(25);

    }

    public void Dispose() => _source.Dispose();
}

public abstract class SortAndBindFixture : IDisposable
{

    private readonly RandomPersonGenerator _generator = new();
    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly IList<Person> _boundList;

    protected readonly IComparer<Person> _comparer = Person.DefaultComparer;
    protected readonly ISourceCache<Person, string> _source = new SourceCache<Person, string>(p => p.Key);

    public SortAndBindFixture()
    {
        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        var args = SetUpTests();
#pragma warning restore CA2214

        // bind and sort in one hit

        _results = args.Aggregrator;
        _boundList = args.List;

    }

    protected abstract (ChangeSetAggregator<Person, string> Aggregrator, IList<Person> List) SetUpTests();

    [Test]
    public async Task InsertAtBeginning()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        // check initial data set is sorted
        await Assert.That(_boundList.Count).IsEqualTo(100);
        await Assert.That(_boundList.SequenceEqual(people.OrderBy(p => p, _comparer))).IsTrue();

        //create age 0 to ensure it is inserted first
        var insert = new Person("_Aaron", 0);
        _source.AddOrUpdate(insert);

        await Assert.That(_boundList.Count).IsEqualTo(101);

        var firstItem = _boundList[0];

        await Assert.That(insert).IsEqualTo(firstItem);

        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();

    }

    [Test]
    public async Task InsertAtEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toInsert = new Person("zzzzz", 1000);

        _source.AddOrUpdate(toInsert);

        await Assert.That(_boundList.Count).IsEqualTo(101);

        var last = _boundList[^1];
        await Assert.That(last).IsEqualTo(toInsert);

        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();
    }

    [Test]
    public async Task InsertInMiddle()
    {
        _source.AddOrUpdate(Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)));

        //create age 0 to ensure it is inserted first
        var insert = new Person("Marvin", 50);

        _source.AddOrUpdate(insert);

        await Assert.That(_boundList.Count).IsEqualTo(101);

        var index = _boundList.IndexOf(insert);

        await Assert.That(index).IsEqualTo(50);

        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();
    }

    [Test]
    public async Task InsertSameLocation()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person($"P{i}", i * 10)));

        // each of these changes should result in index 1
        await UpdateAndAssetPosition(new Person("P2", 15), 1);
        await UpdateAndAssetPosition(new Person("P2", 20), 1);
        await UpdateAndAssetPosition(new Person("P2", 25), 1);

        async Task UpdateAndAssetPosition(Person person, int expectedIndex)
        {
            _source.AddOrUpdate(person);

            // check the item has been replaced
            await Assert.That(_boundList.Count(p => p.Key == person.Key)).IsEqualTo(1);

            await Assert.That(_boundList[expectedIndex]).IsEqualTo(person);

        }

        await Assert.That(_boundList.Count).IsEqualTo(10);

        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();
    }

    [Test]
    public async Task Refresh()
    {
        _source.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person($"P{i}", i * 10)));

        // each of these changes should result in index 1

        var toRefresh = _boundList[1];

        // there will all result in the same position
        await RefreshAtAndAssetPosition(toRefresh, p => p.Age = 15, 1);
        await RefreshAtAndAssetPosition(toRefresh, p => p.Age = 20, 1);
        await RefreshAtAndAssetPosition(toRefresh, p => p.Age = 25, 1);

        // move after
        await RefreshAtAndAssetPosition(toRefresh, p => p.Age = 45, 3);

        async Task RefreshAtAndAssetPosition(Person person, Action<Person> action, int expectedIndex)
        {
            action(person);
            _source.Edit(innerCache => innerCache.Refresh(person.Key));

            // check the item has been replaced
            await Assert.That(_boundList.Count(p => p.Key == person.Key)).IsEqualTo(1);

            await Assert.That(_boundList[expectedIndex]).IsEqualTo(person);

        }

        await Assert.That(_boundList.Count).IsEqualTo(10);

        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();
    }

    [Test]
    public async Task BatchOfVariousChanges()
    {
        var people = Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = people[3];

        // mixture of add, updates and removed
        _source.Edit(innerCache =>
        {
            innerCache.Remove(people[0].Key);
            innerCache.AddOrUpdate(new Person("Mr", "Z", 50, "M"));
            innerCache.Remove(people[1].Key);
            innerCache.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
            innerCache.Remove(people[7].Key);
        });

        // mirror actions to caches
        var expected = Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)).ToList();
        expected.RemoveAt(IndexFromKey(people[0].Key));
        expected.RemoveAt(IndexFromKey(people[1].Key));
        expected.Add(new Person("Mr", "Z", 50, "M"));
        expected[IndexFromKey(toupdate.Key)] = new Person(toupdate.Name, toupdate.Age - 24);
        expected.RemoveAt(IndexFromKey(people[7].Key));

        int IndexFromKey(string key) => expected.FindIndex(p => p.Key == key);

        var expectedInOrder = expected.OrderBy(p => p, _comparer).ToList();

        await Assert.That(expectedInOrder.SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task BatchOfVariousEndingInClear()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        _source.Edit(updater =>
            {
                updater.Clear();
                updater.AddOrUpdate(_generator.Take(10).ToArray());
                updater.Clear();
            });

        await Assert.That(_boundList.Count).IsEqualTo(0);

    }

    [Test]
    public async Task LargeBatchChange()
    {
        // this should produce what are effectively 2 resets for the bound collection
        _source.AddOrUpdate(Enumerable.Range(0, 100).Select(i => new Person($"P{i}", i)));
        _source.AddOrUpdate(Enumerable.Range(100, 100).Select(i => new Person($"P{i}", i)));

        await Assert.That(_boundList.Count).IsEqualTo(200);
        await Assert.That(_boundList.SequenceEqual(_source.Items.OrderBy(p => p, _comparer))).IsTrue();
    }

    [Test]
    public async Task BatchUpdateShiftingIndicies()
    {
        var testData = new[]
        {
            new Person("A", 3),
            new Person("B", 5),
            new Person("C", 7),
            new Person("D", 8),
            new Person("E", 10),
            new Person("F", 12),
            new Person("G", 14)
        };
        _source.AddOrUpdate(testData);

        _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person(testData[0].Name, 6));
                updater.AddOrUpdate(new Person(testData[3].Name, 2));
            });

        var expected = new[]
        {
            new Person("D", 2),
            new Person("B", 5),
            new Person("A", 6),
            new Person("C", 7),
            new Person("E", 10),
            new Person("F", 12),
            new Person("G", 14)
        };

        await Assert.That(_boundList.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task RemoveFirst()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_boundList.Count).IsEqualTo(100);

        _source.Remove(people[0].Key);
        await Assert.That(_boundList.Count).IsEqualTo(99);

        people.RemoveAt(0);

        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();

    }

    [Test]
    public async Task RemoveFromEnd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_boundList.Count).IsEqualTo(100);

        _source.Remove(people[99].Key);
        await Assert.That(_boundList.Count).IsEqualTo(99);

        people.RemoveAt(99);
        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task RemoveFromMiddle()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_boundList.Count).IsEqualTo(100);

        _source.Remove(people[50].Key);
        await Assert.That(_boundList.Count).IsEqualTo(99);

        people.RemoveAt(IndexFromKey(people[50].Key));
        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_boundList.Count).IsEqualTo(100);
        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task UpdateFirst()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[0];

        var update = new Person(toUpdate.Name, toUpdate.Age + 5);

        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(update.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);

        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task UpdateLast()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[^1];

        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(toUpdate.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);

        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();
    }

    [Test]
    public async Task UpdateMiddle()
    {
        //TODO: fixed Text

        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        var toUpdate = _boundList[50];

        _source.AddOrUpdate(new Person(toUpdate.Name, toUpdate.Age + 5));

        people[IndexFromKey(toUpdate.Key)] = new Person(toUpdate.Name, toUpdate.Age + 5);

        int IndexFromKey(string key) => people.FindIndex(p => p.Key == key);

        await Assert.That(people.OrderBy(p => p, _comparer).SequenceEqual(_boundList)).IsTrue();

    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
