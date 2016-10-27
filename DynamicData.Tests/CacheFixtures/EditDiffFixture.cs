
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class EditDiffFixture
    {
        private SourceCache<Person, string> _cache;
        private ChangeSetAggregator<Person, string> _result;

        [SetUp]
        public void Initialise()
        {
            _cache = new SourceCache<Person, string>(p => p.Name);
            _result = _cache.Connect().AsAggregator();
            _cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
        }

        [TearDown]
        public void OnTestCompleted()
        {
            _cache.Dispose();
            _result.Dispose();
        }



        [Test]
        public void New()
        {
            var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

            Assert.AreEqual(15, _cache.Count);
            CollectionAssert.AreEquivalent(newPeople, _cache.Items);
            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
        }

        [Test]
        public void EditWithSameData()
        {
            var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

            Assert.AreEqual(10, _cache.Count);
            CollectionAssert.AreEquivalent(newPeople, _cache.Items);
            Assert.AreEqual(1, _result.Messages.Count);
        }

        [Test]
        public void Amends()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            Assert.AreEqual(3, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(0, lastChange.Adds);
            Assert.AreEqual(3, lastChange.Updates);
            Assert.AreEqual(7, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }

        [Test]
        public void Removes()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            Assert.AreEqual(7, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(0, lastChange.Adds);
            Assert.AreEqual(0, lastChange.Updates);
            Assert.AreEqual(3, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }


        [Test]
        public void VariousChanges()
        {
            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

            _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

            Assert.AreEqual(10, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
            Assert.AreEqual(5, lastChange.Updates);
            Assert.AreEqual(5, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }

        [Test]
        public void New_WithEqualityComparer()
        {
            var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, Person.AgeComparer);

            Assert.AreEqual(15, _cache.Count);
            CollectionAssert.AreEquivalent(newPeople, _cache.Items);
            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
        }

        [Test]
        public void EditWithSameData_WithEqualityComparer()
        {
            var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

            _cache.EditDiff(newPeople, Person.AgeComparer);

            Assert.AreEqual(10, _cache.Count);
            CollectionAssert.AreEquivalent(newPeople, _cache.Items);
            var lastChange = _result.Messages.Last();
            Assert.AreEqual(1, _result.Messages.Count);
        }

        [Test]
        public void Amends_WithEqualityComparer()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, Person.AgeComparer);

            Assert.AreEqual(3, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(0, lastChange.Adds);
            Assert.AreEqual(3, lastChange.Updates);
            Assert.AreEqual(7, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }

        [Test]
        public void Removes_WithEqualityComparer()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, Person.AgeComparer);

            Assert.AreEqual(7, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(0, lastChange.Adds);
            Assert.AreEqual(0, lastChange.Updates);
            Assert.AreEqual(3, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }


        [Test]
        public void VariousChanges_WithEqualityComparer()
        {
            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

            _cache.EditDiff(newList, Person.AgeComparer);

            Assert.AreEqual(10, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
            Assert.AreEqual(5, lastChange.Updates);
            Assert.AreEqual(5, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }
    }
}
