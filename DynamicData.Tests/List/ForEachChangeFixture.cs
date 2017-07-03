using System;
using System.Collections.Generic;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class ForEachChangeFixture
    {
        private ISourceList<Person> _source;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
        }

        public void Dispose()
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

            messages.Count.Should().Be(100);
            messageWriter.Dispose();
        }

        [Test]
        public void EachItemChangeInokesTheCallback()
        {
            var messages = new List<ItemChange<Person>>();

            var messageWriter = _source.Connect()
                .ForEachItemChange(messages.Add)
                .Subscribe();

            _source.AddRange(new RandomPersonGenerator().Take(100));

            messages.Count.Should().Be(100);
            messageWriter.Dispose();
        }

        [Test]
        public void EachItemChangeInokesTheCallbac2()
        {
            var messages = new List<ItemChange<Person>>();

            var messageWriter = _source.Connect()
                .ForEachItemChange(messages.Add)
                .Subscribe();
            _source.AddRange(new RandomPersonGenerator().Take(5));
            _source.InsertRange(new RandomPersonGenerator().Take(5), 2);
            _source.AddRange(new RandomPersonGenerator().Take(5));

            messages.Count.Should().Be(15);
            messageWriter.Dispose();
        }
    }
}
