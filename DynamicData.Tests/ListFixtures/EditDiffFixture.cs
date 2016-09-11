using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{

    [TestFixture]
    public class EditDiffFixture
    {
        private SourceList<Person> _cache;
        private ChangeSetAggregator<Person> _result;

        [SetUp]
        public void Initialise()
        {
            _cache = new SourceList<Person>();
            _result = _cache.Connect().AsAggregator();
            _cache.AddRange(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
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

            _cache.EditDiff(newPeople, Person.NameAgeGenderComparer);

            Assert.AreEqual(15, _cache.Count);
            CollectionAssert.AreEquivalent(newPeople, _cache.Items);
            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
        }

        [Test]
        public void Amends()
        {
            var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            Assert.AreEqual(3, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(3, lastChange.Adds);
            Assert.AreEqual(10, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }

        [Test]
        public void Removes()
        {
            var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            Assert.AreEqual(7, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(0, lastChange.Adds);
            Assert.AreEqual(3, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }


        [Test]
        public void VariousChanges()
        {

            var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i )).ToArray();

            _cache.EditDiff(newList, Person.NameAgeGenderComparer);

            Assert.AreEqual(10, _cache.Count);

            var lastChange = _result.Messages.Last();
            Assert.AreEqual(5, lastChange.Adds);
            Assert.AreEqual(5, lastChange.Removes);

            CollectionAssert.AreEquivalent(newList, _cache.Items);
        }
    }
}
