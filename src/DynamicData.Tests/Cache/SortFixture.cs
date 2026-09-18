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

namespace DynamicData.Tests.Cache;

public class SortFixtureWithReorder : IDisposable
{
    private readonly IComparer<Person> _comparer;

    private readonly RandomPersonGenerator _generator = new();

    private readonly SortedChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public SortFixtureWithReorder()
    {
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

        _source = new SourceCache<Person, string>(p => p.Key);
        _results = new SortedChangeSetAggregator<Person, string>(_source.Connect().Sort(_comparer));
    }

    [Test]
    public async Task AppendAtBeginning()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("_Aaron", 0);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("_Aaron");

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(indexedItem.Value.Index).IsEqualTo(0).Because("Inserted item should have index of zero");
    }

    [Test]
    public async Task AppendAtEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("zzzzz", 1000);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("zzzzz");

        await Assert.That(indexedItem.HasValue).IsTrue();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task AppendInMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("Marvin", 50);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("Marvin");

        await Assert.That(indexedItem.HasValue).IsTrue();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task BatchUpdate1()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.Remove(people[0].Key);
                updater.Remove(people[1].Key);
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.Remove(people[7]);
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate2()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "Z", 50, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate3()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[7];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "A", 10, "M"));
                updater.AddOrUpdate(new Person("Mr", "B", 40, "M"));
                updater.AddOrUpdate(new Person("Mr", "C", 70, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate4()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "A", 10, "M"));
                updater.Remove(people[5]);
                updater.AddOrUpdate(new Person("Mr", "C", 70, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate6()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        _source.Edit(
            updater =>
            {
                updater.Clear();
                updater.AddOrUpdate(_generator.Take(10).ToArray());
                updater.Clear();
            });

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdateWhereUpdateMovesTheIndexDown()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.Remove(people[0].Key);
                updater.Remove(people[1].Key);

                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age + 50));

                updater.AddOrUpdate(_generator.Take(2));

                updater.Remove(people[7]);
            });

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));
        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task DoesNotThrow1()
    {
        var cache = new SourceCache<Data, int>(d => d.Id);
        var sortPump = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        var disposable = cache.Connect().Sort(SortExpressionComparer<Data>.Ascending(d => d.Id), sortPump).Subscribe();

        disposable.Dispose();
    }

    [Test]
    public async Task DoesNotThrow2()
    {
        var cache = new SourceCache<Data, int>(d => d.Id);
        var disposable = cache.Connect().Sort(new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Data>>(SortExpressionComparer<Data>.Ascending(d => d.Id))).Subscribe();

        disposable.Dispose();
    }

    [Test]
    public async Task InlineUpdateProducesAReplace()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var toupdate = people[3];

        _source.AddOrUpdate(new Person(toupdate.Name, toupdate.Age + 1));

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));
        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task RemoveFirst()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems[0];

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");
        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task RemoveFromEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems[^1];

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");

        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task RemoveFromMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems.Skip(50).First();

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");

        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task SortAfterFilter()
    {
        var source = new SourceCache<Person, string>(p => p.Key);

        var filterSubject = new ReactiveUI.Primitives.Signals.StateSignal<Func<Person, bool>>(p => true);

        var agg = new SortedChangeSetAggregator<ViewModel, TestString>(source.Connect().Filter(filterSubject).Group(x => (TestString)x.Key).Transform(x => new ViewModel(x.Key)).Sort(new ViewModel.Comparer()));

        source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("A", 1, "F"));
                x.AddOrUpdate(new Person("a", 1, "M"));
                x.AddOrUpdate(new Person("B", 1, "F"));
                x.AddOrUpdate(new Person("b", 1, "M"));
            });

        filterSubject.OnNext(p => p.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SortAfterFilterList()
    {
        var source = new SourceList<Person>();

        var filterSubject = new ReactiveUI.Primitives.Signals.StateSignal<Func<Person, bool>>(p => true);

        var agg = source.Connect().Filter(filterSubject).Transform(x => new ViewModel(x.Name)).Sort(new ViewModel.Comparer()).AsAggregator();

        source.Edit(
            x =>
            {
                x.Add(new Person("A", 1, "F"));
                x.Add(new Person("a", 1, "M"));
                x.Add(new Person("B", 1, "F"));
                x.Add(new Person("b", 1, "M"));
            });

        filterSubject.OnNext(p => p.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _results.Messages[0].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task UpdateFirst()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems[0].Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");
        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);
        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task UpdateLast()
    {
        //TODO: fixed Text

        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems[^1].Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task UpdateMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems.Skip(50).First().Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    public class Data(int id, string value)
    {
        public int Id { get; } = id;

        public string Value { get; } = value;
    }

    public class TestString(string name) : IEquatable<TestString>
    {
        private readonly string _name = name;

        public static implicit operator TestString(string source) => new(source);

        public static implicit operator string(TestString source) => source?._name!;

        public bool Equals(TestString? other) => StringComparer.OrdinalIgnoreCase.Equals(_name, other?._name);

        public override bool Equals(object? obj) => obj is TestString value && Equals(value);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_name);
    }

    public class ViewModel(string name)
    {
        public string Name { get; } = name;

        public class Comparer : IComparer<ViewModel>
        {
            public int Compare(ViewModel? x, ViewModel? y) => StringComparer.OrdinalIgnoreCase.Compare(x?.Name, y?.Name);
        }
    }
}

public class SortFixture : IDisposable
{
    private readonly IComparer<Person> _comparer;

    private readonly RandomPersonGenerator _generator = new();

    private readonly SortedChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public SortFixture()
    {
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

        _source = new SourceCache<Person, string>(p => p.Key);
        _results = new SortedChangeSetAggregator<Person, string>(_source.Connect().Sort(_comparer));
    }

    [Test]
    public async Task AppendAtBeginning()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("_Aaron", 0);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("_Aaron");

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(indexedItem.Value.Index).IsEqualTo(0).Because("Inserted item should have index of zero");
    }

    [Test]
    public async Task AppendAtEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("zzzzz", 1000);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("zzzzz");

        await Assert.That(indexedItem.HasValue).IsTrue();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task AppendInMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var insert = new Person("Marvin", 50);

        _source.AddOrUpdate(insert);

        await Assert.That(_results.Data.Count).IsEqualTo(101).Because("Should be 101 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup("Marvin");

        await Assert.That(indexedItem.HasValue).IsTrue();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task BatchUpdate1()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.Remove(people[0].Key);
                updater.Remove(people[1].Key);
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.Remove(people[7]);
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate2()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "Z", 50, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate3()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[7];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "A", 10, "M"));
                updater.AddOrUpdate(new Person("Mr", "B", 40, "M"));
                updater.AddOrUpdate(new Person("Mr", "C", 70, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);
        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate4()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age - 24));
                updater.AddOrUpdate(new Person("Mr", "A", 10, "M"));
                updater.Remove(people[5]);
                updater.AddOrUpdate(new Person("Mr", "C", 70, "M"));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdate6()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        _source.Edit(
            updater =>
            {
                updater.Clear();
                updater.AddOrUpdate(_generator.Take(10).ToArray());
                updater.Clear();
            });

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
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
        var list = new ObservableCollectionExtended<Person>(testData.OrderBy(p => p, _comparer));

        var toUpdate1 = testData[0];
        var toUpdate2 = testData[3];

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(toUpdate1.Name, 6));
                updater.AddOrUpdate(new Person(toUpdate2.Name, 2));
            });

        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();

        adaptor.Adapt(_results.Messages.Last(), list);
        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task BatchUpdateWhereUpdateMovesTheIndexDown()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = people[3];

        _source.Edit(
            updater =>
            {
                updater.Remove(people[0].Key);
                updater.Remove(people[1].Key);

                updater.AddOrUpdate(new Person(toupdate.Name, toupdate.Age + 50));

                updater.Remove(people[7]);
            });

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));
        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task DoesNotThrow1()
    {
        var cache = new SourceCache<Data, int>(d => d.Id);
        var sortPump = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        var disposable = cache.Connect().Sort(SortExpressionComparer<Data>.Ascending(d => d.Id), sortPump).Subscribe();

        disposable.Dispose();
    }

    [Test]
    public async Task DoesNotThrow2()
    {
        var cache = new SourceCache<Data, int>(d => d.Id);
        var disposable = cache.Connect().Sort(new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Data>>(SortExpressionComparer<Data>.Ascending(d => d.Id))).Subscribe();

        disposable.Dispose();
    }

    [Test]
    public async Task InlineUpdateProducesAReplace()
    {
        var people = _generator.Take(10).ToArray();
        _source.AddOrUpdate(people);
        var toupdate = people[3];

        _source.AddOrUpdate(new Person(toupdate.Name, toupdate.Age + 1));

        var list = new ObservableCollectionExtended<Person>(people.OrderBy(p => p, _comparer));
        var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
        adaptor.Adapt(_results.Messages.Last(), list);

        var shouldbe = _results.Messages.Last().SortedItems.Select(p => p.Value).ToList();
        await Assert.That(list).IsEquivalentTo(shouldbe);
    }

    [Test]
    public async Task RemoveFirst()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems[0];

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");
        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task RemoveFromEnd()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems[^1];

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");

        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task RemoveFromMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        //create age 0 to ensure it is inserted first
        var remove = _results.Messages[0].SortedItems.Skip(50).First();

        _source.Remove(remove.Key);

        await Assert.That(_results.Data.Count).IsEqualTo(99).Because("Should be 99 people in the cache");

        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(remove.Key);
        await Assert.That(indexedItem.HasValue).IsFalse();

        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task SortAfterFilter()
    {
        var source = new SourceCache<Person, string>(p => p.Key);

        var filterSubject = new ReactiveUI.Primitives.Signals.StateSignal<Func<Person, bool>>(p => true);

        var agg = new SortedChangeSetAggregator<ViewModel, TestString>(source.Connect().Filter(filterSubject).Group(x => (TestString)x.Key).Transform(x => new ViewModel(x.Key)).Sort(new ViewModel.Comparer()));

        source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("A", 1, "F"));
                x.AddOrUpdate(new Person("a", 1, "M"));
                x.AddOrUpdate(new Person("B", 1, "F"));
                x.AddOrUpdate(new Person("b", 1, "M"));
            });

        filterSubject.OnNext(p => p.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SortAfterFilterList()
    {
        var source = new SourceList<Person>();

        var filterSubject = new ReactiveUI.Primitives.Signals.StateSignal<Func<Person, bool>>(p => true);

        var agg = source.Connect().Filter(filterSubject).Transform(x => new ViewModel(x.Name)).Sort(new ViewModel.Comparer()).AsAggregator();

        source.Edit(
            x =>
            {
                x.Add(new Person("A", 1, "F"));
                x.Add(new Person("a", 1, "M"));
                x.Add(new Person("B", 1, "F"));
                x.Add(new Person("b", 1, "M"));
            });

        filterSubject.OnNext(p => p.Name.Equals("a", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _results.Messages[0].SortedItems.ToList();

        await Assert.That(actualResult).IsEquivalentTo(expectedResult);
    }

    [Test]
    public async Task UpdateFirst()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems[0].Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");
        //TODO: fixed Text
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);
        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task UpdateLast()
    {
        //TODO: fixed Text

        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems[^1].Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");
        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    [Test]
    public async Task UpdateMiddle()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        var toupdate = _results.Messages[0].SortedItems.Skip(50).First().Value;
        var update = new Person(toupdate.Name, toupdate.Age + 5);

        _source.AddOrUpdate(update);

        await Assert.That(_results.Data.Count).IsEqualTo(100).Because("Should be 100 people in the cache");

        var indexedItem = _results.Messages[1].SortedItems.Indexed().Lookup(update.Key);

        await Assert.That(indexedItem.HasValue).IsTrue();
        await Assert.That(ReferenceEquals(update, indexedItem.Value.Value)).IsTrue();
        var list = _results.Messages[1].SortedItems.ToList();
        var sortedResult = list.OrderBy(p => _comparer).ToList();
        await Assert.That(list).IsEquivalentTo(sortedResult);
    }

    public class Data(int id, string value)
    {
        public int Id { get; } = id;

        public string Value { get; } = value;
    }

    public class TestString(string name) : IEquatable<TestString>
    {
        private readonly string _name = name;

        public static implicit operator TestString(string source) => new(source);

        public static implicit operator string(TestString source) => source?._name!;

        public bool Equals(TestString? other) => StringComparer.OrdinalIgnoreCase.Equals(_name, other?._name);

        public override bool Equals(object? obj) => obj is TestString testString && Equals(testString);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_name);
    }

    public class ViewModel(string name)
    {
        public string Name { get; set; } = name;

        public class Comparer : IComparer<ViewModel>
        {
            public int Compare(ViewModel? x, ViewModel? y) => StringComparer.OrdinalIgnoreCase.Compare(x?.Name, y?.Name);
        }
    }
}
