using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class Transformer<TDestination, TSource, TKey> : AbstractTransformer<TDestination, TSource, TKey>
    {
        public Transformer(Action<Error<TSource, TKey>> exceptionCallback)
            : base(exceptionCallback)
        {

        }

        protected override IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, Optional<TransformResult>> factory)
        {
            var transformed = updates.Select(factory).SelectValues().ToArray();
            return ProcessUpdates(transformed);
        }

        protected override IChangeSet<TDestination, TKey> DoTransform(IEnumerable<KeyValuePair<TKey, TSource>> items, Func<KeyValuePair<TKey, TSource>, TransformResult> factory)
        {
            var transformed = items.Select(factory).ToArray();
            return ProcessUpdates(transformed);
        }
    }
}
