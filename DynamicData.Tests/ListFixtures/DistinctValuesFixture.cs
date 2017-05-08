using DynamicData.Tests.Domain;
using NUnit.Framework;
using System.Linq;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class DistinctValuesFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void FiresAddWhenaNewItemIsAdded()
        {
            _source.Add(new Person("Person1", 20));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void FiresBatchResultOnce()
        {
            _source.Edit(list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person2", 21));
                list.Add(new Person("Person3", 22));
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(3, _results.Data.Count, "Should be 3 items in the cache");

            CollectionAssert.AreEquivalent(new[] { 20, 21, 22 }, _results.Data.Items);
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void DuplicatedResultsResultInNoAdditionalMessage()
        {
            _source.Edit(list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
            });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 update message");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
            Assert.AreEqual(20, _results.Data.Items.First(), "Should 20");
        }

        [Test]
        public void RemovingAnItemRemovesTheDistinct()
        {
            var person = new Person("Person1", 20);

            _source.Add(person);
            _source.Remove(person);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 update message");
            Assert.AreEqual(0, _results.Data.Count, "Should be 1 items in the cache");

            Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes, "Second messsage should be a remove");
        }

        [Test]
        public void Replacing()
        {
            var person = new Person("A", 20);
            var replaceWith = new Person("A", 21);

            _source.Add(person);
            _source.Replace(person, replaceWith);
            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 update message");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");

            Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
            Assert.AreEqual(2, _results.Messages.Skip(1).First().Count, "Second messsage should be an add an a remove");
        }
    }
}
