using System;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    [TestFixture]
    public class IgnoreUpdateFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void SetUp()
        {
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = new ChangeSetAggregator<Person, string>
                (
                _source.Connect().IgnoreUpdateWhen((current, previous) => current == previous)
                );
        }

        [Test]
        public void IgnoreFunctionWillIgnoreSubsequentUpdatesOfAnItem()
        {
            var person = new Person("Person", 10);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(person);
            _source.AddOrUpdate(person);

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }
    }
}
