#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class ToSortedCollectionFixture : IDisposable
{
    private readonly SourceCache<Person, int> _cache;

    private readonly CompositeDisposable _cleanup = new();

    private readonly List<Person> _sortedCollection = new();

    private readonly List<Person> _unsortedCollection = new();

    public ToSortedCollectionFixture()
    {
        _cache = new SourceCache<Person, int>(p => p.Age);
        _cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
    }

    public void Dispose()
    {
        _cache.Dispose();
        _cleanup.Dispose();
    }

    [Test]
    public async Task SortAscending()
    {
        TestScheduler testScheduler = new();

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).ToCollection().Do(
                persons =>
                {
                    _unsortedCollection.Clear();
                    _unsortedCollection.AddRange(persons);
                }).Subscribe());

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).ToSortedCollection(p => p.Age).Do(
                persons =>
                {
                    _sortedCollection.Clear();
                    _sortedCollection.AddRange(persons);
                }).Subscribe());

        // Insert an item with a lower sort order
        _cache.AddOrUpdate(new Person("Name", 0));

        testScheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        await Assert.That(_cache.Items).IsEquivalentTo(_unsortedCollection);
        await Assert.That(_cache.Items).IsNotEqualTo(_sortedCollection);
        await Assert.That(_cache.Items.OrderBy(p => p.Age)).IsEquivalentTo(_sortedCollection);
    }

    [Test]
    public async Task SortDescending()
    {
        TestScheduler testScheduler = new();

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).ToCollection().Do(
                persons =>
                {
                    _unsortedCollection.Clear();
                    _unsortedCollection.AddRange(persons);
                }).Subscribe());

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).ToSortedCollection(p => p.Age, SortDirection.Descending).Do(
                persons =>
                {
                    _sortedCollection.Clear();
                    _sortedCollection.AddRange(persons);
                }).Subscribe());

        // Insert an item with a lower sort order
        _cache.AddOrUpdate(new Person("Name", 0));

        testScheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        await Assert.That(_cache.Items).IsEquivalentTo(_unsortedCollection);
        await Assert.That(_cache.Items).IsNotEqualTo(_sortedCollection);
        await Assert.That(_cache.Items.OrderByDescending(p => p.Age)).IsEquivalentTo(_sortedCollection);
    }
}
