using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    
    public class FilterOnPropertyFixture
    {
        [Fact]
        public void InitialValues()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                1.Should().Be(stub.Results.Messages.Count);
                82.Should().Be(stub.Results.Data.Count);

                stub.Results.Data.Items.ShouldAllBeEquivalentTo(people.Skip(18));
            }
        }

        [Fact]
        public void ChangeAValueToMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                people[20].Age = 10;

                2.Should().Be(stub.Results.Messages.Count);
                81.Should().Be(stub.Results.Data.Count);
            }
        }

        [Fact]
        public void ChangeAValueToNoLongerMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                people[10].Age = 20;

                2.Should().Be(stub.Results.Messages.Count);
                83.Should().Be(stub.Results.Data.Count);
            }
        }

        [Fact]
        public void ChangeAValueSoItIsStillInTheFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                people[50].Age = 100;

                1.Should().Be(stub.Results.Messages.Count);
                82.Should().Be(stub.Results.Data.Count);
            }
        }

        private class FilterPropertyStub : IDisposable
        {
            public ISourceList<Person> Source { get; } = new SourceList<Person>();
            public ChangeSetAggregator<Person> Results { get; }


            public FilterPropertyStub()
            {
                Results = new ChangeSetAggregator<Person>(Source.Connect().FilterOnProperty(p => p.Age, p => p.Age > 18));
            }

            public void Dispose()
            {
                Source.Dispose();
                Results.Dispose();
            }
        }
    }
}