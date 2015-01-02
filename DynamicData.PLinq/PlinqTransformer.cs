using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Operators;

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

        protected override IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, TransformedItem> factory)
        {
            var transformed = updates.ShouldParallelise(_parallelisationOptions)
                ? updates.Parallelise(_parallelisationOptions).Select(factory).ToArray()
                : updates.Select(factory).ToArray();

            return ProcessUpdates(transformed);
        }
    }
}