#region Usings

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

#endregion<TSource,TKey>

namespace DynamicData.Operators
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