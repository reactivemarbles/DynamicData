using System;
using System.Collections.Generic; 
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    internal class ToObservableChangeSetFixture : ReactiveTest
    {        
        private IObservable<Person> _observable;
        private TestScheduler _scheduler;
        private IDisposable _disposable;
        private List<Person> _target;

        [SetUp]
        public void Initialise()
        {
            _scheduler = new TestScheduler();
            _observable = _scheduler.CreateColdObservable(
                OnNext(1, new Person("One", 1)),
                OnNext(2, new Person("Two", 2)),
                OnNext(3, new Person("Three", 3)));

            _target = new List<Person>();

            _disposable = _observable                
                .ToObservableChangeSet(p=>p.Key,limitSizeTo: 2, scheduler: _scheduler)                                                                          
                .Clone(_target)
                .Subscribe();            
        }

        [TearDown]
        public void Cleanup()
        {
            _disposable.Dispose();            
        }

        [Test]
        public void ShouldLimitSizeOfBoundCollection()
        {
            _scheduler.AdvanceTo(2);
            Assert.AreEqual(2, _target.Count, "Should be 2 item in target collection");

           _scheduler.AdvanceTo(3);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks); //push time forward as size limit is checked for after the event 

            Assert.AreEqual(2, _target.Count, "Should be 2 item in target collection because of size limit");
        }
    }
}
