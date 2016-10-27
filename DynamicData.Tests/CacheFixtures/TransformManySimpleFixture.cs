using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class TransformManySimpleFixture
    {
        private ISourceCache<PersonWithChildren, string> _source;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<PersonWithChildren, string>(p => p.Key);

            _results = _source.Connect().TransformMany(p => p.Relations, p => p.Name)
                .AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void Adds()
        {
            var parent = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child2", 2),
                new Person("Child3", 3)
            });
            _source.AddOrUpdate(parent);
            Assert.AreEqual(3, _results.Data.Count, "Should be 4 in the cache");

            Assert.IsTrue(_results.Data.Lookup("Child1").HasValue, "Child 1 should be in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child2").HasValue, "Child 2 should be in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child3").HasValue, "Child 3 should be in the cache");
        }


        [Test]
        public void Remove()
        {
            var parent = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child2", 2),
                new Person("Child3", 3)
            });
            _source.AddOrUpdate(parent);
            _source.Remove(parent);
            Assert.AreEqual(0, _results.Data.Count, "Should be 4 in the cache");
        }

        [Test]
        public void RemovewithIncompleteChildren()
        {
            var parent1 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child2", 2),
                new Person("Child3", 3)
            });
            _source.AddOrUpdate(parent1);

            var parent2 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child3", 3)
            });
            _source.Remove(parent2);
            Assert.AreEqual(0, _results.Data.Count, "Should be 0 in the cache");
        }

        [Test]
        public void UpdateWithLessChildren()
        {
            var parent1 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child2", 2),
                new Person("Child3", 3)
            });
            _source.AddOrUpdate(parent1);

            var parent2 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child3", 3),
            });
            _source.AddOrUpdate(parent2);
            Assert.AreEqual(2, _results.Data.Count, "Should be 2 in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child1").HasValue, "Child 1 should be in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child3").HasValue, "Child 3 should be in the cache");
        }


        [Test]
        public void UpdateWithMultipleChanges()
        {
            var parent1 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child2", 2),
                new Person("Child3", 3)
            });
            _source.AddOrUpdate(parent1);

            var parent2 = new PersonWithChildren("parent", 50, new Person[]
            {
                new Person("Child1", 1),
                new Person("Child3", 3),
                new Person("Child5", 3),
            });
            _source.AddOrUpdate(parent2);
            Assert.AreEqual(3, _results.Data.Count, "Should be 2 in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child1").HasValue, "Child 1 should be in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child3").HasValue, "Child 3 should be in the cache");
            Assert.IsTrue(_results.Data.Lookup("Child5").HasValue, "Child 5 should be in the cache");
        }
    }
}