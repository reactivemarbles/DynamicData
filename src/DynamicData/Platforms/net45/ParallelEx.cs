// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive.PLinq
#else
namespace DynamicData.PLinq
#endif
{
/// <summary>
/// Parallelisation extensions for DynamicData.
///  </summary>
internal static class ParallelEx
    {
        /// <summary>
        /// Executes the Parallelise operation.
        /// </summary>
        /// <typeparam name="TObject">The type of the TObject value.</typeparam>
        /// <typeparam name="TKey">The type of the TKey value.</typeparam>
        /// <param name="source">The source value.</param>
        /// <param name="option">The option value.</param>
        /// <returns>The result of the operation.</returns>
        internal static ParallelQuery<Change<TObject, TKey>> Parallelise<TObject, TKey>(this IChangeSet<TObject, TKey> source, ParallelisationOptions option)
            where TObject : notnull
            where TKey : notnull => option.Type switch
            {
                ParallelType.Parallelise => source.AsParallel(),
                ParallelType.Ordered => source.AsParallel().AsOrdered(),
                _ => throw new ArgumentException("Should not parallelise!  Call ShouldParallelise() first"),
            };

        /// <summary>
        /// Executes the Parallelise operation.
        /// </summary>
        /// <typeparam name="TObject">The type of the TObject value.</typeparam>
        /// <typeparam name="TKey">The type of the TKey value.</typeparam>
        /// <param name="source">The source value.</param>
        /// <param name="option">The option value.</param>
        /// <returns>The result of the operation.</returns>
        internal static ParallelQuery<KeyValuePair<TKey, TObject>> Parallelise<TObject, TKey>(this IEnumerable<KeyValuePair<TKey, TObject>> source, ParallelisationOptions option)
            where TKey : notnull => option.Type switch
            {
                ParallelType.Parallelise => source.AsParallel(),
                ParallelType.Ordered => source.AsParallel().AsOrdered(),
                _ => throw new ArgumentException("Should not parallelise!  Call ShouldParallelise() first"),
            };

        /// <summary>
        /// Executes the Parallelise operation.
        /// </summary>
        /// <typeparam name="T">The type of the T value.</typeparam>
        /// <param name="source">The source value.</param>
        /// <param name="option">The option value.</param>
        /// <returns>The result of the operation.</returns>
        internal static IEnumerable<T> Parallelise<T>(this IEnumerable<T> source, ParallelisationOptions option)
        {
            switch (option.Type)
            {
                case ParallelType.Parallelise:
                    {
                        var parallelise = source as T[] ?? source.ToArray();
                        if (option.Threshold >= 0 && parallelise.Length >= option.Threshold)
                        {
                            return parallelise.AsParallel();
                        }

                        return parallelise;
                    }

                case ParallelType.Ordered:
                    {
                        var parallelise = source as T[] ?? source.ToArray();
                        if (option.Threshold >= 0 && parallelise.Length >= option.Threshold)
                        {
                            return parallelise.AsParallel().AsOrdered();
                        }

                        return parallelise;
                    }

                default:
                    return source;
            }
        }

        /// <summary>
        /// Executes the ShouldParallelise operation.
        /// </summary>
        /// <typeparam name="TObject">The type of the TObject value.</typeparam>
        /// <typeparam name="TKey">The type of the TKey value.</typeparam>
        /// <param name="source">The source value.</param>
        /// <param name="option">The option value.</param>
        /// <returns>The result of the operation.</returns>
        internal static bool ShouldParallelise<TObject, TKey>(this IChangeSet<TObject, TKey> source, ParallelisationOptions option)
            where TObject : notnull
            where TKey : notnull => (option.Type == ParallelType.Parallelise || option.Type == ParallelType.Ordered) && (option.Threshold >= 0 && source.Count >= option.Threshold);

        /// <summary>
        /// Executes the ShouldParallelise operation.
        /// </summary>
        /// <typeparam name="TObject">The type of the TObject value.</typeparam>
        /// <typeparam name="TKey">The type of the TKey value.</typeparam>
        /// <param name="source">The source value.</param>
        /// <param name="option">The option value.</param>
        /// <returns>The result of the operation.</returns>
        internal static bool ShouldParallelise<TObject, TKey>(this IEnumerable<KeyValuePair<TKey, TObject>> source, ParallelisationOptions option)
            where TKey : notnull => (option.Type == ParallelType.Parallelise || option.Type == ParallelType.Ordered) && (option.Threshold >= 0 && source.Skip(option.Threshold).Any());
    }
}

#endif
