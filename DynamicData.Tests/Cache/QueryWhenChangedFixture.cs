using DynamicData.Tests.Domain;
using Xunit;
using System;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class QueryWhenChangedFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _source;
        private readonly ChangeSetAggregator<Person, string> _results;

        public QueryWhenChangedFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = new ChangeSetAggregator<Person, string>(_source.Connect(p => p.Age > 20));
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
            _source.AddOrUpdate(new Person("A", 1));
            var subscription = _source.Connect()
                                      .QueryWhenChanged()
                                      .Subscribe(x => invoked = true);
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

            _source.AddOrUpdate(new Person("A", 1));
            invoked.Should().BeTrue();

            subscription.Dispose();
        }

        [Fact]
        public void ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
        {
            bool invoked = false;
            _source.AddOrUpdate(new Person("A", 1));
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

            _source.AddOrUpdate(new Person("A", 1));
            invoked.Should().BeTrue();

            subscription.Dispose();
        }
    }
}
