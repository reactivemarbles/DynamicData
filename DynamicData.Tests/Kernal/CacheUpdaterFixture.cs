using System;
using System.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    internal class CacheUpdaterFixture
    {
        [SetUp]
        public void Initialise()
        {
            _cache = new Cache<Person, String>();
            _updater = new IntermediateUpdater<Person, string>(_cache);
        }

        private Cache<Person, string> _cache;
        private IntermediateUpdater<Person, string> _updater;

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
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people,p=>p.Key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            CollectionAssert.AreEqual(people, _cache.Items);
            Assert.AreEqual(100, _cache.Count, "Should be 100 items in the cache");
            CollectionAssert.AreEquivalent(people.Select(p => new Change<Person, string>(ChangeReason.Add, p.Key, p)), updates);
            Assert.AreEqual(updates.Count, 100, "Should be 100 updates");
        }


        [Test]
        public void BatchRemoves()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people,p=>p.Key);
            _updater.Remove(people,p=>p.Key);
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count, "Everything should be removed");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 100, "Should be 100 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 100, "Should be 100 removes");
            Assert.AreEqual(updates.Count, 200, "Should be 200 updates");
        }

        //[Test]
        //public void BatchSuccessiveUpdates()
        //{
        //    var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
           
            
        //    _updater.AddOrUpdate(people, p => p.Key);

        //    IChangeSet<Person, string> updates = _updater.AsChangeSet();

        //    Assert.AreEqual(new Person("Name1", 1), _cache.Lookup("Name1").Value);
        //    Assert.AreEqual(100, _cache.Count, "Sucessive updates should replace cache value");
        //    Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Change), 99, "Should be 99 updates");
        //    Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 1, "Should be 1 add");
        //    Assert.AreEqual(updates.Count, 100, "Should be 100 updates");
        //}

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            _updater.AddOrUpdate(people, p => p.Key);
            _updater.Clear();
            IChangeSet<Person, string> updates = _updater.AsChangeSet();

            Assert.AreEqual(0, _cache.Count, "Everything should be removed");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Add), 100, "Should be 100 adds");
            Assert.AreEqual(updates.Count(update => update.Reason == ChangeReason.Remove), 100, "Should be 100 removes");
            Assert.AreEqual(updates.Count, 200, "Should be 200 updates");
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