using System;
using System.Linq;
using DynamicData.ReactiveUI.Tests.Domain;
using DynamicData.Tests;
using ReactiveUI;
using Xunit;

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
	
	public class ObservableCollectionToObservableChangeSetWithoutINdexFixture:IDisposable
	{
		private readonly ReactiveList<Person> _collection;
		private readonly ChangeSetAggregator<Person> _results;
		private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        
		public  ObservableCollectionToObservableChangeSetWithoutINdexFixture()
		{
			_collection = new ReactiveList<Person>();
			_results = _collection.ToObservableChangeSet().AsAggregator();
		}

		public void Dispose()
		{
			_results.Dispose();
		}

		[Fact]
		public void AddInvokesAnAddChange()
		{
			var person = new Person("Adult1", 50);
			_collection.Add(person);

			//Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
			//Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
			//Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
		}

		[Fact]
		public void RemoveGetsRemovedFromDestination()
		{
			var person = new Person("Adult1", 50);
			_collection.Add(person);
			_collection.Remove(person);

			//Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
			//Assert.AreEqual(0, _results.Data.Count, "Should be nothing in the cache");
			//Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
			//Assert.AreEqual(1, _results.Messages.Skip(1).First().Removes, "First message should be a remove");
		}

		[Fact]
		public void DuplicatesAreAllowed()
		{
			//NB: the following is a replace bcause the hash code of user is calculated from the user name
			_collection.Add(new Person("Adult1", 50));
			_collection.Add(new Person("Adult1", 51));

			//Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
			//Assert.AreEqual(2, _results.Data.Count, "Should be 1 item in the cache");
			//Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
			//Assert.AreEqual(1, _results.Messages.Skip(1).First().Adds, "First message should be an add");
		}

		[Fact]
		public void Replace()
		{
			//NB: the following is a replace because the hash code of user is calculated from the user name
			var person = new Person("Adult1", 50);
			var replaced = new Person("Adult1", 50);
			_collection.Add(person);
			_collection.Replace(person, replaced);

			//Assert.AreEqual(2, _results.Messages.Count, "Should be 1 updates");
			//Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
			//Assert.AreEqual(1, _results.Messages.First().Adds, "First message should be an add");
			//Assert.AreEqual(1, _results.Messages.Skip(1).First().Replaced, "First message should be an update");
		}

		[Fact]
		public void ResetFiresClearsAndAdds()
		{
			var people = _generator.Take(10);
			_collection.AddRange(people);
			//Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");

			_collection.Reset();
			//Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
			//Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");

			var update11 = _results.Messages[1];
			//Assert.AreEqual(10, update11.Removes, "Should be 10 removes");
			//Assert.AreEqual(10, update11.Adds, "Should be 10 adds");
			//Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
		}

	}
}