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
/// Provides members for the PTransform class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformFactory">The transformFactory value.</param>
/// <param name="parallelisationOptions">The parallelisationOptions value.</param>
/// <param name="exceptionCallback">The exceptionCallback value.</param>
internal sealed class PTransform<TDestination, TSource, TKey>(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, TDestination> transformFactory,
        ParallelisationOptions parallelisationOptions,
        Action<Error<TSource, TKey>>? exceptionCallback = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        /// <summary>
        /// Executes the Run operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IObservable<IChangeSet<TDestination, TKey>> Run() =>
            Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
            {
                var cache = new ChangeAwareCache<TDestination, TKey>();
                var transformer = source.Select(changes => DoTransform(cache, changes));
                return transformer.NotEmpty().SubscribeSafe(observer);
            });

        /// <summary>
        /// Executes the DoTransform operation.
        /// </summary>
        /// <param name="cache">The cache value.</param>
        /// <param name="changes">The changes value.</param>
        /// <returns>The result of the operation.</returns>
        [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "A collection initializer is not equivalent to a .ToArray() call for a ParallelQuery<T>. This change actually introduces a race-condition exception.")]
        private ChangeSet<TDestination, TKey> DoTransform(ChangeAwareCache<TDestination, TKey> cache, IChangeSet<TSource, TKey> changes)
        {
            var transformed = changes.ShouldParallelise(parallelisationOptions)
                ? changes.Parallelise(parallelisationOptions).Select(ToDestination).ToArray()
                : changes.Select(ToDestination).ToArray();

            return ProcessUpdates(cache, transformed);
        }

        /// <summary>
        /// Executes the ToDestination operation.
        /// </summary>
        /// <param name="change">The change value.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the ProcessUpdates operation.
        /// </summary>
        /// <param name="cache">The cache value.</param>
        /// <param name="transformedItems">The transformedItems value.</param>
        /// <returns>The result of the operation.</returns>
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

/// <summary>
/// Represents the TransformResult value.
/// </summary>
private readonly struct TransformResult
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TransformResult"/> struct.
            /// </summary>
            /// <param name="change">The change value.</param>
            /// <param name="destination">The destination value.</param>
            public TransformResult(in Change<TSource, TKey> change, TDestination destination)
                : this()
            {
                Change = change;
                Destination = destination;
                Success = true;
                Key = change.Key;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="TransformResult"/> struct.
            /// </summary>
            /// <param name="change">The change value.</param>
            public TransformResult(in Change<TSource, TKey> change)
                : this()
            {
                Change = change;
                Destination = ReactiveUI.Primitives.Optional<TDestination>.None;
                Success = true;
                Key = change.Key;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="TransformResult"/> struct.
            /// </summary>
            /// <param name="change">The change value.</param>
            /// <param name="error">The error value.</param>
            public TransformResult(in Change<TSource, TKey> change, Exception error)
                : this()
            {
                Change = change;
                Error = error;
                Success = false;
                Key = change.Key;
            }

            /// <summary>
            /// Gets the Change value.
            /// </summary>
            public Change<TSource, TKey> Change { get; }

            /// <summary>
            /// Gets the Error value.
            /// </summary>
            public Exception? Error { get; }

            /// <summary>
            /// Gets the Success value.
            /// </summary>
            public bool Success { get; }

            /// <summary>
            /// Gets the Destination value.
            /// </summary>
            public ReactiveUI.Primitives.Optional<TDestination> Destination { get; }

            /// <summary>
            /// Gets the Key value.
            /// </summary>
            public TKey Key { get; }
        }
    }
}

#endif
