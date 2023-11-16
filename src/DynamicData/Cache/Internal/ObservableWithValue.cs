// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class ObservableWithValue<TObject, TValue>
    where TValue : notnull
{
    public ObservableWithValue(TObject item, IObservable<TValue> source)
    {
        Item = item;
        Observable = source.Do(value => LatestValue = value);
    }

    public TObject Item { get; }

    public Optional<TValue> LatestValue { get; private set; } = Optional<TValue>.None;

    public IObservable<TValue> Observable { get; }
}
