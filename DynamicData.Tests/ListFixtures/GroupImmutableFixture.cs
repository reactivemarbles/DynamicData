using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using DynamicData.Kernel;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class GroupImmutableFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<List.IGrouping<Person, int>> _results;
        private ISubject<Unit> _regrouper;
        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _regrouper = new Subject<Unit>();
            _results = _source.Connect().GroupWithImmutableState(p => p.Age, _regrouper).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }
        
        [Test]
        public void Add()
        {

            _source.Add(new Person("Person1", 20));
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 add");
            Assert.AreEqual(1, _results.Messages.First().Adds);
        }

        [Test]
        public void UpdatesArePermissible()
        {
            _source.Add(new Person("Person1", 20));
            _source.Add(new Person("Person2", 20));

            Assert.AreEqual(1, _results.Data.Count);//1 group
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Replaced);

            var group = _results.Data.Items.First();
            Assert.AreEqual(2, group.Count);
        }

        [Test]
        public void UpdateAnItemWillChangedThegroup()
        {
            var person1 = new Person("Person1", 20);
            _source.Add(person1);
            _source.Replace(person1, new Person("Person1", 21));

            Assert.AreEqual(1, _results.Data.Count);
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Adds);
            Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes);
            var group = _results.Data.Items.First();
            Assert.AreEqual(1, group.Count);

            Assert.AreEqual(21, group.Key);
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Person1", 20);
            _source.Add(person);
            _source.Remove(person);

            Assert.AreEqual(2, _results.Messages.Count);
            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void FiresManyValueForBatchOfDifferentAdds()
        {
            _source.Edit(updater =>
            {
                updater.Add(new Person("Person1", 20));
                updater.Add(new Person("Person2", 21));
                updater.Add(new Person("Person3", 22));
                updater.Add(new Person("Person4", 23));
            });

            Assert.AreEqual(4, _results.Data.Count);
            Assert.AreEqual(1, _results.Messages.Count);
            Assert.AreEqual(1, _results.Messages.First().Count);
            foreach (var update in _results.Messages.First())
            {
                Assert.AreEqual(ListChangeReason.AddRange, update.Reason);
            }
        }

        [Test]
        public void FiresOnlyOnceForABatchOfUniqueValues()
        {
            _source.Edit(updater =>
            {
                updater.Add(new Person("Person1", 20));
                updater.Add(new Person("Person2", 20));
                updater.Add(new Person("Person3", 20));
                updater.Add(new Person("Person4", 20));
            });

            Assert.AreEqual(1, _results.Messages.Count);
            Assert.AreEqual(1, _results.Messages.First().Adds);
            Assert.AreEqual(4, _results.Data.Items.First().Count);
        }

        [Test]
        public void ChanegMultipleGroups()
        {
            var initialPeople = Enumerable.Range(1, 10000)
                .Select(i => new Person("Person" + i, i % 10))
                .ToArray();

            _source.AddRange(initialPeople);

            initialPeople.GroupBy(p => p.Age)
                .ForEach(group =>
                {
                    var grp = _results.Data.Items.First(g=> g.Key.Equals(group.Key));
                    CollectionAssert.AreEquivalent(group.ToArray(), grp.Items);
                });

            _source.RemoveMany(initialPeople.Take(15));

            initialPeople.Skip(15).GroupBy(p => p.Age)
                .ForEach(group =>
                {
                    var list = _results.Data.Items.First(p => p.Key == group.Key);
                    CollectionAssert.AreEquivalent(group, list.Items);

                });

            Assert.AreEqual(2, _results.Messages.Count);
            Assert.AreEqual(10, _results.Messages.First().Adds);
            Assert.AreEqual(10, _results.Messages.Skip(1).First().Replaced);
        }

        [Test]
        public void Reevaluate()
        {
            var initialPeople = Enumerable.Range(1, 10)
                .Select(i => new Person("Person" + i, i % 2))
                .ToArray();

            _source.AddRange(initialPeople);
            Assert.AreEqual(1, _results.Messages.Count);

            //do an inline update
            foreach (var person in initialPeople)
                person.Age = person.Age + 1;

            //signal operators to evaluate again
            _regrouper.OnNext();

            initialPeople.GroupBy(p => p.Age)
                .ForEach(groupContainer =>
                {
                    
                    var grouping = _results.Data.Items.First(g=>g.Key == groupContainer.Key);
                    CollectionAssert.AreEquivalent(groupContainer, grouping.Items);

                });

            Assert.AreEqual(2, _results.Data.Count);
            Assert.AreEqual(2, _results.Messages.Count);

            var secondMessage = _results.Messages.Skip(1).First();
            Assert.AreEqual(1, secondMessage.Removes);
            Assert.AreEqual(1, secondMessage.Replaced);
            Assert.AreEqual(1, secondMessage.Adds);
        }
    }
}
