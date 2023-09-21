// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// Parallelisation extensions for DynamicData.
    ///  </summary>
    internal static class ParallelEx
    {
        internal static ParallelQuery<Change<TObject, TKey>> Parallelise<TObject, TKey>(this IChangeSet<TObject, TKey> source, ParallelisationOptions option)
            where TObject : notnull
            where TKey : notnull
        {
            switch (option.Type)
            {
                case ParallelType.Parallelise:
                    return source.AsParallel();

                case ParallelType.Ordered:
                    return source.AsParallel().AsOrdered();

                default:
                    throw new ArgumentException("Should not parallelise!  Call ShouldParallelise() first");
            }
        }

        internal static ParallelQuery<KeyValuePair<TKey, TObject>> Parallelise<TObject, TKey>(this IEnumerable<KeyValuePair<TKey, TObject>> source, ParallelisationOptions option)
            where TKey : notnull
        {
            switch (option.Type)
            {
                case ParallelType.Parallelise:
                    return source.AsParallel();

                case ParallelType.Ordered:
                    return source.AsParallel().AsOrdered();

                default:
                    throw new ArgumentException("Should not parallelise!  Call ShouldParallelise() first");
            }
        }

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

        internal static bool ShouldParallelise<TObject, TKey>(this IChangeSet<TObject, TKey> source, ParallelisationOptions option)
            where TObject : notnull
            where TKey : notnull
        {
            return (option.Type == ParallelType.Parallelise || option.Type == ParallelType.Ordered) && (option.Threshold >= 0 && source.Count >= option.Threshold);
        }

        internal static bool ShouldParallelise<TObject, TKey>(this IEnumerable<KeyValuePair<TKey, TObject>> source, ParallelisationOptions option)
            where TKey : notnull
        {
            return (option.Type == ParallelType.Parallelise || option.Type == ParallelType.Ordered) && (option.Threshold >= 0 && source.Skip(option.Threshold).Any());
        }
    }
}

#endif
