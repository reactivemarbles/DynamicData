using DynamicData.Tests.Domain;
using Xunit;
using System;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class QueryWhenChangedFixture: IDisposable
    {
        private readonly ISourceList<Person> _source;
        private readonly ChangeSetAggregator<Person> _results;

        public  QueryWhenChangedFixture()
        {
            _source = new SourceList<Person>();
            _results = new ChangeSetAggregator<Person>(_source.Connect(p => p.Age > 20));
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void ChangeInvokedOnSubscriptionIfItHasData()
        {
            bool invoked = false;
            _source.Add(new Person("A", 1));
            var subscription = _source.Connect()
                                      .QueryWhenChanged()
                                      .Subscribe(x => invoked = true);
            invoked.Should().BeTrue();
            subscription.Dispose();
        }

        [Fact]
        public void CanHandleAddsAndUpdates()
        {
            bool invoked = false;
            var subscription = _source.Connect()
                .QueryWhenChanged(q => q.Count)
                .Subscribe(query => invoked = true);

            var person = new Person("A", 1);
            _source.Add(person);
            _source.Remove(person);

            invoked.Should().BeTrue();
            subscription.Dispose();
        }

        [Fact]
        public void ChangeInvokedOnNext()
        {
            bool invoked = false;

            var subscription = _source.Connect()
                .QueryWhenChanged()
                .Subscribe(x => invoked = true);

            invoked.Should().BeFalse();

            _source.Add(new Person("A", 1));
            invoked.Should().BeTrue();

            subscription.Dispose();
        }

        [Fact]
        public void ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
        {
            bool invoked = false;
            _source.Add(new Person("A", 1));
            var subscription = _source.Connect()
                .QueryWhenChanged(query => query.Count)
                .Subscribe(x => invoked = true);
            invoked.Should().BeTrue();
            subscription.Dispose();
        }

        [Fact]
        public void ChangeInvokedOnNext_WithSelector()
        {
            bool invoked = false;

            var subscription = _source.Connect()
                .QueryWhenChanged(query => query.Count)
                .Subscribe(x => invoked = true);

            invoked.Should().BeFalse();

            _source.Add(new Person("A", 1));
            invoked.Should().BeTrue();

            subscription.Dispose();
        }
    }
}
