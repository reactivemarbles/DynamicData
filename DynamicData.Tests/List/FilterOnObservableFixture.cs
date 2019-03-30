using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    public class FilterOnObservableFixture
    {
        [Fact]
        public void InitialValues()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                // should have 100-18 left
                stub.Results.Data.Count.Should().Be(82);

                // initial addrange, refreshes to filter out < 18
                stub.Results.Messages.Count.Should().Be(1+18);

                stub.Results.Data.Items.ShouldAllBeEquivalentTo(people.Skip(18));
            }
        }

        [Fact]
        public void ChangeAValueToMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                people[20].SetAge(10);

                // should have 100-18-1 left
                stub.Results.Data.Count.Should().Be(81);

                // initial addrange, refreshes to filter out < 18 and then refresh for the filter change
                stub.Results.Messages.Count.Should().Be(1+18+1);
            }
        }

        [Fact]
        public void ChangeAValueToNoLongerMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                // should have 100-18 left
                stub.Results.Data.Count.Should().Be(82);

                stub.Results.Messages.Count.Should().Be(1+18);

                people[10].SetAge(20);

                // should have 82+1 left
                stub.Results.Data.Count.Should().Be(83);

                // initial addrange, refreshes to filter out < 18 and then one refresh for the filter change
                stub.Results.Messages.Count.Should().Be(1+18+1);
            }
        }

        [Fact]
        public void ChangeAValueSoItIsStillInTheFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);

                people[50].SetAge(100);
                stub.Results.Data.Count.Should().Be(82);
                // initial addrange, refreshes to filter out < 18 and then no refresh for the no-op filter change
                stub.Results.Messages.Count.Should().Be(1+18+0);
            }
        }

        [Fact]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);
                stub.Source.Clear();

                stub.Results.Data.Count.Should().Be(0);
            }
        }


        [Fact]
        public void RemoveRange()
        {
            var people = Enumerable.Range(1, 100).Select(i => new PersonObs ("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddRange(people);
                stub.Source.RemoveRange(89,10);

                stub.Results.Data.Count.Should().Be(72);
                // initial addrange, refreshes to filter out < 18 and then removerange
                stub.Results.Messages.Count.Should().Be(1+18+1);
            }
        }

        private class FilterPropertyStub : IDisposable
        {
            public ISourceList<PersonObs > Source { get; } = new SourceList<PersonObs >();
            public ChangeSetAggregator<PersonObs > Results { get; }


            public FilterPropertyStub()
            {
                Results = new ChangeSetAggregator<PersonObs>(Source.Connect()
                    .FilterOnObservable(p => p.Age.Select(v => v > 18)));
            }

            public void Dispose()
            {
                Source.Dispose();
                Results.Dispose();
            }
        }
    }
}