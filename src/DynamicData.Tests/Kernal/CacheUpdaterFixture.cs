#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Kernal;

public class CacheUpdaterFixture
{
    private readonly ChangeAwareCache<Person, string> _cache;

    private readonly CacheUpdater<Person, string> _updater;

    public CacheUpdaterFixture()
    {
        _cache = new ChangeAwareCache<Person, string>();
        _updater = new CacheUpdater<Person, string>(_cache);
    }

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _updater.AddOrUpdate(person, "Adult1");
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
    public async Task Remove()
    {
        const string key = "Adult1";

        var person = new Person(key, 50);
        _updater.AddOrUpdate(person, key);
        _updater.Remove(key);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Count).IsEqualTo(0);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Remove)).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";

        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);
        _updater.AddOrUpdate(newperson, key);
        _updater.AddOrUpdate(updated, key);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        await Assert.That(_cache.Lookup(key).Value).IsEqualTo(updated);
        await Assert.That(_cache.Count).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(updates.Count(update => update.Reason == ChangeReason.Update)).IsEqualTo(1);
        await Assert.That(updates.Count).IsEqualTo(2);
    }
}
