#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using DynamicData.Experimental;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

#endregion

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class WatcherFixture
    {
        private TestScheduler _scheduler = new TestScheduler();
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<SelfObservingPerson, string> _results;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private IWatcher<Person, string> _watcher;

        private IDisposable _cleanUp;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new TestScheduler();
            _source = new SourceCache<Person, string>(p => p.Key);
            _watcher = _source.Connect().AsWatcher(_scheduler);

            _results = new ChangeSetAggregator<SelfObservingPerson, string>
                (
                _source.Connect()
                    .Transform(p => new SelfObservingPerson(_watcher.Watch(p.Key).Select(w => w.Current)))
                    .DisposeMany()
                );

            _cleanUp = Disposable.Create(() =>
            {
                _results.Dispose();
                _source.Dispose();
                _watcher.Dispose();
            });
        }

        [TearDown]
        public void CleanUp()
        {
            _cleanUp.Dispose();
        }

        [Test]
        public void AddNew()
        {
            var person = new Person("Adult1", 50);
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));


            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
            var result = _results.Data.Items.First();
            Assert.AreEqual(1, result.UpdateCount, "Person should have received 1 update");
            Assert.AreEqual(false, result.Completed, "Person should have received 1 update");
        }


        [Test]
        public void Update()
        {
            var first = new Person("Adult1", 50);
            var second = new Person("Adult1", 51);
            _source.BatchUpdate(updater => updater.AddOrUpdate(first));

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
            _source.BatchUpdate(updater => updater.AddOrUpdate(second));

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");


            //var firstResult = _results.Data.Items.First();
            //Assert.AreEqual(1, firstResult.UpdateCount, "First Person should have received 1 update");
            //Assert.AreEqual(false, firstResult.Completed, "First Person should have received 1 update");

            var secondResult = _results.Messages[1].First();
            Assert.AreEqual(1, secondResult.Previous.Value.UpdateCount, "Second Person should have received 1 update");
            Assert.AreEqual(true, secondResult.Previous.Value.Completed, "Second person  should have received 1 update");
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
            _source.Remove(person.Key);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(11).Ticks);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be 0 item in the cache");

            var secondResult = _results.Messages[1].First();
            Assert.AreEqual(1, secondResult.Current.UpdateCount, "Second Person should have received 1 update");
            Assert.AreEqual(true, secondResult.Current.Completed, "Second person  should have received 1 update");
       
        }

        [Test]
        public void Watch()
        {
            var person = new Person("Adult1", 50);
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));

            var result = new List<Change<Person, string>>(3);
            var watch = _watcher.Watch("Adult1").Subscribe(result.Add);
            
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
            Assert.AreEqual(1, result.Count, "Should be 1 updates");
            Assert.AreEqual(person, result[0].Current, "Should be 1 item in the cache");

            _source.BatchUpdate(updater => updater.Remove(("Adult1")));
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);

            watch.Dispose();
        }

        [Test]
        public void WatchMany()
        {
            _source.AddOrUpdate(new Person("Adult1", 50));

            var result = new List<Change<Person, string>>(3);
            var watch1 = _watcher.Watch("Adult1").Subscribe(result.Add);
            var watch2 = _watcher.Watch("Adult1").Subscribe(result.Add);
            var watch3 = _watcher.Watch("Adult1").Subscribe(result.Add);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

            Assert.AreEqual(3, result.Count, "Should be 3 updates");
            foreach (var update in result)
            {
                Assert.AreEqual(ChangeReason.Add, update.Reason, "Change reason should be add");
            }
            result.Clear();

            _source.AddOrUpdate(new Person("Adult1", 51));
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
            Assert.AreEqual(3, result.Count, "Should be 3 updates");
            foreach (var update in result)
            {
                Assert.AreEqual(ChangeReason.Update, update.Reason, "Change reason should be add");
            }
            result.Clear();

            _source.Remove("Adult1");
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
            Assert.AreEqual(3, result.Count, "Should be 3 updates");
            foreach (var update in result)
            {
                Assert.AreEqual(ChangeReason.Remove, update.Reason, "Change reason should be add");
            }
            result.Clear();

            watch1.Dispose();
            watch2.Dispose();
            watch3.Dispose();
        }
    }
}
