using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class SourceCacheFixture
    {
        private TestChangeSetResult<Person, string> _results;
        private ISourceCache<Person, string> _source;


        [SetUp]
        public void MyTestInitialize()
        {
            _source = new SourceCache<Person, string>(p=>p.Key); 
             _results= new TestChangeSetResult<Person, string>(_source.Connect());
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }


        [Test]
        public void CanHandleABatchOfUpdates()
        {
            _source.BatchUpdate(updater =>
                {
                    var torequery = new Person("Adult1", 44);

                    updater.AddOrUpdate(new Person("Adult1", 40));
                    updater.AddOrUpdate(new Person("Adult1", 41));
                    updater.AddOrUpdate(new Person("Adult1", 42));
                    updater.AddOrUpdate(new Person("Adult1", 43));
                    updater.Evaluate(torequery);
                    updater.Remove(torequery);
                    updater.Evaluate(torequery);
                });

            Assert.AreEqual(6, _results.Summary.Overall.Count, "Should be  6 up`dates");
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 message");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 update");
            Assert.AreEqual(3, _results.Messages[0].Updates, "Should be 3 updates");
            Assert.AreEqual(1, _results.Messages[0].Removes, "Should be  1 remove");
            Assert.AreEqual(1, _results.Messages[0].Evaluates, "Should be 1 evaluate");

            Assert.AreEqual(0, _results.Data.Count, "Should be 1 item in` the cache");
         //   var filtered = people.Where(p => p.Age > 20).ToArray();
          //  CollectionAssert.AreEqual(filtered, _results.Data.Items.Where(p => p.Age > 20), "Incorrect Filter result");
        }


        [Test]
        public void ScheduledUpdatesArriveInOrder()
        {
            //var queue = new AsyncQueue>?();

            var largebatch = Enumerable.Range(1, 10000).Select(i => new Person("Large.{0}".FormatWith(i), i)).ToArray();
            var five = Enumerable.Range(1, 5).Select(i => new Person("Five.{0}".FormatWith(i), i)).ToArray();
            var single1 = new Person("Name.A", 20);
            
            var results = new List<int>();

            var subscription = _source.Connect()
                    .TimeInterval()
                     .Subscribe(updates => results.Add(updates.Value.Count));


            _source.BatchUpdate(updater => updater.AddOrUpdate(largebatch));
            _source.BatchUpdate(updater => updater.AddOrUpdate(five));
            _source.BatchUpdate(updater => updater.AddOrUpdate(single1));

            Thread.Sleep(100);
            subscription.Dispose();
            _source.Dispose();

            Assert.AreEqual(10000, results[0], "largebatch should be first");
            Assert.AreEqual(5, results[1], "Five should be second");
            Assert.AreEqual(1, results[2], "single1 should be third");
        }


        [Test]
        public void SubscribesDisposesCorrectly()
        {
            bool called = false;
            bool errored = false;
            bool completed = false;
            IDisposable subscription = _source.Connect()
                .FinallySafe(() => completed = true)
                .Subscribe(updates => { called = true; }, ex => errored = true,() => completed = true);
            _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Adult1", 40)));

            //_stream.
              subscription.Dispose();
            _source.Dispose();

            Assert.IsTrue(called, "Subscription has not been invoked");
            Assert.IsTrue(completed, "Completed has not been invoked");
        }
    }
}