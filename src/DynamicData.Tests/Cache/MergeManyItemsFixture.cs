using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class MergeManyItemsFixture: IDisposable
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

        private readonly ISourceCache<ObjectWithObservable, int> _source;

        public  MergeManyItemsFixture()
        {
            _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void InvocationOnlyWhenChildIsInvoked()
        {
            bool invoked = false;

            var stream = _source.Connect()
                                .MergeManyItems(o => o.Observable)
                                .Subscribe(o =>
                                {
                                    invoked = true;
                                    (o.Item.Id == 1).Should().BeTrue();
                });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeTrue();
            stream.Dispose();
        }

        [Fact]
        public void RemovedItemWillNotCauseInvocation()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeManyItems(o => o.Observable)
                .Subscribe(o =>
                {
                    invoked = true;
                    (o.Item.Id == 1).Should().BeTrue();
                });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);
            _source.Remove(item);
            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
            stream.Dispose();
        }

        [Fact]
        public void EverythingIsUnsubscribedWhenStreamIsDisposed()
        {
            bool invoked = false;
            var stream = _source.Connect()
                .MergeManyItems(o => o.Observable)
                .Subscribe(o =>
                {
                    invoked = true;
                    (o.Item.Id == 1).Should().BeTrue();
                });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            stream.Dispose();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
        }
    }
}
