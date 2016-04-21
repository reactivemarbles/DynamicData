using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    [TestFixture]
    public class ObservableCollectionToObservableChangeSetFixture
    {
        private TestObservableCollection<Person> _collection;
        private ChangeSetAggregator<Person, string> _results;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void SetUp()
        {
            _collection = new TestObservableCollection<Person>();
            _results = _collection.ToObservableChangeSet(p => p.Name).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _results.Dispose();
        }

        [Test]
        public void AddInvokesAnAddChange()
        {
            var person = new Person("Adult1", 50);
            _collection.Add(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void RemoveGetsRemovedFromDestination()
        {
            var person = new Person("Adult1", 50);
            _collection.Add(person);
            _collection.Remove(person);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing in the cache");
            Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes, "First message should be a remove");
        }

        [Test]
        public void ReplacedItemFiresAReplace()
        {
            //NB: the following is a replace bcause the hash code of user is calculated from the user name
            _collection.Add(new Person("Adult1", 50));
            _collection.Add(new Person("Adult1", 51));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Updates, "First message should be an update");
        }

        [Test]
        public void ResetFiresClearsAndAdds()
        {
            var people = _generator.Take(10);
            people.ForEach(_collection.Add);
            Assert.AreEqual(10, _results.Messages.Count, "Should be 10 updates");

            _collection.Reset();
            Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
            Assert.AreEqual(11, _results.Messages.Count, "Should be 11 updates");

            var update11 = _results.Messages[10];
            Assert.AreEqual(10, update11.Removes, "Should be 10 removes");
            Assert.AreEqual(10, update11.Adds, "Should be 10 adds");
            Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
        }

        private class TestObservableCollection<T> : ObservableCollection<T>
        {
            public void Reset()
            {
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
