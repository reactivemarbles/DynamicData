using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class AutoRefreshFixture
    {
        [Test]
        public void AutoRefresh()
        {
            var items = Enumerable.Range(1, 100)
                .Select(i => new Person("Person" + i, 1))
                .ToArray();

            //result should only be true when all items are set to true
            using (var cache = new SourceList<Person>())
            using (var results = cache.Connect().AutoRefresh(nameof(Person.Age)).AsAggregator())
            {
                cache.AddRange(items);

                results.Data.Count.Should().Be(100);
                results.Messages.Count.Should().Be(1);

                items[0].Age = 10;
                results.Data.Count.Should().Be(100);
                results.Messages.Count.Should().Be(2);

                results.Messages[1].First().Reason.Should().Be(ListChangeReason.Refresh);

                //remove an item and check no change is fired
                var toRemove = items[1];
                cache.Remove(toRemove);
                results.Data.Count.Should().Be(99);
                results.Messages.Count.Should().Be(3);
                toRemove.Age = 100;
                results.Messages.Count.Should().Be(3);

                //add it back in and check it updates
                cache.Add(toRemove);
                results.Messages.Count.Should().Be(4);
                toRemove.Age = 101;
                results.Messages.Count.Should().Be(5);

                results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Refresh);
            }
        }

        [Test]
        public void AutoRefreshBatched()
        {
            var scheduler = new TestScheduler();

            var items = Enumerable.Range(1, 100)
                .Select(i => new Person("Person" + i, 1))
                .ToArray();

            //result should only be true when all items are set to true
            using (var cache = new SourceList<Person>())
            using (var results = cache.Connect().AutoRefresh(nameof(Person.Age), TimeSpan.FromSeconds(1), scheduler).AsAggregator())
            {
                cache.AddRange(items);

                results.Data.Count.Should().Be(100);
                results.Messages.Count.Should().Be(1);
                
                //update 50 records
                items.Skip(50)
                    .ForEach(p => p.Age = p.Age + 1);

                scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

                //should be another message with 50 refreshes
                results.Messages.Count.Should().Be(2);
                results.Messages[1].Refreshes.Should().Be(50);
            }
        }
    }
}
