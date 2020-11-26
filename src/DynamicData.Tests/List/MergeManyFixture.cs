using System;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List
{
    public class MergeManyFixture : IDisposable
    {
        private readonly ISourceList<ObjectWithObservable> _source;

        public MergeManyFixture()
        {
            _source = new SourceList<ObjectWithObservable>();
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void EverythingIsUnsubscribedWhenStreamIsDisposed()
        {
            bool invoked = false;
            var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.Add(item);

            stream.Dispose();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
        }

        /// <summary>
        /// Invocations the only when child is invoked.
        /// </summary>
        [Fact]
        public void InvocationOnlyWhenChildIsInvoked()
        {
            bool invoked = false;

            var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.Add(item);

            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeTrue();
            stream.Dispose();
        }

        [Fact]
        public void RemovedItemWillNotCauseInvocation()
        {
            bool invoked = false;
            var stream = _source.Connect().MergeMany(o => o.Observable).Subscribe(o => { invoked = true; });

            var item = new ObjectWithObservable(1);
            _source.Add(item);
            _source.Remove(item);
            invoked.Should().BeFalse();

            item.InvokeObservable(true);
            invoked.Should().BeFalse();
            stream.Dispose();
        }

        private class ObjectWithObservable
        {
            private readonly ISubject<bool> _changed = new Subject<bool>();

            private bool _value;

            public ObjectWithObservable(int id)
            {
                Id = id;
            }

            public int Id { get; }

            public IObservable<bool> Observable => _changed;

            public void InvokeObservable(bool value)
            {
                _value = value;
                _changed.OnNext(value);
            }
        }
    }
}