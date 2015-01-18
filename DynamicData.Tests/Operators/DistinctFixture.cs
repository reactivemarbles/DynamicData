using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    //TODO: Sort this mess out and make better tests


    [TestFixture]
    public class DistinctFixture
    {
        private ISourceCache<Person, string> _source;
        private IObservable<IChangeSet<Person, string>> _stream;
        private IDisposable _subscriber;


        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p=>p.Name);
            _stream = _source.Connect();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _subscriber.Dispose();
        }

        [Test]
        public void FiresAddWhenaNewItemIsAdded()
        {
            bool called = false;
            _subscriber = _stream.DistinctValues(p => p.Age)

                                            .Subscribe(
                                                updates =>
                                                    {
                                                        called = true;
                                                        Assert.AreEqual(1, updates.Count, "Should be 1 add");
                                                        Assert.AreEqual(ChangeReason.Add, updates.First().Reason);
                                                    });
            _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Person1", 20)));

            _subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }


        [Test]
        public void FiresCompletedWhenDisposed()
        {
            bool completed = false;
            _subscriber = _stream.DistinctValues(p => p.Age)
                                            .Subscribe(updates => { },
                                                       () => { completed = true; });

            _source.Dispose();
            _subscriber.Dispose();
            Assert.IsTrue(completed, "Completed has not been invoked");
        }


        [Test]
        public void FiresErrorWhenAnExceptionIsThrown()
        {

            bool completed = false;
            bool error = false;
            _subscriber = _stream.DistinctValues(p => p.Age)
                                 .Finally(() => completed = true)  
                                 .SubscribeAndCatch(updates => { throw new Exception("Dodgy"); },ex => error = true);

            _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Person1", 20)));
            _subscriber.Dispose();

            Assert.IsTrue(error, "Error has not been invoked");
        }

        [Test]
        public void FiresManyValueForBatchOfDifferentAdds()
        {
            bool called = false;
            bool ended = false;
            _subscriber = _stream.DistinctValues(p => p.Age)
      
                                            .Subscribe(
                                                updates =>
                                                    {
                                                        called = true;
                                                        Assert.AreEqual(4, updates.Count, "Should be 4 adds");
                                                        foreach (var update in updates)
                                                        {
                                                            Assert.AreEqual(ChangeReason.Add, update.Reason);
                                                        }
                                                    });
            _source.BatchUpdate(updater =>
                {
                    updater.AddOrUpdate(new Person("Person1", 20));
                    updater.AddOrUpdate(new Person("Person2", 21));
                    updater.AddOrUpdate(new Person("Person3", 22));
                    updater.AddOrUpdate(new Person("Person4", 23));
                });

            _subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void FiresOnlyOnceForABatchOfUniqueValues()
        {
            bool called = false;
            _subscriber = _stream.DistinctValues(p => p.Age)
                                            .Subscribe(
                                                updates =>
                                                    {
                                                        called = true;
                                                        Assert.AreEqual(1, updates.Count, "Should be 1 add");
                                                        Assert.AreEqual(ChangeReason.Add, updates.First().Reason);
                                                    });
            _source.BatchUpdate(updater =>
                {
                    updater.AddOrUpdate(new Person("Person1", 20));
                    updater.AddOrUpdate(new Person("Person2", 20));
                    updater.AddOrUpdate(new Person("Person3", 20));
                    updater.AddOrUpdate(new Person("Person4", 20));
                });

            _subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void FiresRemoveWhenADistinctValueIsRemovedFromTheSource()
        {
            bool called = false;
            //skip first one a this is setting up the stream
            _subscriber = _stream.DistinctValues(p => p.Age).Skip(1)
                                            .Subscribe(
                                                updates =>
                                                    {
                                                        called = true;
                                                        Assert.AreEqual(1, updates.Count, "Should be 1 update");
                                                        foreach (var update in updates)
                                                        {
                                                            Assert.AreEqual(ChangeReason.Remove, update.Reason);
                                                        }
                                                    });

            //load feeder
            _source.BatchUpdate(updater => { updater.AddOrUpdate(new Person("Person1", 20)); });

            //remove
            _source.BatchUpdate(updater => { updater.Remove(new Person("Person1", 20)); });

            _subscriber.Dispose();
            Assert.IsTrue(called, "No update has been invoked");
        }

        [Test]
        public void ReceivesUpdateWhenFeederIsInvoked()
        {
            bool called = false;
            _subscriber = _stream.DistinctValues(p => p.Age)
                                            .Subscribe(updates => { called = true; });
            _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Person1", 20)));
            _subscriber.Dispose();
            Assert.IsTrue(called, "Subscription has not been invoked");
        }

        [Test]
        public void TimeFor10000Invocations()
        {
            ISubject<Person> subject = new Subject<Person>();
                
                bool called = false;
            _subscriber = _stream
              //  .IgnoreUpdateWhen((current,previous)=>current.Age!=previous.Age )
                .DistinctValues(p => p.Age)
                .Subscribe(updates => { called = true; });


            var update = new Person("Person1", 20);

            int items = 10000;

            Timer.ToConsole(() => subject.OnNext(update), items, "Subject alone");
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Person1", 20))), items, "Indvidual Updates");
            var people = Enumerable.Range(1, items).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToList();
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(people)), 10, "Batch Updates");
            
            _subscriber.Dispose();
          //  Assert.IsTrue(called, "Subscription has not been invoked");
        }

        [Test]
        public void TimeFor10000Invocations_Filter()
        {
            ISubject<Person> subject = new Subject<Person>();

            bool called = false;
            _subscriber = _stream
                //  .IgnoreUpdateWhen((current,previous)=>current.Age!=previous.Age )
                .Clone()
                //.Sort(new Persp)
                .Subscribe(updates => { called = true; });


            var update = new Person("Person1", 20);

            int items = 10000;

            Timer.ToConsole(() => subject.OnNext(update), items, "Subject alone");
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(new Person("Person1", 20))), items, "Indvidual Updates");
            var people = Enumerable.Range(1, items).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToList();
            Timer.ToConsole(() => _source.BatchUpdate(updater => updater.AddOrUpdate(people)), 10, "Batch Updates");

            _subscriber.Dispose();
            //  Assert.IsTrue(called, "Subscription has not been invoked");
        }

     }
}