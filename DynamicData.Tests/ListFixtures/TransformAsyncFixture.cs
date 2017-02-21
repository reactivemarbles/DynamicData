using System;
using System.Linq;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class TransformAsyncFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<PersonWithGender> _results;

        private readonly Func<Person, Task<PersonWithGender>> _transformFactory = p =>
        {
            string gender = p.Age % 2 == 0 ? "M" : "F";
            var transformed = new PersonWithGender(p, gender);
            return Task.FromResult(transformed);
        };

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = new ChangeSetAggregator<PersonWithGender>(_source.Connect().TransformAsync(_transformFactory));
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public async Task Add()
        {
            var person = new Person("Adult1", 50);
            _source.Add(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");

            var transformed = await _transformFactory(person);
            Assert.AreEqual(transformed, _results.Data.Items.First(), "Should be same person");
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
        public void Update()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);

            _source.Add(newperson);
            _source.Add(updated);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds");
            Assert.AreEqual(0, _results.Messages[0].Replaced, "Should be 1 update");
        }

        [Test]
        public async Task BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            _source.AddRange(people);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(100, _results.Messages[0].Adds, "Should return 100 adds");

            var tasks = people.Select(_transformFactory);
            var result = await Task.WhenAll(tasks);

            var transformed = result.OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(transformed, _results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
        }

        [Test]
        public void SameKeyChanges()
        {
            var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

            _source.AddRange(people);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(10, _results.Messages[0].Adds, "Should return 10 adds");
            Assert.AreEqual(10, _results.Data.Count, "Should result in 10 records");
        }

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

            _source.AddRange(people);
            _source.Clear();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(100, _results.Messages[0].Adds, "Should be 80 addes");
            Assert.AreEqual(100, _results.Messages[1].Removes, "Should be 80 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");
        }
    }
}