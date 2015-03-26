using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class MergeManyWithKeyOverloadFixture
    {
        private class ObjectWithObservable
        {
            private readonly int _id;
            private readonly ISubject<bool> _changed = new Subject<bool>();
            private bool _value;

            public ObjectWithObservable(int id)
            {
                _id = id;
            }

            public void InvokeObservable(bool value)
            {
                _value = value;
                _changed.OnNext(value);
            }

            public IObservable<bool> Observable => _changed.AsObservable();

	        public int Id => _id;
        }

        private ISourceCache<ObjectWithObservable, int> _source;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void InvocationOnlyWhenChildIsInvoked()
        {
            bool invoked = false;


            var stream = _source.Connect()
                .MergeMany((o, key) => o.Observable)
                .Subscribe(o =>
                           {
                               invoked = true;
                           });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            Assert.IsFalse(invoked, "Error. The operator should not have been invoked");

            item.InvokeObservable(true);
            Assert.IsTrue(invoked, "The observable should have notified");
            stream.Dispose();
        }

        [Test]
        public void RemovedItemWillNotCauseInvocation()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeMany((o,key) => o.Observable)
                .Subscribe(o =>
                           {
                               invoked = true;
                           });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);
            _source.Remove(item);
            Assert.IsFalse(invoked, "Error. The operator should not have been invoked");

            item.InvokeObservable(true);
            Assert.IsFalse(invoked, "The observable should not have notified as it is no longer in  the stream");
            stream.Dispose();
        }

        [Test]
        public void EverythingIsUnsubscribedWhenStreamIsDisposed()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeMany((o, key) => o.Observable)
                .Subscribe(o =>
                           {
                               invoked = true;
                           });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            stream.Dispose();

            item.InvokeObservable(true);
            Assert.IsFalse(invoked, "The stream has been disposed so there should be no notificiation");
        }
    }
}