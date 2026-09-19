#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Test Fixture for the FilterOnObservable extension method.
/// </summary>
public class FilterOnObservableFixture : IDisposable
{
    private const int MagicNumber = 37;

    private readonly ChangeSetAggregator<Person, string> _sourceResults;

    private readonly ISourceCache<Person, string> _source;

    public FilterOnObservableFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _sourceResults = _source.Connect().AsAggregator();
    }

    [Test]
    public async Task FactoryIsInvoked()
    {
        // having
        var invoked = false;
        var val = -1;
        IObservable<bool> factory(Person p)
        {
            invoked = true;
            val = p.Age;
            return Observable.Return(true);
        }
        using var sub = _source.Connect().FilterOnObservable(factory).Subscribe();

        // when
        AddPerson(MagicNumber);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(1);
        await Assert.That(invoked).IsTrue();
        await Assert.That(val).IsEqualTo(MagicNumber).Because("Was value added to cache");
        await Assert.That(() => _source.Connect().FilterOnObservable((Func<Person, IObservable<bool>>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task FactoryWithKeyIsInvoked()
    {
        // having
        var invoked = false;
        var val = -1;
        IObservable<bool> factory(Person p, string name)
        {
            invoked = true;
            val = p.Age;
            return Observable.Return(true);
        }
        using var sub = _source.Connect().FilterOnObservable(factory).Subscribe();

        // when
        AddPerson(MagicNumber);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(1);
        await Assert.That(invoked).IsTrue();
        await Assert.That(val).IsEqualTo(MagicNumber).Because("Was value added to cache");
        await Assert.That(() => _source.Connect().FilterOnObservable((Func<Person, string, IObservable<bool>>)null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ObservableCacheEx.FilterOnObservable(null!, (Func<Person, string, IObservable<bool>>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task FilteredOutIfNoObservableValue()
    {
        // having
        using var filterStats = _source.Connect().FilterOnObservable(p => Observable.Never<bool>()).AsAggregator();

        // when
        AddPeople(MagicNumber);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(_sourceResults.Messages[0].Adds).IsEqualTo(MagicNumber);
        await Assert.That(_sourceResults.Messages.Count).IsEqualTo(1).Because("Should have all been added at once");
        await Assert.That(filterStats.Messages.Count).IsEqualTo(0).Because("All items should be filtered out");
    }

    [Test]
    public async Task ObservableFilterUsedToDetermineInclusion()
    {
        // having
        Predicate<Person> predicate = p => p.Age % 2 == 0;
        Func<Person, IObservable<bool>> filterFactory = p => Observable.Return(predicate(p));
        var passCount = 0;
        var failCount = 0;
        using var filterStats = _source.Connect().FilterOnObservable(filterFactory).AsAggregator();

        // when
        AddPeople(MagicNumber).ForEach(p => _ = predicate(p) ? passCount++ : failCount++);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(passCount + failCount);
        await Assert.That(filterStats.Data.Count).IsEqualTo(passCount);
    }

    [Test]
    public async Task ObservableFilterTriggersAddAndRemove()
    {
        // having
        ReactiveUI.Primitives.Signals.ISignal<bool> filterSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(true);
        filterSubject.OnNext(false);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(_sourceResults.Messages.Count).IsEqualTo(1).Because("Should have all been added at once");
        await Assert.That(filterStats.Data.Count).IsEqualTo(0);
        await Assert.That(filterStats.Messages.Count).IsEqualTo(MagicNumber * 2).Because("Each should be added and removed");
        await Assert.That(filterStats.Summary.Overall.Adds).IsEqualTo(MagicNumber);
        await Assert.That(filterStats.Summary.Overall.Removes).IsEqualTo(MagicNumber);
    }

    [Test]
    public async Task ObservableFilterDuplicateValuesHaveNoEffect()
    {
        // having
        ReactiveUI.Primitives.Signals.ISignal<bool> filterSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(false);
        filterSubject.OnNext(false);
        filterSubject.OnNext(false);
        filterSubject.OnNext(true);
        filterSubject.OnNext(true);
        filterSubject.OnNext(true);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(_sourceResults.Messages.Count).IsEqualTo(1).Because("Should have all been added at once");
        await Assert.That(filterStats.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(filterStats.Messages.Count).IsEqualTo(MagicNumber).Because("Each should be added individually");
        await Assert.That(filterStats.Summary.Overall.Adds).IsEqualTo(MagicNumber);
    }

    [Test]
    public async Task ObservableFilterChangesCanBeBuffered()
    {
        // having
        TestScheduler? scheduler = new TestScheduler();
        ReactiveUI.Primitives.Signals.ISignal<bool> filterSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject, TimeSpan.FromSeconds(1), scheduler).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(true);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // then
        await Assert.That(_sourceResults.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(_sourceResults.Messages.Count).IsEqualTo(1).Because("Should have all been added at once");
        await Assert.That(filterStats.Data.Count).IsEqualTo(MagicNumber);
        await Assert.That(filterStats.Messages.Count).IsEqualTo(1).Because("Should have all been added at once");
        await Assert.That(filterStats.Summary.Overall.Adds).IsEqualTo(MagicNumber);
    }

    private static Person NewPerson(int n) => new("Name" + n, n);

    private IEnumerable<Person> AddPeople(int count)
    {
        var people = Enumerable.Range(0, count).Select(NewPerson).ToArray();
        _source.AddOrUpdate(people);
        return people;
    }

    private Person AddPerson(int n)
    {
        var p = NewPerson(n);
        _source.AddOrUpdate(p);
        return p;
    }

    public void Dispose()
    {
        _source.Dispose();
        _sourceResults.Dispose();
    }
}
