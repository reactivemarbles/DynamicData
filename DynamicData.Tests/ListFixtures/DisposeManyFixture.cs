using System;
using System.Linq;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class DisposeManyFixture
    {
        private ISourceList<DisposableObject> _source;
        private ChangeSetAggregator<DisposableObject> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<DisposableObject>();
            _results = new ChangeSetAggregator<DisposableObject>(_source.Connect().DisposeMany());
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void AddWillNotCallDispose()
        {
            _source.Add(new DisposableObject(1));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(false, _results.Data.Items.First().IsDisposed, "Should not be disposed");
        }

        [Test]
        public void RemoveWillCallDispose()
        {
            _source.Add(new DisposableObject(1));
            _source.RemoveAt(0);

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be 0 items in the cache");
            Assert.AreEqual(true, _results.Messages[1].First().Item.Current.IsDisposed, "Should be disposed");
        }

        [Test]
        public void UpdateWillCallDispose()
        {
            _source.Add(new DisposableObject(1));
            _source.ReplaceAt(0, new DisposableObject(1));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
            Assert.AreEqual(false, _results.Messages[1].First().Item.Current.IsDisposed, "Current should not be disposed");
            Assert.AreEqual(true, _results.Messages[1].First().Item.Previous.Value.IsDisposed, "Previous should be disposed");
        }

        [Test]
        public void EverythingIsDisposedWhenStreamIsDisposed()
        {
            var toadd = Enumerable.Range(1, 10).Select(i => new DisposableObject(i));
            _source.AddRange(toadd);
            _source.Clear();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");

            var itemsCleared = _results.Messages[1].First().Range;
            Assert.IsTrue(itemsCleared.All(d => d.IsDisposed));
        }

        private class DisposableObject : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public int Id { get; private set; }

            public DisposableObject(int id)
            {
                Id = id;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
