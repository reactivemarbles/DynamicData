using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class MergeManyWithKeyOverloadFixture: IDisposable
    {
        private class ObjectWithObservable
        {
            private readonly ISubject<bool> _changed = new Subject<bool>();
            private bool _value;

            public ObjectWithObservable(int id)
            {
                Id = id;
            }

            public void InvokeObservable(bool value)
            {
                _value = value;
                _changed.OnNext(value);
            }

            public void CompleteObservable()
            {
                _changed.OnCompleted();
            }

            public void FailObservable(Exception ex)
            {
                _changed.OnError(ex);
            }

            public IObservable<bool> Observable => _changed.AsObservable();

            public int Id { get; }
        }

        private readonly ISourceCache<ObjectWithObservable, int> _source;

        public  MergeManyWithKeyOverloadFixture()
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
            var invoked = false;

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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void SingleItemCompleteWillNotMergedStream()
        {
            var completed = false;
            var stream =
                _source.Connect()
                    .MergeMany((o, key) => o.Observable)
                    .Subscribe(
                        _ => {},
                        () => completed = true
                        );

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            item.InvokeObservable(true);
            item.CompleteObservable();

            stream.Dispose();

            completed.Should().BeFalse();
        }

        [Fact]
        public void SingleItemFailWillNotFailMergedStream()
        {
            var failed = false;
            var stream =
                _source.Connect()
                    .MergeMany((o, key) => o.Observable)
                    .Subscribe(
                        _ => { },
                        ex => failed = true
                    );

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            item.FailObservable(new Exception("Test exception"));

            stream.Dispose();

            failed.Should().BeFalse();
        }

    }
}
