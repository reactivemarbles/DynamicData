using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    internal class SourceUpdaterFixture
    {
        private ChangeAwareCache<Person, string> _cache;
        private CacheUpdater<Person, string> _updater;

        [SetUp]
        public void Initialise()
        {
            _cache = new ChangeAwareCache<Person, string>();
            _updater = new CacheUpdater<Person, string>(_cache, new KeySelector<Person, string>(p => p.Name));
        }


        [Test]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _updater.AddOrUpdate(person);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(person, _cache.Lookup("Adult1").Value);
            Assert.AreEqual(1, _cache.Count);
            Assert.AreEqual(updates.Count, 1, "Should be 1 updates");
            Assert.AreEqual(new Change<Person, string>(ChangeReason.Add, person.Name, person), updates.First(),
                            "Should be 1 updates");
        }

        [Test]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
            const string key = "Adult1";

            _updater.Remove(key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count);
            Assert.AreEqual(0, updates.Count, "Should be 0 updates");
        }

        [Test]
        public void BatchOfUniqueUpdates()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            CollectionAssert.AreEqual(people, _cache.Items);
            Assert.AreEqual(100, _cache.Count, "Should be 100 items in the cache");
            CollectionAssert.AreEquivalent(people.Select(p => new Change<Person, string>(ChangeReason.Add, p.Name, p)),
                                           updates);
            Assert.AreEqual(updates.Count, 100, "Should be 100 updates");
        }

        [Test]
        public void BatchRemoves()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            _updater.Remove(people);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count, "Everything should be removed");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 100, "Should be 100 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 100, "Should be 100 removes");
            Assert.AreEqual(updates.Count, 200, "Should be 200 updates");
        }

        [Test]
        public void BatchSuccessiveUpdates()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name1", i)).ToArray();
            _updater.AddOrUpdate(people);

            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(new Person("Name1", 100), _cache.Lookup("Name1").Value);
            Assert.AreEqual(1, _cache.Count, "Sucessive updates should replace cache value");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Update), 99, "Should be 99 updates");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 add");
            Assert.AreEqual(updates.Count, 100, "Should be 100 updates");
        }

        [Test]
        public void CanRemove()
        {
            const string key = "Adult1";

            var person = new Person(key, 50);
            _updater.AddOrUpdate(person);
            _updater.Remove(person);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count);
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 add");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 1, "Should be 1 remove");
            Assert.AreEqual(updates.Count, 2, "Should be 2 updates");
        }

        [Test]
        public void CanUpdate()
        {
            const string key = "Adult1";

            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);
            _updater.AddOrUpdate(newperson);
            _updater.AddOrUpdate(updated);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(updated, _cache.Lookup(key).Value);
            Assert.AreEqual(1, _cache.Count);
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Update), 1, "Should be 1 update");
            Assert.AreEqual(updates.Count, 2, "Should be 2 updates");
        }

        [Test]
        public void Clear()
        {
            Person[] people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people);
            _updater.Clear();
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count, "Everything should be removed");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 100, "Should be 100 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 100, "Should be 100 removes");
            Assert.AreEqual(updates.Count, 200, "Should be 200 updates");
        }

        [Test]
        public void NullSelectorWillThrow()
        {
            // Assert.Throws<ArgumentNullException>(() => new SourceUpdater<Person, string>(_cache, new KeySelector<Person, string>(null)));
        }
    }
}
