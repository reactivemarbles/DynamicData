using DynamicData.Tests.Domain;
using NUnit.Framework;
using System;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    [TestFixture]
    public class QueryWhenChangedFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = new ChangeSetAggregator<Person, string>(_source.Connect(p => p.Age > 20));
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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
