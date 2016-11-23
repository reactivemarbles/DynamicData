using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class GroupFixture
    {
        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
        }

        private ISourceCache<Person, string> _source;

        [Test]
        public void Add()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(
                                                updates =>
                                                {
                                                    Assert.AreEqual(1, updates.Count, "Should be 1 add");
                                                    Assert.AreEqual(ChangeReason.Add, updates.First().Reason);
                                                    called = true;
                                                });
            _source.AddOrUpdate(new Person("Person1", 20));

            subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void UpdateNotPossible()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age).Skip(1)
                                            .Subscribe(updates => { called = true; });
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person1", 20));
            subscriber.Dispose();
            Assert.IsFalse(called, "No update has been invoked");
        }

        [Test]
        public void UpdateAnItemWillChangedThegroup()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(updates => { called = true; });
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person1", 21));
            subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void Remove()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Skip(1)
                                            .Subscribe(
                                                updates =>
                                                {
                                                    Assert.AreEqual(1, updates.Count, "Should be 1 add");
                                                    Assert.AreEqual(ChangeReason.Remove, updates.First().Reason);
                                                    called = true;
                                                });
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.Remove(new Person("Person1", 20));
            subscriber.Dispose();
            Assert.IsTrue(called, "Notification should have fired");
        }

        [Test]
        public void FiresCompletedWhenDisposed()
        {
            bool completed = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(updates => { },
                                                       () => { completed = true; });
            _source.Dispose();
            subscriber.Dispose();
            Assert.IsTrue(completed, "Completed has not been invoked");
        }

        [Test]
        public void FiresManyValueForBatchOfDifferentAdds()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(
                                                updates =>
                                                {
                                                    Assert.AreEqual(4, updates.Count, "Should be 4 adds");
                                                    foreach (var update in updates)
                                                    {
                                                        Assert.AreEqual(ChangeReason.Add, update.Reason);
                                                    }
                                                    called = true;
                                                });
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

            subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void FiresOnlyOnceForABatchOfUniqueValues()
        {
            bool called = false;
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(
                                                updates =>
                                                {
                                                    Assert.AreEqual(1, updates.Count, "Should be 1 add");
                                                    Assert.AreEqual(ChangeReason.Add, updates.First().Reason);
                                                    called = true;
                                                });
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

            subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void FiresRemoveWhenEmptied()
        {
            bool called = false;
            //skip first one a this is setting up the stream
            IDisposable subscriber = _source.Connect().Group(p => p.Age).Skip(1)
                                            .Subscribe(
                                                updates =>
                                                {
                                                    Assert.AreEqual(1, updates.Count, "Should be 1 update");
                                                    foreach (var update in updates)
                                                    {
                                                        Assert.AreEqual(ChangeReason.Remove, update.Reason);
                                                    }
                                                    called = true;
                                                });
            var person = new Person("Person1", 20);

            _source.AddOrUpdate(person);

            //remove
            _source.Remove(person);

            subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void ReceivesUpdateWhenFeederIsInvoked()
        {
            bool called = false;
            var subscriber = _source.Connect().Group(p => p.Age)
                                    .Subscribe(updates => { called = true; });
            _source.AddOrUpdate(new Person("Person1", 20));
            subscriber.Dispose();
            Assert.IsTrue(called, "Subscription has not been invoked");
        }
    }
}
