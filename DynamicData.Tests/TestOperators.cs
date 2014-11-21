using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace DynamicData.Tests
{

    public static class TestOperators

    {

        /// <summary>
        /// Aggregates the updates for testing only.  **** Make sure source is disposed in TestClean to dispose the aggregator
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IList<IChangeSet<TObject, TKey>> AggregateUpdates<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            var result = new List<IChangeSet<TObject, TKey>>();

            var scanner = source.Aggregate(result, (seed, next) =>
            {
                seed.Add(next);
                return seed;
            }).Subscribe(_=>{},()=>{Console.WriteLine("Complete");});

            return result;
        }

    }

}
