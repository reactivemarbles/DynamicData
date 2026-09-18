#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class IObservableListBindListFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly IObservableList<Person> _list;

    private readonly ChangeSetAggregator<Person> _observableListNotifications;

    private readonly SourceList<Person> _source;

    private readonly ChangeSetAggregator<Person> _sourceListNotifications;

    public IObservableListBindListFixture()
    {
        _source = new SourceList<Person>();
        _sourceListNotifications = _source.Connect().AutoRefresh().BindToObservableList(out _list).AsAggregator();

        _observableListNotifications = _list.Connect().AsAggregator();
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

            _source.AddRange(people);
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

            _source.AddRange(people);
            binder.Dispose();
            return (result!.Value, list);
        }
    }

    [Test]
    public async Task AddRange()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        await Assert.That(_list.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_list.Items).IsEquivalentTo(people).Because("Collections should be equivalent");
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_list.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_list.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task Clear()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);
        _source.Clear();
        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _sourceListNotifications.Dispose();
        _observableListNotifications.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task ListRecievesRefresh()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        person.Age = 60;

        await Assert.That(_observableListNotifications.Messages.Count).IsEqualTo(2);
        await Assert.That(_observableListNotifications.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.Add(person);
        _source.Replace(person, personUpdated);

        await Assert.That(_list.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_list.Items[0]).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
