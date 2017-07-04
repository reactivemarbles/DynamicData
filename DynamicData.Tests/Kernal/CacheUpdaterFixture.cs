
using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Kernal
{
    
    public class CacheUpdaterFixture
    {
        private readonly ChangeAwareCache<Person, string> _cache;
        private readonly CacheUpdater<Person, string> _updater;

        public  CacheUpdaterFixture()
        {
            _cache = new ChangeAwareCache<Person, string>();
            _updater = new CacheUpdater<Person, string>(_cache);
        }



        [Fact]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _updater.AddOrUpdate(person, "Adult1");
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
        public void Remove()
        {
            const string key = "Adult1";

            var person = new Person(key, 50);
            _updater.AddOrUpdate(person, key);
            _updater.Remove(key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Count.Should().Be(0);
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 1 add");
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Remove), "Should be 1 remove");
            2.Should().Be(updates.Count, "Should be 2 updates");
        }

        [Fact]
        public void Update()
        {
            const string key = "Adult1";

            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);
            _updater.AddOrUpdate(newperson, key);
            _updater.AddOrUpdate(updated, key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            _cache.Lookup(key).Value.Should().Be(updated);
            _cache.Count.Should().Be(1);
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Add), "Should be 1 adds");
            1.Should().Be(updates.Count(update => update.Reason == ChangeReason.Update), "Should be 1 update");
            2.Should().Be(updates.Count, "Should be 2 updates");
        }

    }
}
