// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

internal sealed class ObservableWithValue<TObject, TValue>
    where TValue : notnull
{
    public ObservableWithValue(TObject item, IObservable<TValue> source)
    {
        Item = item;
        Observable = source.Do(value => LatestValue = value);
    }

    public TObject Item { get; }

    public ReactiveUI.Primitives.Optional<TValue> LatestValue { get; private set; } = ReactiveUI.Primitives.Optional<TValue>.None;

    public IObservable<TValue> Observable { get; }
}
