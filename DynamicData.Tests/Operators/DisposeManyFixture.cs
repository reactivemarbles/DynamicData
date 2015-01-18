using System;
using System.Linq;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class DisposeManyFixture
    {
        private class DisposableObject: IDisposable
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

        private ISourceCache<DisposableObject, int> _source;
      
        private ChangeSetAggregator<DisposableObject, int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<DisposableObject, int>(p=>p.Id);
            _results = new ChangeSetAggregator<DisposableObject, int>(_source.Connect().DisposeMany());
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
            _source.AddOrUpdate(new DisposableObject(1));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(false, _results.Data.Items.First().IsDisposed, "Should not be disposed");
        }


        [Test]
        public void RemoveWillCallDispose()
        {
            _source.AddOrUpdate(new DisposableObject(1));
            _source.Remove(1);
           
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(0, _results.Data.Count, "Should be 0 items in the cache");
            Assert.AreEqual(true, _results.Messages[1].First().Current.IsDisposed, "Should be disposed");
        }

        [Test]
        public void UpdateWillCallDispose()
        {
            _source.AddOrUpdate(new DisposableObject(1));
            _source.AddOrUpdate(new DisposableObject(1));

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 items in the cache");
            Assert.AreEqual(false, _results.Messages[1].First().Current.IsDisposed, "Current should not be disposed");
            Assert.AreEqual(true, _results.Messages[1].First().Previous.Value.IsDisposed, "Previous should be disposed");
        }

        [Test]
        public void EverythingIsDisposedWhenStreamIsDisposed()
        {
            _source.AddOrUpdate(Enumerable.Range(1,10).Select(i=>new DisposableObject(i)));
            _source.Clear();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.IsTrue(_results.Messages[1].All(d=>d.Current.IsDisposed));

        }
    }
}