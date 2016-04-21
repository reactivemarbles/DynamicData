using System;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class ObservableWithValue<TObject, TValue>
    {
        private readonly TObject _item;
        private readonly IObservable<TValue> _source;
        private Optional<TValue> _latestValue = Optional<TValue>.None;

        public ObservableWithValue(TObject item, IObservable<TValue> source)
        {
            _item = item;
            _source = source.Do(value => _latestValue = value);
        }

        public TObject Item { get { return _item; } }

        public Optional<TValue> LatestValue { get { return _latestValue; } }

        public IObservable<TValue> Observable { get { return _source; } }
    }
}
