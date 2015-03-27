using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
	[TestFixture]
	public class SubscribeManyFixture
	{
		private class SubscribeableObject
		{
			public bool IsSubscribed { get; private set; }
			public int Id { get; private set; }

			public void Subscribe()
			{
				IsSubscribed = true;
			}

			public void UnSubscribe()
			{
				IsSubscribed = false;
			}

			public SubscribeableObject(int id)
			{
				Id = id;
			}

		}

		private ISourceList<SubscribeableObject> _source;
		private ChangeSetAggregator<SubscribeableObject> _results;

		[SetUp]
		public void Initialise()
		{
			_source = new SourceList<SubscribeableObject>();
			_results = new ChangeSetAggregator<SubscribeableObject>(
				_source.Connect().SubscribeMany(subscribeable =>
				{
					subscribeable.Subscribe();
					return Disposable.Create(subscribeable.UnSubscribe);
				}));

		}

		[TearDown]
		public void Cleanup()
		{
			_source.Dispose();
			_results.Dispose();
		}

		[Test]
		public void AddedItemWillbeSubscribed()
		{
			_source.Add(new SubscribeableObject(1));

			Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
			Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
			Assert.AreEqual(true, _results.Data.Items.First().IsSubscribed, "Should be subscribed");
		}

		[Test]
		public void RemoveIsUnsubscribed()
		{
			_source.Add(new SubscribeableObject(1));
			_source.RemoveAt(0);

			Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
			Assert.AreEqual(0, _results.Data.Count, "Should be 0 items in the cache");
			Assert.AreEqual(false, _results.Messages[1].First().Item.Current.IsSubscribed, "Should be be unsubscribed");
		}

		//[Test]
		//public void UpdateUnsubscribesPrevious()
		//{
		//	_source.Add(new SubscribeableObject(1));
		//	_source.BatchUpdate(updater => updater.AddOrUpdate(new SubscribeableObject(1)));

		//	Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
		//	Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
		//	Assert.AreEqual(true, _results.Messages[1].First().Current.IsSubscribed, "Current should be subscribed");
		//	Assert.AreEqual(false, _results.Messages[1].First().Previous.Value.IsSubscribed, "Previous should not be subscribed");
		//}

		[Test]
		public void EverythingIsUnsubscribedWhenStreamIsDisposed()
		{
			_source.AddRange(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i)));
			_source.Clear();

			Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");

			var items = _results.Messages[0].SelectMany(x => x.Range);

				Assert.IsTrue(items.All(d => !d.IsSubscribed));

		}
	}
}