

using System;
using System.Linq;
using DynamicData.Operators;


namespace DynamicData.Internal
{
    internal class Transformer<TDestination, TSource, TKey> : AbstractTransformer<TDestination, TSource, TKey>
    {

        public Transformer(Action<Error<TSource, TKey>> exceptionCallback)
            : base(exceptionCallback)
        {
        }

        protected override IChangeSet<TDestination, TKey> DoTransform(IChangeSet<TSource, TKey> updates, Func<Change<TSource, TKey>, TransformedItem> factory)
        {
            var transformed = updates.Select(factory).ToArray();

            return ProcessUpdates(transformed);
        }

    }
}