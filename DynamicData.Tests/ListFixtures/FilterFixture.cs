using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixture
{
    [TestFixture]
    public class FilterFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        
        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<Person>();
            _results = _source.Connect(p => p.Age > 20).AsAggregator();
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
            _source.Edit(list=>list.Add(person));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddNotMatched()
        {
            var person = new Person("Adult1", 10);
			_source.Edit(list => list.Add(person));

			Assert.AreEqual(0, _results.Messages.Count, "Should have no item updates");
            Assert.AreEqual(0, _results.Data.Count, "Cache should have no items");
        }

        [Test]
        public void AddNotMatchedAndUpdateMatched()
        {
            const string key = "Adult1";
            var notmatched = new Person(key, 19);
            var matched = new Person(key, 21);
            
            _source.Edit(list =>
                    {
						list.Add(notmatched);
						list.Add(matched);
                    });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(matched, _results.Messages[0].First().Current, "Should be same person");
            Assert.AreEqual(matched, _results.Data.Items.First(), "Should be same person");
        }
        
        [Test]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
			_source.Edit(list => list.Remove(new Person("anyone",1)));
			Assert.AreEqual(0, _results.Messages.Count, "Should be 0 updates");
        }
        
        [Test]
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            _source.Edit(list=>list.Add(people));
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(80, _results.Messages[0].Adds, "Should return 80 adds");

            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
        }
        
        [Test]
        public void BatchRemoves()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
			_source.Edit(list => list.Add(people));
			_source.Edit(list => list.Remove(people));

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
				_source.Edit(list => list.Add(person1));
			}

            Assert.AreEqual(80, _results.Messages.Count, "Should be 80 updates");
            Assert.AreEqual(80, _results.Data.Count, "Should be 100 in the cache");
            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            CollectionAssert.AreEqual(filtered, _results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
  
        }

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
			_source.Edit(list => list.Add(people));
			_source.Edit(list=>list.Clear());

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

			_source.Edit(list => list.Add(person));
			_source.Edit(list => list.Remove(person));

			Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 80 addes");
            Assert.AreEqual(1, _results.Messages[1].Removes, "Should be 80 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");
        }

   //     [Test]
   //     public void UpdateMatched()

   //     {
   //         const string key = "Adult1";
   //         var newperson = new Person(key, 50);
   //         var updated = new Person(key, 51);

			//_source.Edit(list => list.Add(newperson));
			//_source.AddOrUpdate(updated);

   //         Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
   //         Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds");
   //         Assert.AreEqual(1, _results.Messages[1].Updates, "Should be 1 update");
   //     }

        [Test]
        public void SameKeyChanges()
        {
            const string key = "Adult1";

	        var toaddandremove = new Person(key, 53);
            _source.Edit(updater =>
                               {
                                   updater.Add(new Person(key, 50));
                                   updater.Add(new Person(key, 52));
                                   updater.Add(toaddandremove);
                                   updater.Remove(toaddandremove);
                               });

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(3, _results.Messages[0].Adds, "Should be 3 adds");
            Assert.AreEqual(0, _results.Messages[0].Updates, "Should be 0 updates");
            Assert.AreEqual(1, _results.Messages[0].Removes, "Should be 1 remove");
        }

        [Test]
        public void UpdateNotMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 10);
            var updated = new Person(key, 11);

			_source.Edit(list => list.Add(newperson));
			_source.Edit(list => list.Add(updated));


			Assert.AreEqual(0, _results.Messages.Count, "Should be no updates");
            Assert.AreEqual(0, _results.Data.Count, "Should nothing cached");
        }
    }
}