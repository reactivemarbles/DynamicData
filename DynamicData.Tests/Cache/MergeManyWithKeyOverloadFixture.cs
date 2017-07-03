using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class MergeManyWithKeyOverloadFixture: IDisposable
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

        public void Dispose()
        {
            _source.Dispose();
        }

        [Test]
        public void InvocationOnlyWhenChildIsInvoked()
        {
            bool invoked = false;

            var stream = _source.Connect()
                                .MergeMany((o, key) => o.Observable)
                                .Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeTrue();
            stream.Dispose();
        }

        [Test]
        public void RemovedItemWillNotCauseInvocation()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeMany((o, key) => o.Observable)
                .Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);
            _source.Remove(item);
            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
            stream.Dispose();
        }

        [Test]
        public void EverythingIsUnsubscribedWhenStreamIsDisposed()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeMany((o, key) => o.Observable)
                .Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            stream.Dispose();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
        }
    }
}
