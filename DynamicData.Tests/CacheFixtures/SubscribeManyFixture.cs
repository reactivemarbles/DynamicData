
using System.Linq;
using System.Reactive.Disposables;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
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
        
        private ISourceCache<SubscribeableObject, int> _source;
        private ChangeSetAggregator<SubscribeableObject, int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<SubscribeableObject, int>(p=>p.Id);
            _results = new ChangeSetAggregator<SubscribeableObject, int>(
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
            _source.BatchUpdate(updater => updater.AddOrUpdate(new SubscribeableObject(1)));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(true, _results.Data.Items.First().IsSubscribed, "Should be subscribed");
        }

        [Test]
        public void RemoveIsUnsubscribed()
        {
            _source.BatchUpdate(updater => updater.AddOrUpdate(new SubscribeableObject(1)));
            _source.BatchUpdate(updater => updater.Remove(1));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be 0 items in the cache");
            Assert.AreEqual(false, _results.Messages[1].First().Current.IsSubscribed, "Should be be unsubscribed");
        }

        [Test]
        public void UpdateUnsubscribesPrevious()
        {
            _source.BatchUpdate(updater => updater.AddOrUpdate(new SubscribeableObject(1)));
            _source.BatchUpdate(updater => updater.AddOrUpdate(new SubscribeableObject(1)));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
            Assert.AreEqual(true, _results.Messages[1].First().Current.IsSubscribed, "Current should be subscribed");
            Assert.AreEqual(false, _results.Messages[1].First().Previous.Value.IsSubscribed, "Previous should not be subscribed");
        }

        [Test]
        public void EverythingIsUnsubscribedWhenStreamIsDisposed()
        {
            _source.BatchUpdate(updater => updater.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new SubscribeableObject(i))));
            _source.BatchUpdate(updater => updater.Clear());

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.IsTrue(_results.Messages[1].All(d => !d.Current.IsSubscribed));

        }
    }
}
