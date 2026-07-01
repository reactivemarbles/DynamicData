// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformImmutable class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class TransformImmutable<TDestination, TSource, TKey>
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TSource, TKey>> _source;

    /// <summary>
    /// The _transformFactory field.
    /// </summary>
    private readonly Func<TSource, TDestination> _transformFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformImmutable{TDestination, TSource, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    public TransformImmutable(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> transformFactory)
    {
        _source = source;
        _transformFactory = transformFactory;
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run()
        => Observable.Create<IChangeSet<TDestination, TKey>>(observer => _source
            .SubscribeSafe(Observer.Create<IChangeSet<TSource, TKey>>(
                onNext: upstreamChanges =>
                {
                    var downstreamChanges = new ChangeSet<TDestination, TKey>(capacity: upstreamChanges.Count);

                    try
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            downstreamChanges.Add(new(
                                reason: change.Reason,
                                key: change.Key,
                                current: _transformFactory.Invoke(change.Current),
                                previous: change.Previous.HasValue
                                    ? _transformFactory.Invoke(change.Previous.Value)
                                    : ReactiveUI.Primitives.Optional<TDestination>.None,
                                currentIndex: change.CurrentIndex,
                                previousIndex: change.PreviousIndex));
                        }
                    }

                    // We're invoking "untrusted" consumer code, (I.E. _transformFactory), so catch and propagate any errors
                    catch (Exception error)
                    {
                        observer.OnError(error);
                    }

                    observer.OnNext(downstreamChanges);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted)));
}
