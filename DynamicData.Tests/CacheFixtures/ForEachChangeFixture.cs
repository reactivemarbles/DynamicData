using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using System;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    [TestFixture]
    public class ForEachChangeFixture
    {
        private ISourceCache<Person, string> _source;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void Test()
        {
            var messages = new List<Change<Person, string>>();
            var messageWriter = _source
                .Connect()
                .ForEachChange(messages.Add)
                .Subscribe();

            _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
            messageWriter.Dispose();

            messages.Count.Should().Be(100);
        }
    }
}
