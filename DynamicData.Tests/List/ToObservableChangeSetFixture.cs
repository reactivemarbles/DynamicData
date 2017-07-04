using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using Xunit;
using System;
using System.Collections.Generic;
using FluentAssertions;

namespace DynamicData.Tests.List
{

    public class ToObservableChangeSetFixture : ReactiveTest, IDisposable
    {
        private IObservable<Person> _observable;
        private readonly TestScheduler _scheduler;
        private readonly IDisposable _disposable;
        private readonly List<Person> _target;

        private readonly Person _person1 = new Person("One", 1);
        private readonly Person _person2 = new Person("Two", 2);
        private readonly Person _person3 = new Person("Three", 3);

        public  ToObservableChangeSetFixture()
        {
            _scheduler = new TestScheduler();
            _observable = _scheduler.CreateColdObservable(
                OnNext(1, _person1),
                OnNext(2, _person2),
                OnNext(3, _person3));

            _target = new List<Person>();

            _disposable = _observable
                .ToObservableChangeSet(2, _scheduler)
                .Clone(_target)
                .Subscribe();
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }

        [Fact]
        public void ShouldLimitSizeOfBoundCollection()
        {
            _scheduler.AdvanceTo(2);
            _target.Count.Should().Be(2, "Should be 2 item in target collection");

            _scheduler.AdvanceTo(3);
            _target.Count.Should().Be(2, "Should be 2 item in target collection because of size limit");

            var expected = new[] {_person2, _person3};

            _target.ShouldAllBeEquivalentTo(expected);
        }
    }
}
