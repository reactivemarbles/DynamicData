using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using Xunit;
using System;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class ForEachChangeFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _source;

        public  ForEachChangeFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
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
