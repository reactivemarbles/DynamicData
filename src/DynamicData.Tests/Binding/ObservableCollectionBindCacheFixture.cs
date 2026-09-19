#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class ObservableCollectionBindCacheFixture : IDisposable
{
    private readonly IDisposable _binder;

    private readonly ObservableCollectionExtended<Person> _collection = new();

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISourceCache<Person, string> _source;

    public ObservableCollectionBindCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _binder = _source.Connect().Bind(_collection).Subscribe();
    }

    [Test]
    public async Task ResetThresholdsForBinding_ObservableCollection()
    {
        var people = _generator.Take(100).ToArray();

        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());

        await Assert.That(test1.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test2.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test3.action).IsEqualTo(NotifyCollectionChangedAction.Add);
        await Assert.That(test4.action).IsEqualTo(NotifyCollectionChangedAction.Add);

        return;

        (NotifyCollectionChangedAction action, ObservableCollectionExtended<Person> list) Test(BindingOptions? options = null)
        {
            _source.Clear();

            NotifyCollectionChangedAction? result = null;

            var list = new ObservableCollectionExtended<Person>();
            using var listEvents = list.ObserveCollectionChanges().Take(1)
                .Select(e => e.EventArgs.Action)
                .Subscribe(events =>
                {
                    result = events;
                });

            var binder = options == null
                ? _source.Connect().Bind(list).Subscribe()
                : _source.Connect().Bind(list, options.Value).Subscribe();

            _source.AddOrUpdate(people);
            binder.Dispose();

            return (result!.Value, list);
        }
    }

    [Test]
    public async Task ResetThresholdsForBinding_ReadonlyObservableCollection()
    {
        var people = _generator.Take(100).ToArray();

        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());

        await Assert.That(test1.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test2.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test3.action).IsEqualTo(NotifyCollectionChangedAction.Add);
        await Assert.That(test4.action).IsEqualTo(NotifyCollectionChangedAction.Add);

        return;

        (NotifyCollectionChangedAction action, ReadOnlyObservableCollection<Person> list) Test(BindingOptions? options = null)
        {
            _source.Clear();

            NotifyCollectionChangedAction? result = null;
            ReadOnlyObservableCollection<Person> list;
            //var list = new ObservableCollectionExtended<Person>();

            var binder = options == null
                ? _source.Connect().Bind(out list).Subscribe()
                : _source.Connect().Bind(out list, options.Value).Subscribe();

            using var listEvents = list.ObserveCollectionChanges().Take(1)
                .Select(e => e.EventArgs.Action)
                .Subscribe(events =>
                {
                    result = events;
                });

            _source.AddOrUpdate(people);
            binder.Dispose();
            return (result!.Value, list);
        }
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BatchAdd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
    }

    [Test]
    public async Task BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task UpdateToSourceSendsReplaceOnDestination()
    {
        await RunTest(true);
        await RunTest(false);

        async Task RunTest(bool useReplace)
        {
            var collection = new ObservableCollectionExtended<Person>();

            using var source = new SourceCache<Person, string>(p => p.Name);
            using var binder = source.Connect().Bind(collection, new ObservableCollectionAdaptor<Person, string>(useReplaceForUpdates: useReplace)).Subscribe();

            NotifyCollectionChangedAction action = default;
            source.AddOrUpdate(new Person("Adult1", 50));

            using (collection.ObserveCollectionChanges().Select(x => x.EventArgs.Action).Subscribe(updateType => action = updateType))
            {
                source.AddOrUpdate(new Person("Adult1", 51));
            }

            if (useReplace)
            {
                await Assert.That(action).IsEqualTo(NotifyCollectionChangedAction.Replace);
            }
            else
            {
                await Assert.That(action).IsEqualTo(NotifyCollectionChangedAction.Add);
            }
        }
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
