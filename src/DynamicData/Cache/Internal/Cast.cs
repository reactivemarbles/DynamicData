// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class Cast<TSource, TKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TSource, TKey>> _source;
        private readonly Func<TSource, TDestination> _converter;

        public Cast(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public IObservable<IChangeSet<TDestination, TKey>> Run()
        {
            return _source.Select(changes =>
            {
                var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                                                                          change.Key,
                                                                          _converter(change.Current),
                                                                          change.Previous.Convert(_converter),
                                                                          change.CurrentIndex,
                                                                          change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
        }
    }

}
