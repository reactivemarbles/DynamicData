using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System;
using DynamicData.Binding;
using DynamicData.PLinq;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    internal class ToObservableChangeSetFixture : ReactiveTest
    {        
        private IObservable<Person> _observable;
        private TestScheduler _scheduler;
        private IDisposable _disposable;
        private ObservableCollectionExtended<Person> _target;

        [SetUp]
        public void Initialise()
        {
            _scheduler = new TestScheduler();
            _observable = _scheduler.CreateColdObservable(
                OnNext(1, new Person("One", 1)),
                OnNext(2, new Person("Two", 2)),
                OnNext(3, new Person("Three", 3))
                );
            _target = new ObservableCollectionExtended<Person>();            
            _disposable = _observable                
                .ToObservableChangeSet(2)                                
                //.AsObservableList()                
               // .Connect()                                                
                .Bind(_target)
                .Subscribe();            
        }

        [TearDown]
        public void Cleanup()
        {
            _disposable.Dispose();            
        }

        [Test]
        [Explicit("Turn off for now as this is a work in progresss")]
        public void ShouldLimitSizeOfBoundCollection()
        {
            _scheduler.AdvanceTo(2);
            Assert.AreEqual(2, _target.Count, "Should be 2 item in target collection");

            _scheduler.AdvanceTo(3);
         //   Assert.AreEqual(2, _target.Count, "Should be 2 item in target collection because of size limit");

            _scheduler.AdvanceTo(3);
        }
    }
}
