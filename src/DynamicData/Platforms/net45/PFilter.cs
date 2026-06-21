// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive.PLinq
#else
namespace DynamicData.PLinq
#endif
{
/// <summary>
/// Provides members for the PFilter class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="filter">The filter value.</param>
/// <param name="parallelisationOptions">The parallelisationOptions value.</param>
internal sealed class PFilter<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        where TObject : notnull
        where TKey : notnull
    {
        /// <summary>
        /// Executes the Run operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var filterer = new PLinqFilteredUpdater(filter, parallelisationOptions);
                        return source.Select(filterer.Update).NotEmpty().SubscribeSafe(observer);
                    });

/// <summary>
/// Provides members for the PLinqFilteredUpdater class.
/// </summary>
/// <param name="filter">The filter value.</param>
/// <param name="parallelisationOptions">The parallelisationOptions value.</param>
private sealed class PLinqFilteredUpdater(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions) : AbstractFilter<TObject, TKey>(new ChangeAwareCache<TObject, TKey>(), filter)
        {
            /// <summary>
            /// The _parallelisationOptions field.
            /// </summary>
            private readonly ParallelisationOptions _parallelisationOptions = parallelisationOptions;

            /// <summary>
            /// Executes the GetChangesWithFilter operation.
            /// </summary>
            /// <param name="updates">The updates value.</param>
            /// <returns>The result of the operation.</returns>
            protected override IEnumerable<UpdateWithFilter> GetChangesWithFilter(ChangeSet<TObject, TKey> updates)
            {
                if (updates.ShouldParallelise(_parallelisationOptions))
                {
                    return updates.Parallelise(_parallelisationOptions).Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
                }

                return updates.Select(u => new UpdateWithFilter(Filter(u.Current), u)).ToArray();
            }

            /// <summary>
            /// Executes the Refresh operation.
            /// </summary>
            /// <param name="items">The items value.</param>
            /// <param name="factory">The factory value.</param>
            /// <returns>The result of the operation.</returns>
            protected override IEnumerable<Change<TObject, TKey>> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, ReactiveUI.Primitives.Optional<Change<TObject, TKey>>> factory)
            {
                var keyValuePairs = items as KeyValuePair<TKey, TObject>[] ?? items.ToArray();

                return keyValuePairs.ShouldParallelise(_parallelisationOptions) ? keyValuePairs.Parallelise(_parallelisationOptions).Select(factory).SelectValues() : keyValuePairs.Select(factory).SelectValues();
            }
        }
    }
}

#endif
