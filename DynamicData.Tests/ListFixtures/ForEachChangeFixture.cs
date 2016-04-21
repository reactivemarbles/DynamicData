using System;
using System.Collections.Generic;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class ForEachChangeFixture
    {
        private ISourceList<Person> _source;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void EachChangeInokesTheCallback()
        {
            var messages = new List<Change<Person>>();

            var messageWriter = _source
                .Connect()
                .ForEachChange(messages.Add)
                .Subscribe();

            var people = new RandomPersonGenerator().Take(100);
            people.ForEach(_source.Add);

            Assert.AreEqual(100, messages.Count);
            messageWriter.Dispose();
        }

        [Test]
        public void EachItemChangeInokesTheCallback()
        {
            var messages = new List<ItemChange<Person>>();

            var messageWriter = _source
                .Connect()
                .ForEachItemChange(messages.Add)
                .Subscribe();

            _source.AddRange(new RandomPersonGenerator().Take(100));

            Assert.AreEqual(100, messages.Count);
            messageWriter.Dispose();
        }

        [Test]
        public void EachItemChangeInokesTheCallbac2()
        {
            var messages = new List<ItemChange<Person>>();

            var messageWriter = _source
                .Connect()
                .ForEachItemChange(messages.Add)
                .Subscribe();
            _source.AddRange(new RandomPersonGenerator().Take(5));
            _source.InsertRange(new RandomPersonGenerator().Take(5), 2);
            _source.AddRange(new RandomPersonGenerator().Take(5));

            Assert.AreEqual(15, messages.Count);
            messageWriter.Dispose();
        }
    }
}
