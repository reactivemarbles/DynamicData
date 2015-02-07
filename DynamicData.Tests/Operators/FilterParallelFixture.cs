using System.Linq;
using DynamicData.PLinq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class FilterParallelFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source =new SourceCache<Person, string>(p=>p.Key);
            _results = new ChangeSetAggregator<Person, string>(_source.Connect().Filter(p => p.Age > 20,new ParallelisationOptions(ParallelType.Ordered)));
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void AddMatched()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddNotMatched()
        {
            var person = new Person("Adult1", 10);
            _source.AddOrUpdate(person);

            Assert.AreEqual(0, _results.Messages.Count, "Should have no item updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void AddNotMatchedAndUpdateMatched()
        {
            const string key = "Adult1";
            var notmatched = new Person(key, 19);
            var matched = new Person(key, 21);

            _source.BatchUpdate(updater =>
                               {
                                   updater.AddOrUpdate(notmatched);
                                   updater.AddOrUpdate(matched);
                               });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(matched, _results.Messages[0].First().Current, "Should be same person");
            Assert.AreEqual(matched, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
            const string key = "Adult1";
            _source.BatchUpdate(updater => updater.Remove(key));
            Assert.AreEqual(0, _results.Messages.Count, "Should be 0 updates");
        }

        [Test]
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(80, _results.Messages[0].Adds, "Should return 80 adds");

            var filtered = people.Where(p => p.Age > 20).OrderBy(p=>p.Name).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p => p.Name), "Incorrect Filter result");
        }


        [Test]
        public void BatchRemoves()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _source.BatchUpdate(updater => updater.Remove(people));

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
                _source.BatchUpdate(updater => updater.AddOrUpdate(person1));
            }

            Assert.AreEqual(80, _results.Messages.Count, "Should be 100 updates");
            Assert.AreEqual(80, _results.Data.Count, "Should be 100 in the cache");
            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p=>p.Age), "Incorrect Filter result");

        }

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _source.BatchUpdate(updater => updater.Clear());

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

            _source.BatchUpdate(updater => updater.AddOrUpdate(person));
            _source.BatchUpdate(updater => updater.Remove(key));

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

            _source.BatchUpdate(updater => updater.AddOrUpdate(newperson));
            _source.BatchUpdate(updater => updater.AddOrUpdate(updated));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds");
            Assert.AreEqual(1, _results.Messages[1].Updates, "Should be 1 update");
        }

        [Test]
        public void SameKeyChanges()
        {
            const string key = "Adult1";

            _source.BatchUpdate(updater =>
                               {
                                   updater.AddOrUpdate(new Person(key, 50));
                                   updater.AddOrUpdate(new Person(key, 52));
                                   updater.AddOrUpdate(new Person(key, 53));
                                   updater.Remove(key);
                               });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds");
            Assert.AreEqual(2, _results.Messages[0].Updates, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Removes, "Should be 1 remove");
        }

        [Test]
        public void UpdateNotMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 10);
            var updated = new Person(key, 11);

            _source.BatchUpdate(updater => updater.AddOrUpdate(newperson));
            _source.BatchUpdate(updater => updater.AddOrUpdate(updated));

            Assert.AreEqual(0, _results.Messages.Count, "Should be no updates");
            Assert.AreEqual(0, _results.Data.Count, "Should nothing cached");
        }
    }
}