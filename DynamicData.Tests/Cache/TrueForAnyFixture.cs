using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Xunit;


namespace DynamicData.Tests.Cache
{
    
    public class TrueForAnyFixture: IDisposable
    {
        private readonly ISourceCache<ObjectWithObservable, int> _source;
        private readonly IObservable<bool> _observable;

        public  TrueForAnyFixture()
        {
            _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
            _observable = _source.Connect().TrueForAny(o => o.Observable.StartWith(o.Value), o => o == true);
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void InitialItemReturnsFalseWhenObservaleHasNoValue()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result => { valuereturned = result; });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            valuereturned.HasValue.Should().BeTrue();
            valuereturned.Value.Should().Be(false, "The intial value should be false");

            subscribed.Dispose();
        }

        [Fact]
        public void InlineObservableChangeProducesResult()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result => { valuereturned = result; });

            var item = new ObjectWithObservable(1);
            item.InvokeObservable(true);
            _source.AddOrUpdate(item);

            valuereturned.Value.Should().Be(true, "Value should be true");
            subscribed.Dispose();
        }

        [Fact]
        public void MultipleValuesReturnTrue()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result => { valuereturned = result; });

            var item1 = new ObjectWithObservable(1);
            var item2 = new ObjectWithObservable(2);
            var item3 = new ObjectWithObservable(3);
            _source.AddOrUpdate(item1);
            _source.AddOrUpdate(item2);
            _source.AddOrUpdate(item3);
            valuereturned.Value.Should().Be(false, "Value should be false");

            item1.InvokeObservable(true);
            valuereturned.Value.Should().Be(true, "Value should be true");
            subscribed.Dispose();
        }

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

            public IObservable<bool> Observable { get { return _changed; } }

            public bool Value { get { return _value; } }

            public int Id { get { return _id; } }
        }
    }
}
