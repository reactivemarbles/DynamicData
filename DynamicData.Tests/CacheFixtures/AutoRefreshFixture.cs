using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
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
            using (var cache = new SourceCache<Person, string>(m => m.Name))
            using (var results = cache.Connect().AutoRefresh(p=>p.Age).AsAggregator())
            {
                cache.AddOrUpdate(items);

                results.Data.Count.Should().Be(100);
                results.Messages.Count.Should().Be(1);

                items[0].Age = 10;
                results.Data.Count.Should().Be(100);
                results.Messages.Count.Should().Be(2);

                results.Messages[1].First().Reason.Should().Be(ChangeReason.Refresh);

                //remove an item and check no change is fired
                var toRemove = items[1];
                cache.Remove(toRemove);
                results.Data.Count.Should().Be(99);
                results.Messages.Count.Should().Be(3);
                toRemove.Age = 100;
                results.Messages.Count.Should().Be(3);

                //add it back in and check it updates
                cache.AddOrUpdate(toRemove);
                results.Messages.Count.Should().Be(4);
                toRemove.Age = 101;
                results.Messages.Count.Should().Be(5);

                results.Messages.Last().First().Reason.Should().Be(ChangeReason.Refresh);
            }
        }
    }
}
