using System.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

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

    [Fact]
    public void Add()
    {
        var person = new Person("Adult1", 50);
        _updater.AddOrUpdate(person);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Lookup("Adult1").Value.Should().Be(person);
        _cache.Count.Should().Be(1);
        updates.Count.Should().Be(1);
        updates.First().Should().Be(new Change<Person, string>(ChangeReason.Add, person.Name, person), "Should be 1 updates");
    }

    [Fact]
    public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        const string key = "Adult1";

        _updater.Remove(key);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Count.Should().Be(0);
        updates.Count.Should().Be(0, "Should be 0 updates");
    }

    [Fact]
    public void BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        var updates = _cache.CaptureChanges();

        _cache.Items.ToArray().Should().BeEquivalentTo(people);
        _cache.Count.Should().Be(100);
        updates.Adds.Should().Be(100);
        updates.Count.Should().Be(100);
    }

    [Fact]
    public void BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        _updater.Remove(people);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Count.Should().Be(0, "Everything should be removed");
        updates.Count(update => update.Reason == ChangeReason.Add).Should().Be(100);
        updates.Count(update => update.Reason == ChangeReason.Remove).Should().Be(100);
        updates.Count.Should().Be(200);
    }

    [Fact]
    public void BatchSuccessiveUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name1", i)).ToArray();
        _updater.AddOrUpdate(people);

        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Lookup("Name1").Value.Age.Should().Be(100);
        _cache.Count.Should().Be(1, "Successive updates should replace cache value");
        updates.Count(update => update.Reason == ChangeReason.Update).Should().Be(99);
        updates.Count(update => update.Reason == ChangeReason.Add).Should().Be(1);
        updates.Count.Should().Be(100);
    }

    [Fact]
    public void CanRemove()
    {
        const string key = "Adult1";

        var person = new Person(key, 50);
        _updater.AddOrUpdate(person);
        _updater.Remove(person);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Count.Should().Be(0);
        updates.Count(update => update.Reason == ChangeReason.Add).Should().Be(1);
        updates.Count(update => update.Reason == ChangeReason.Remove).Should().Be(1);
        updates.Count.Should().Be(2);
    }

    [Fact]
    public void CanUpdate()
    {
        const string key = "Adult1";

        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);
        _updater.AddOrUpdate(newperson);
        _updater.AddOrUpdate(updated);
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Lookup(key).Value.Should().Be(updated);
        _cache.Count.Should().Be(1);
        updates.Count(update => update.Reason == ChangeReason.Add).Should().Be(1);
        updates.Count(update => update.Reason == ChangeReason.Update).Should().Be(1);
        updates.Count.Should().Be(2);
    }

    [Fact]
    public void Clear()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _updater.AddOrUpdate(people);
        _updater.Clear();
        IChangeSet<Person, string> updates = _cache.CaptureChanges();

        _cache.Count.Should().Be(0, "Everything should be removed");
        updates.Count(update => update.Reason == ChangeReason.Add).Should().Be(100);
        updates.Count(update => update.Reason == ChangeReason.Remove).Should().Be(100);
        updates.Count.Should().Be(200);
    }

    [Fact]
    public void NullSelectorWillThrow()
    {
        // Assert.Throws<ArgumentNullException>(() => new SourceUpdater<Person, string>(_cache, new KeySelector<Person, string>(null)));
    }
}
