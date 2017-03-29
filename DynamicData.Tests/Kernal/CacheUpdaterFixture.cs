using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    internal class CacheUpdaterFixture
    {
        private ChangeAwareCache<Person, string> _cache;
        private CacheUpdater<Person, string> _updater;

        [SetUp]
        public void Initialise()
        {
            _cache = new ChangeAwareCache<Person, string>();
            _updater = new CacheUpdater<Person, string>(_cache);
        }


        [Test]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _updater.AddOrUpdate(person, "Adult1");
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
        public void Remove()
        {
            const string key = "Adult1";

            var person = new Person(key, 50);
            _updater.AddOrUpdate(person, key);
            _updater.Remove(key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count);
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 add");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 1, "Should be 1 remove");
            Assert.AreEqual(updates.Count, 2, "Should be 2 updates");
        }

        [Test]
        public void Update()
        {
            const string key = "Adult1";

            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);
            _updater.AddOrUpdate(newperson, key);
            _updater.AddOrUpdate(updated, key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(updated, _cache.Lookup(key).Value);
            Assert.AreEqual(1, _cache.Count);
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Update), 1, "Should be 1 update");
            Assert.AreEqual(updates.Count, 2, "Should be 2 updates");
        }
    }
}
