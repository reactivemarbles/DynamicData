using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DynamicData.Tests.ListFixtures
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
                .ToObservableChangeSet(2, _scheduler)                                                                          
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
            Assert.AreEqual(2, _target.Count, "Should be 2 item in target collection because of size limit");
            
            var expected = new[] {new Person("Two", 2), new Person("Three", 3)};

            CollectionAssert.AreEquivalent(expected, _target);
        }
    }
}
