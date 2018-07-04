using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class ToList<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        public ToList(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _source = source;
        }

        public IObservable<IList<TObject>> Run()
        {
            return _source
                .Scan(ImmutableList<TObject>.Empty, (state, changes) => state.Clone(changes));
        }

    }
}