using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.List
{
    
    public class GroupOnPropertyFixture: IDisposable
    {
        private readonly ISourceList<Person> _source;
        private readonly ChangeSetAggregator<DynamicData.List.IGrouping<Person, int>> _results;


        public  GroupOnPropertyFixture()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().GroupOnPropertyWithImmutableState(p=>p.Age).AsAggregator();
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void CanGroupOnAdds()
        {
            _source.Add(new Person("A",10));

            _results.Data.Count.Should().Be(1);

            var firstGroup= _results.Data.Items.First();

            firstGroup.Count.Should().Be(1);
            firstGroup.Key.Should().Be(10);
        }

        [Test]
        public void CanRemoveFromGroup()
        {
            var person = new Person("A", 10);
            _source.Add(person);
            _source.Remove(person);

            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void Regroup()
        {
            var person = new Person("A", 10);
            _source.Add(person);
            person.Age = 20;

            _results.Data.Count.Should().Be(1);
            var firstGroup = _results.Data.Items.First();

            firstGroup.Count.Should().Be(1);
            firstGroup.Key.Should().Be(20);
        }

        [Test]
        public void CanHandleAddBatch()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(1000).ToArray();
            
            _source.AddRange(people);

            var expectedGroupCount = people.Select(p => p.Age).Distinct().Count();
            _results.Data.Count.Should().Be(expectedGroupCount);
        }

        [Test]
        public void CanHandleChangedItemsBatch()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(100).ToArray();

            _source.AddRange(people);

            var initialCount = people.Select(p => p.Age).Distinct().Count();
            _results.Data.Count.Should().Be(initialCount);

            people.Take(25)
                    .ForEach(p=>p.Age=200);


             var changedCount = people.Select(p => p.Age).Distinct().Count();
            _results.Data.Count.Should().Be(changedCount);

            //check that each item is only in one cache
            var peopleInCache = _results.Data.Items
                .SelectMany(g => g.Items)
                .ToArray();

            peopleInCache.Length.Should().Be(100);

        }
    }
}
