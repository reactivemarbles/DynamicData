using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.PLinq
{
    internal class PlinqTransformer<TDestination, TSource, TKey> : AbstractTransformer<TDestination, TSource, TKey>
    {
        private readonly ParallelisationOptions _parallelisationOptions;

        public PlinqTransformer(ParallelisationOptions parallelisationOptions, Action<Error<TSource, TKey>> exceptionCallback)
            : base(exceptionCallback)
        {
            _parallelisationOptions = parallelisationOptions;
        }

        protected override IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, Optional<TransformResult>> factory)
        {
            var transformed = updates.ShouldParallelise(_parallelisationOptions)
                ? updates.Parallelise(_parallelisationOptions).Select(factory).SelectValues().ToArray()
                : updates.Select(factory).SelectValues().ToArray();

            return ProcessUpdates(transformed);
        }

        protected override IChangeSet<TDestination, TKey> DoTransform(IEnumerable<KeyValuePair<TKey, TSource>> items, Func<KeyValuePair<TKey, TSource>, TransformResult> factory)
        {
            var keyValuePairs = items.AsArray();

            var transformed = keyValuePairs.ShouldParallelise(_parallelisationOptions)
                ? keyValuePairs.Parallelise(_parallelisationOptions).Select(factory).ToArray()
                : keyValuePairs.Select(factory).ToArray();

            return ProcessUpdates(transformed);
        }
    }
}
