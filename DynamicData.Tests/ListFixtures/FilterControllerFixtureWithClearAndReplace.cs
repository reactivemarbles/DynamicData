using System.Linq;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class FilterControllerFixtureWithClearAndReplace
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private FilterController<Person> _filter;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _filter = new FilterController<Person>(p => p.Age > 20);
            _results = _source.Connect().Filter(_filter).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void ChangeFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();

            _source.AddRange(people);
            Assert.AreEqual(80, _results.Data.Count, "Should be 80 people in the cache");

            _filter.Change(p => p.Age <= 50);
            Assert.AreEqual(50, _results.Data.Count, "Should be 50 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");
            //  Assert.AreEqual(50, _results.Messages[1].Removes, "Should be 50 removes in the second message");
            // Assert.AreEqual(20, _results.Messages[1].Adds, "Should be 20 adds in the second message");

            Assert.IsTrue(_results.Data.Items.All(p => p.Age <= 50));
        }

        [Test]
        public void ReevaluateFilter()
        {
            //re-evaluate for inline changes
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

            _source.AddRange(people);
            Assert.AreEqual(80, _results.Data.Count, "Should be 80 people in the cache");

            foreach (var person in people)
            {
                person.Age = person.Age + 10;
            }
            _filter.Reevaluate();

            Assert.AreEqual(90, _results.Data.Count, "Should be 90 people in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 update messages");
            Assert.AreEqual(0, _results.Messages[1].Removes, "Should be 80 removes in the second message");
            Assert.AreEqual(10, _results.Messages[1].Adds, "Should be 10 adds in the second message");

            foreach (var person in people)
            {
                person.Age = person.Age - 10;
            }
            _filter.Reevaluate();

            Assert.AreEqual(80, _results.Data.Count, "Should be 80 people in the cache");
            Assert.AreEqual(3, _results.Messages.Count, "Should be 3 update messages");
            // Assert.AreEqual(10, _results.Messages[2].Removes, "Should be 10 removes in the third message");
        }

        #region Static filter tests

        /* Should be the same as standard lambda filter */

        [Test]
        public void AddMatched()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddNotMatched()
        {
            var person = new Person("Adult1", 10);
            _source.Add(person);

            Assert.AreEqual(0, _results.Messages.Count, "Should have no item updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void AddNotMatchedAndUpdateMatched()
        {
            const string key = "Adult1";
            var notmatched = new Person(key, 19);
            var matched = new Person(key, 21);

            _source.Edit(updater =>
            {
                updater.Add(notmatched);
                updater.Add(matched);
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(matched, _results.Messages[0].First().Range.First(), "Should be same person");
            Assert.AreEqual(matched, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
            _source.Remove(new Person("A", 1));
            Assert.AreEqual(0, _results.Messages.Count, "Should be 0 updates");
        }

        [Test]
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            _source.AddRange(people);
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(80, _results.Messages[0].Adds, "Should return 80 adds");

            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
        }

        [Test]
        public void BatchRemoves()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

            _source.AddRange(people);
            _source.Clear();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(80, _results.Messages[0].Adds, "Should be 80 addes");
            Assert.AreEqual(80, _results.Messages[1].Removes, "Should be 80 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");
        }

        [Test]
        public void BatchSuccessiveUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
            foreach (var person in people)
            {
                Person person1 = person;
                _source.Add(person1);
            }

            Assert.AreEqual(80, _results.Messages.Count, "Should be 80 messages");
            Assert.AreEqual(80, _results.Data.Count, "Should be 80 in the cache");
            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
        }

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
            _source.AddRange(people);
            _source.Clear();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(80, _results.Messages[0].Adds, "Should be 80 addes");
            Assert.AreEqual(80, _results.Messages[1].Removes, "Should be 80 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");
        }

        [Test]
        public void Remove()
        {
            const string key = "Adult1";
            var person = new Person(key, 50);

            _source.Add(person);
            _source.Remove(person);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 80 addes");
            Assert.AreEqual(1, _results.Messages[1].Removes, "Should be 80 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");
        }

        [Test]
        public void UpdateMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);

            _source.Add(newperson);
            _source.Replace(newperson, updated);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds");
            Assert.AreEqual(1, _results.Messages[1].Replaced, "Should be 1 update");
        }

        [Test]
        public void SameKeyChanges()
        {
            const string key = "Adult1";

            _source.Edit(updater =>
            {
                updater.Add(new Person(key, 50));
                updater.Add(new Person(key, 52));
                updater.Add(new Person(key, 53));
                //    updater.Remove(key);
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(3, _results.Messages[0].Adds, "Should be 3 adds");
            //Assert.AreEqual(1, _results.Messages[0].Removes, "Should be 1 remove");
        }

        [Test]
        public void UpdateNotMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 10);
            var updated = new Person(key, 11);

            _source.Add(newperson);
            _source.Replace(newperson, updated);

            Assert.AreEqual(0, _results.Messages.Count, "Should be no updates");
            Assert.AreEqual(0, _results.Data.Count, "Should nothing cached");
        }

        #endregion
    }
}
