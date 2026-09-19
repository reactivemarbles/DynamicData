using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class GroupFixture : IDisposable
{
    private readonly ISourceCache<Person, string> _source;

    private ReadOnlyObservableCollection<GroupViewModel>? _entries;

    public GroupFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Test]
    public async Task Kaboom()
    {
        SourceCache<Mytype, int> cache = new(x => x.Key);
        List<Mytype> listWithDuplicates =
        [
            new(1, "G1"),
            new(1, "G2"),
        ];
        cache
            .Connect()
            .Group(x => x.Grouping)
            .Subscribe();

        cache.Edit(x =>
        {
            x.AddOrUpdate(listWithDuplicates);
            x.Clear();
            x.AddOrUpdate(listWithDuplicates);
        });
    }

    class Mytype(int key, string grouping)
    {
        public int Key { get; set; } = key;
        public string Grouping { get; set; } = grouping;

        public override string ToString() => $"{Key}, {Grouping}";
    }

    [Test]
    public async Task Add()
    {
        var called = false;
        var observedCounts = new List<int>();
        var observedReasons = new List<ChangeReason>();
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                observedCounts.Add(updates.Count);
                observedReasons.Add(updates.First().Reason);
                called = true;
            });
        _source.AddOrUpdate(new Person("Person1", 20));

        subscriber.Dispose();
        await Assert.That(called).IsTrue();
        await Assert.That(observedCounts).IsEquivalentTo(new[] { 1 });
        await Assert.That(observedReasons).IsEquivalentTo(new[] { ChangeReason.Add });
    }

    [Test]
    public async Task AddItemAfterUpdateItemProcessAdd()
    {
        var subscriber = _source.Connect().Group(x => x.Name[0].ToString()).Transform(x => new GroupViewModel(x)).Bind(out _entries).Subscribe();

        _source.Edit(x => x.AddOrUpdate(new Person("Adam", 1)));

        var firstGroup = _entries.First();
        await Assert.That(firstGroup.Entries.Count).IsEqualTo(1);

        _source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("Adam", 3)); // update
                x.AddOrUpdate(new Person("Alfred", 1)); // add
            });

        await Assert.That(firstGroup.Entries.Count).IsEqualTo(2);

        subscriber.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task FiresCompletedWhenDisposed()
    {
        var completed = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => { }, () => completed = true);
        _source.Dispose();
        subscriber.Dispose();
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task FiresManyValueForBatchOfDifferentAdds()
    {
        var called = false;
        var observedCounts = new List<int>();
        var observedReasons = new List<ChangeReason>();
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                observedCounts.Add(updates.Count);
                observedReasons.AddRange(updates.Select(update => update.Reason));
                called = true;
            });
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

        subscriber.Dispose();
        await Assert.That(called).IsTrue();
        await Assert.That(observedCounts).IsEquivalentTo(new[] { 4 });
        await Assert.That(observedReasons).IsEquivalentTo(Enumerable.Repeat(ChangeReason.Add, 4));
    }

    [Test]
    public async Task FiresOnlyOnceForABatchOfUniqueValues()
    {
        var called = false;
        var observedCounts = new List<int>();
        var observedReasons = new List<ChangeReason>();
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(
            updates =>
            {
                observedCounts.Add(updates.Count);
                observedReasons.Add(updates.First().Reason);
                called = true;
            });
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

        subscriber.Dispose();
        await Assert.That(called).IsTrue();
        await Assert.That(observedCounts).IsEquivalentTo(new[] { 1 });
        await Assert.That(observedReasons).IsEquivalentTo(new[] { ChangeReason.Add });
    }

    [Test]
    public async Task FiresRemoveWhenEmptied()
    {
        var called = false;
        var observedCounts = new List<int>();
        var observedReasons = new List<ChangeReason>();
        //skip first one a this is setting up the stream
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(
            updates =>
            {
                observedCounts.Add(updates.Count);
                observedReasons.AddRange(updates.Select(update => update.Reason));
                called = true;
            });
        var person = new Person("Person1", 20);

        _source.AddOrUpdate(person);

        //remove
        _source.Remove(person);

        subscriber.Dispose();
        await Assert.That(called).IsTrue();
        await Assert.That(observedCounts).IsEquivalentTo(new[] { 1 });
        await Assert.That(observedReasons).IsEquivalentTo(new[] { ChangeReason.Remove });
    }

    [Test]
    public async Task ReceivesUpdateWhenFeederIsInvoked()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        subscriber.Dispose();
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task Remove()
    {
        var called = false;
        var observedCounts = new List<int>();
        var observedReasons = new List<ChangeReason>();
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(
            updates =>
            {
                observedCounts.Add(updates.Count);
                observedReasons.Add(updates.First().Reason);
                called = true;
            });
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.Remove(new Person("Person1", 20));
        subscriber.Dispose();
        await Assert.That(called).IsTrue();
        await Assert.That(observedCounts).IsEquivalentTo(new[] { 1 });
        await Assert.That(observedReasons).IsEquivalentTo(new[] { ChangeReason.Remove });
    }

    [Test]
    public async Task UpdateAnItemWillChangedThegroup()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person1", 21));
        subscriber.Dispose();
        await Assert.That(called).IsTrue();
    }

    [Test]
    public async Task UpdateItemAfterAddItemProcessAdd()
    {
        var subscriber = _source.Connect().Group(x => x.Name[0].ToString()).Transform(x => new GroupViewModel(x)).Bind(out _entries).Subscribe();

        _source.Edit(x => x.AddOrUpdate(new Person("Adam", 1)));

        var firstGroup = _entries.First();
        await Assert.That(firstGroup.Entries.Count).IsEqualTo(1);

        _source.Edit(
            x =>
            {
                x.AddOrUpdate(new Person("Alfred", 1)); // add
                x.AddOrUpdate(new Person("Adam", 3)); // update
            });

        await Assert.That(firstGroup.Entries.Count).IsEqualTo(2);

        subscriber.Dispose();
    }

    [Test]
    public async Task UpdateNotPossible()
    {
        var called = false;
        var subscriber = _source.Connect().Group(p => p.Age).Skip(1).Subscribe(updates => called = true);
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person1", 20));
        subscriber.Dispose();
        await Assert.That(called).IsFalse();
    }

    public class GroupEntryViewModel(Person person)
    {
        public Person Person { get; } = person;
    }

    public class GroupViewModel
    {
        private readonly ReadOnlyObservableCollection<GroupEntryViewModel> _entries;

        public GroupViewModel(IGroup<Person, string, string> person) => person?.Cache.Connect().Transform(x => new GroupEntryViewModel(x)).Bind(out _entries).Subscribe();

        public ReadOnlyObservableCollection<GroupEntryViewModel> Entries => _entries;
    }
}
