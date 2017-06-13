using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class GroupOnPropertyWithImmutableStateFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<List.IGrouping<Person, int>> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().GroupOnPropertyWithImmutableState(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void CanGroupOnAdds()
        {
            _source.Add(new Person("A", 10));

            Assert.AreEqual(1, _results.Data.Count);

            var firstGroup = _results.Data.Items.First();

            Assert.AreEqual(1, firstGroup.Count);
            Assert.AreEqual(10, firstGroup.Key);
        }

        [Test]
        public void CanRemoveFromGroup()
        {
            var person = new Person("A", 10);
            _source.Add(person);
            _source.Remove(person);

            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void Regroup()
        {
            var person = new Person("A", 10);
            _source.Add(person);
            person.Age = 20;

            Assert.AreEqual(1, _results.Data.Count);
            var firstGroup = _results.Data.Items.First();

            Assert.AreEqual(1, firstGroup.Count);
            Assert.AreEqual(20, firstGroup.Key);
        }

        [Test]
        public void CanHandleAddBatch()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(1000).ToArray();

            _source.AddRange(people);

            var expectedGroupCount = people.Select(p => p.Age).Distinct().Count();
            Assert.AreEqual(expectedGroupCount, _results.Data.Count);
        }

        [Test]
        public void CanHandleChangedItemsBatch()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(100).ToArray();

            _source.AddRange(people);

            var initialCount = people.Select(p => p.Age).Distinct().Count();
            Assert.AreEqual(initialCount, _results.Data.Count);

            people.Take(25)
                .ForEach(p => p.Age = 200);


            var changedCount = people.Select(p => p.Age).Distinct().Count();
            Assert.AreEqual(changedCount, _results.Data.Count);

            //check that each item is only in one cache
            var peopleInCache = _results.Data.Items
                .SelectMany(g => g.Items)
                .ToArray();

            Assert.AreEqual(100, peopleInCache.Length);

        }
    }
}