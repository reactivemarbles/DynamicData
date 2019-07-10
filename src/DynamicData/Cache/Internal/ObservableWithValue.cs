// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ObservableWithValue<TObject, TValue>
    {
        private Optional<TValue> _latestValue = Optional<TValue>.None;

        public ObservableWithValue(TObject item, IObservable<TValue> source)
        {
            Item = item;
            Observable = source.Do(value => _latestValue = value);
        }

        public TObject Item { get; }

        public Optional<TValue> LatestValue => _latestValue;

        public IObservable<TValue> Observable { get; }
    }
}
