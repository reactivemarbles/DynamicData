using System;
using System.Linq;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using IDisposable = System.IDisposable;

namespace DynamicData.Tests.Binding
{
	[TestFixture]
	public class BindFromObservableListFixture
	{
		private ObservableCollectionExtended<Person> _collection = new ObservableCollectionExtended<Person>();
		private SourceList<Person> _source;
		private IDisposable _binder;
		private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

		[SetUp]
		public void SetUp()
		{
			_collection = new ObservableCollectionExtended<Person>();
			_source = new SourceList<Person>();
			_binder = _source.Connect().Bind(_collection).Subscribe();

		}

		[TearDown]
		public void CleanUp()
		{
			_binder.Dispose();
			_source.Dispose();
		}

		[Test]
		public void AddToSourceAddsToDestination()
		{
			var person = new Person("Adult1", 50);
			_source.Add(person);

			Assert.AreEqual(1, _collection.Count, "Should be 1 item in the collection");
			Assert.AreEqual(person, _collection.First(), "Should be same person");
		}

		[Test]
		public void UpdateToSourceUpdatesTheDestination()
		{
			var person = new Person("Adult1", 50);
			var personUpdated = new Person("Adult1", 51);
			_source.Add(person);
			_source.Replace(person,personUpdated);

			Assert.AreEqual(1, _collection.Count, "Should be 1 item in the collection");
			Assert.AreEqual(personUpdated, _collection.First(), "Should be updated person");
		}


		[Test]
		public void RemoveSourceRemovesFromTheDestination()
		{
			var person = new Person("Adult1", 50);
			_source.Add(person);
			_source.Remove(person);

			Assert.AreEqual(0, _collection.Count, "Should be 1 item in the collection");
		}

		[Test]
		public void BatchAdd()
		{
			var people = _generator.Take(100).ToList();
			_source.AddRange(people);

			Assert.AreEqual(100, _collection.Count, "Should be 100 items in the collection");
			CollectionAssert.AreEquivalent(people, _collection, "Collections should be equivalent");
		}

		[Test]
		public void BatchRemove()
		{
			var people = _generator.Take(100).ToList();
			_source.AddRange(people);
			_source.Clear();
			Assert.AreEqual(0, _collection.Count, "Should be 100 items in the collection");
		}

	}
}