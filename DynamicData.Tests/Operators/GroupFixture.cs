using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Controllers;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{

    [TestFixture]
    public class GroupControllerFixture
    {
        private const string Under20 = "Under20";
        private const string Under60 = "21 to 59";
        private const string Pensioner = "Pensioners";

        public enum AgeBracket
        {
            Under20,
            Adult,
            Pensioner
        }

        private readonly Func<Person, AgeBracket> _grouper = p =>
                                                {
                                                    if (p.Age <= 19) return AgeBracket.Under20;
                                                    if (p.Age <= 60) return AgeBracket.Adult;
                                                    return AgeBracket.Pensioner;
                                                };

        private ISourceCache<Person, string> _source;
        private GroupController  _controller;
        private IObservableCache<IGroup<Person, string, AgeBracket>, AgeBracket> _grouped;


        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _controller =new GroupController();
            _grouped = _source.Connect().Group(_grouper, _controller).AsObservableCache();



        }

            
         [Test]
        public void RegroupRecaluatesGroupings()
        {

            var p1 = new Person("P1", 10);
            var p2 = new Person("P2", 15);
            var p3 = new Person("P3", 30);
            var p4 = new Person("P4", 70);
            var people = new[] { p1, p2, p3, p4 };

            _source.AddOrUpdate(people);

            Assert.IsTrue(IsContainedIn("P1", AgeBracket.Under20));
            Assert.IsTrue(IsContainedIn("P2", AgeBracket.Under20));
            Assert.IsTrue(IsContainedIn("P3", AgeBracket.Adult));
            Assert.IsTrue(IsContainedIn("P4", AgeBracket.Pensioner));

             p1.Age = 60;
             p2.Age = 80;
             p3.Age = 15;
             p4.Age = 30;

             _controller.RefreshGroup();

             Assert.IsTrue(IsContainedIn("P1", AgeBracket.Adult));
             Assert.IsTrue(IsContainedIn("P2", AgeBracket.Pensioner));
             Assert.IsTrue(IsContainedIn("P3", AgeBracket.Under20));
             Assert.IsTrue(IsContainedIn("P4", AgeBracket.Adult));

        }

         private bool IsContainedIn(string name, AgeBracket bracket)
         {
             var group = _grouped.Lookup(bracket);
             if (!group.HasValue) return false;

             return group.Value.Cache.Lookup(name).HasValue;
        }

    }

    [TestFixture]
    public class GroupFixture
    {
        [SetUp]
        public void Initialise()
        {
            _source =new SourceCache<Person, string>(p=>p.Name);
        }

        [TearDown]
        public void CleeanUp()
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
                                            .Subscribe(updates =>
                                                {
                                                    called = true;
                                                });
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person1", 20));
            subscriber.Dispose();
            Assert.IsFalse(called, "No update has been invoked");
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
            _source.BatchUpdate(updater =>
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
            _source.BatchUpdate(updater =>
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
            //load feeder
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
            IDisposable subscriber = _source.Connect().Group(p => p.Age)
                                            .Subscribe(updates => { called = true; });
            _source.AddOrUpdate(new Person("Person1", 20));
            subscriber.Dispose();
            Assert.IsTrue(called, "Subscription has not been invoked");
        }
    }
}