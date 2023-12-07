// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
using System.Reactive.Linq;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    internal sealed class PTransform<TDestination, TSource, TKey>(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory,
        ParallelisationOptions parallelisationOptions,
        Action<Error<TSource, TKey>>? exceptionCallback = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        public IObservable<IChangeSet<TDestination, TKey>> Run() =>
            Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
            {
                var cache = new ChangeAwareCache<TDestination, TKey>();
                var transformer = source.Select(changes => DoTransform(cache, changes));
                return transformer.NotEmpty().SubscribeSafe(observer);
            });

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "A collection initializer is not equivalent to a .ToArray() call for a ParallelQuery<T>. This change actually introduces a race-condition exception.")]
        private ChangeSet<TDestination, TKey> DoTransform(ChangeAwareCache<TDestination, TKey> cache, IChangeSet<TSource, TKey> changes)
        {
            var transformed = changes.ShouldParallelise(parallelisationOptions)
                ? changes.Parallelise(parallelisationOptions).Select(ToDestination).ToArray()
                : changes.Select(ToDestination).ToArray();

            return ProcessUpdates(cache, transformed);
        }

        private TransformResult ToDestination(Change<TSource, TKey> change)
        {
            try
            {
                if (change.Reason == ChangeReason.Add || change.Reason == ChangeReason.Update)
                {
                    var destination = transformFactory(change.Current, change.Previous, change.Key);
                    return new TransformResult(change, destination);
                }

                return new TransformResult(change);
            }
            catch (Exception ex)
            {
                // only handle errors if a handler has been specified
                if (exceptionCallback != null)
                {
                    return new TransformResult(change, ex);
                }

                throw;
            }
        }

        private ChangeSet<TDestination, TKey> ProcessUpdates(ChangeAwareCache<TDestination, TKey> cache, IEnumerable<TransformResult> transformedItems)
        {
            foreach (var result in transformedItems)
            {
                if (result.Success)
                {
                    switch (result.Change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Update:
#pragma warning disable CS8604 // Possible null reference argument.
                            cache.AddOrUpdate(result.Destination.ValueOrDefault(), result.Key);
#pragma warning restore CS8604 // Possible null reference argument.
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(result.Key);
                            break;

                        case ChangeReason.Refresh:
                            cache.Refresh(result.Key);
                            break;
                    }
                }
                else
                {
                    exceptionCallback?.Invoke(new Error<TSource, TKey>(result.Error, result.Change.Current, result.Change.Key));
                }
            }

            return cache.CaptureChanges();
        }

        private readonly struct TransformResult
        {
            public TransformResult(in Change<TSource, TKey> change, TDestination destination)
                : this()
            {
                Change = change;
                Destination = destination;
                Success = true;
                Key = change.Key;
            }

            public TransformResult(in Change<TSource, TKey> change)
                : this()
            {
                Change = change;
                Destination = Optional<TDestination>.None;
                Success = true;
                Key = change.Key;
            }

            public TransformResult(in Change<TSource, TKey> change, Exception error)
                : this()
            {
                Change = change;
                Error = error;
                Success = false;
                Key = change.Key;
            }

            public Change<TSource, TKey> Change { get; }

            public Exception? Error { get; }

            public bool Success { get; }

            public Optional<TDestination> Destination { get; }

            public TKey Key { get; }
        }
    }
}

#endif
