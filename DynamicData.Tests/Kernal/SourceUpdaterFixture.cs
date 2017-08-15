using System;
using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Kernal
{
    
    public class SourceUpdaterFixture
    {
        private readonly ChangeAwareCache<Person, string> _cache;
        private readonly CacheUpdater<Person, string> _updater;

        public  SourceUpdaterFixture()
        {
            _cache = new ChangeAwareCache<Person, string>();
            _updater = new CacheUpdater<Person, string>(_cache, new KeySelector<Person, string>(p => p.Name));
        }

        [Fact]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _updater.AddOrUpdate(person);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Lookup("Adult1").Value.Should().Be(person);
            _cache.Count.Should().Be(1);
            1.Should().Be(updates.Count, "Should be 1 updates");
            updates.First().Should().Be(new Change<Person, string>(ChangeReason.Add, person.Name, person), "Should be 1 updates");
        }

        [Fact]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
            const string key = "Adult1";

            _updater.Remove(key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Count.Should().Be(0);
            updates.Count.Should().Be(0, "Should be 0 updates");
        }

        [Fact]
        public void BatchOfUniqueUpdates()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            var updates = _updater.AsChangeSet();


            _cache.Items.ToArray().ShouldAllBeEquivalentTo(people);
            _cache.Count.Should().Be(100);
            updates.Adds.Should().Be(100);
            updates.Count.Should().Be(100);
        }

        [Fact]
        public void BatchRemoves()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            _updater.Remove(people);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Count.Should().Be(0, "Everything should be removed");
            100.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 100 adds");
            100.Should().Be(updates.Count(update => update.Reason == ChangeReason.Remove), "Should be 100 removes");
            200.Should().Be(updates.Count, "Should be 200 updates");
        }

        [Fact]
        public void BatchSuccessiveUpdates()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name1", i)).ToArray();
            _updater.AddOrUpdate(people);

            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Lookup("Name1").Value.Age.Should().Be(100);
            _cache.Count.Should().Be(1, "Successive updates should replace cache value");
            99.Should().Be(updates.Count(update => update.Reason == ChangeReason.Update), "Should be 99 updates");
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 1 add");
            100.Should().Be(updates.Count, "Should be 100 updates");
        }

        [Fact]
        public void CanRemove()
        {
            const string key = "Adult1";

            var person = new Person(key, 50);
            _updater.AddOrUpdate(person);
            _updater.Remove(person);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Count.Should().Be(0);
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 1 add");
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Remove), "Should be 1 remove");
            2.Should().Be(updates.Count, "Should be 2 updates");
        }

        [Fact]
        public void CanUpdate()
        {
            const string key = "Adult1";

            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);
            _updater.AddOrUpdate(newperson);
            _updater.AddOrUpdate(updated);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Lookup(key).Value.Should().Be(updated);
            _cache.Count.Should().Be(1);
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 1 adds");
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Update), "Should be 1 update");
            2.Should().Be(updates.Count, "Should be 2 updates");
        }

        [Fact]
        public void Clear()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            _updater.Clear();
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Count.Should().Be(0, "Everything should be removed");
            100.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 100 adds");
            100.Should().Be(updates.Count(update => update.Reason == ChangeReason.Remove), "Should be 100 removes");
            200.Should().Be(updates.Count, "Should be 200 updates");
        }

        [Fact]
        public void NullSelectorWillThrow()
        {
            // Assert.Throws<ArgumentNullException>(() => new SourceUpdater<Person, string>(_cache, new KeySelector<Person, string>(null)));
        }

    }
}
