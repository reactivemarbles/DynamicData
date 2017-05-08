using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class GroupOnFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<IGroup<Person, int>> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().GroupOn(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void CleeanUp()
        {
            _source.Dispose();
        }

        [Test]
        public void Add()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");

            var firstGroup = _results.Data.Items.First().List.Items.ToArray();
            Assert.AreEqual(person, firstGroup[0], "Should be same person");
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);
            _source.Remove(person);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be no groups");
        }

        [Test]
        public void UpdateWillChangeTheGroup()
        {
            var person = new Person("Adult1", 50);
            var amended = new Person("Adult1", 60);
            _source.Add(person);
            _source.ReplaceAt(0, amended);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");

            var firstGroup = _results.Data.Items.First().List.Items.ToArray();
            Assert.AreEqual(amended, firstGroup[0], "Should be same person");
        }

        [Test]
        public void BigList()
        {
            var generator = new RandomPersonGenerator();
            var people = generator.Take(10000).ToArray();
            _source.AddRange(people);

            Console.WriteLine();
        }
    }
}
