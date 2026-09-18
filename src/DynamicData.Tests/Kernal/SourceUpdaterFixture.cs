#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Kernal;

public class SourceUpdaterFixture
{
    private readonly ChangeAwareCache<Person, string> _cache;

    private readonly CacheUpdater<Person, string> _updater;

    public SourceUpdaterFixture()
    {
        _cache = new ChangeAwareCache<Person, string>();
        _updater = new CacheUpdater<Person, string>(_cache, p => p.Name);
    }

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _updater.AddOrUpdate(person);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Lookup("Adult1").Value).IsEqualTo(person);
        await Assert.That(_cache.Count).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(1);
        await Assert.That(updates.First()).IsEqualTo(new Change<Person, string>(ChangeReason.Add, person.Name, person)).Because("Should be 1 updates");
    }

    [Test]
    public async Task AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        const string key = "Adult1";

        _updater.Remove(key);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Count).IsEqualTo(0);
        await Assert.That(updates.Count).IsEqualTo(0).Because("Should be 0 updates");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        var updates = _cache.CaptureChanges();

        await Assert.That(_cache.Items.ToArray()).IsEquivalentTo(people);
        await Assert.That(_cache.Count).IsEqualTo(100);
        await Assert.That(updates.Adds).IsEqualTo(100);
        await Assert.That(updates.Count).IsEqualTo(100);
    }

    [Test]
    public async Task BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        _updater.Remove(people);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Count).IsEqualTo(0).Because("Everything should be removed");
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(100);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Remove)).IsEqualTo(100);
        await Assert.That(updates.Count).IsEqualTo(200);
    }

    [Test]
    public async Task BatchSuccessiveUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name1", i)).ToArray();
        _updater.AddOrUpdate(people);

        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Lookup("Name1").Value.Age).IsEqualTo(100);
        await Assert.That(_cache.Count).IsEqualTo(1).Because("Successive updates should replace cache value");
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Update)).IsEqualTo(99);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(100);
    }

    [Test]
    public async Task CanRemove()
    {
        const string key = "Adult1";

        var person = new Person(key, 50);
        _updater.AddOrUpdate(person);
        _updater.Remove(person);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Count).IsEqualTo(0);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Remove)).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task CanUpdate()
    {
        const string key = "Adult1";

        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);
        _updater.AddOrUpdate(newperson);
        _updater.AddOrUpdate(updated);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Lookup(key).Value).IsEqualTo(updated);
        await Assert.That(_cache.Count).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Update)).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        _updater.Clear();
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Count).IsEqualTo(0).Because("Everything should be removed");
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(100);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Remove)).IsEqualTo(100);
        await Assert.That(updates.Count).IsEqualTo(200);
    }

    [Test]
    public async Task NullSelectorWillThrow()
    {
        // Assert.Throws<ArgumentNullException>(() => new SourceUpdater<Person, string>(_cache, new KeySelector<Person, string>(null)));
    }
}
