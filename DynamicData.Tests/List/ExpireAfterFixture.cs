using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using Xunit;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class ExpireAfterFixture: IDisposable
    {
        private readonly ISourceList<Person> _source;
        private readonly ChangeSetAggregator<Person> _results;

        private readonly TestScheduler _scheduler;

        public ExpireAfterFixture()
        {
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
            _results = _source.Connect().AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void ComplexRemove()
        {
            TimeSpan? RemoveFunc(Person t)
            {
                if (t.Age <= 40)
                    return TimeSpan.FromSeconds(5);

                if (t.Age <= 80)
                    return TimeSpan.FromSeconds(7);
                return null;
            }

            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _source.AddRange(items);

            var remover = _source.ExpireAfter(RemoveFunc, _scheduler).Subscribe();
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5010).Ticks);

            _source.Count.Should().Be(60, "40 items should have been removed from the cache");

            _scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);
            _source.Count.Should().Be(20, "80 items should have been removed from the cache");

            remover.Dispose();
        }

        [Fact]
        public void ItemAddedIsExpired()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();

            _source.Add(new Person("Name1", 10));

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds in the first update");
            _results.Messages[1].Removes.Should().Be(1, "Should be 1 removes in the second update");
        }

        [Fact]
        public void ExpireIsCancelledWhenUpdated()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();

            var p1 = new Person("Name1", 20);
            var p2 = new Person("Name1", 21);

            _source.Add(p1);

            _source.Replace(p1, p2);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();
            _results.Data.Count.Should().Be(0, "Should be no data in the cache");
            _results.Messages.Count.Should().Be(3, "Should be 3 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 add in the first message");
            _results.Messages[1].Replaced.Should().Be(1, "Should be 1 update in the second message");
            _results.Messages[2].Removes.Should().Be(1, "Should be 1 remove in the 3rd message");
        }

        [Fact]
        public void CanHandleABatchOfUpdates()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();
            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            _source.AddRange(items);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();

            _results.Data.Count.Should().Be(0, "Should be no data in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(100, "Should be 100 adds in the first message");
            _results.Messages[1].Removes.Should().Be(100, "Should be 100 removes in the second message");
        }
    }
}
