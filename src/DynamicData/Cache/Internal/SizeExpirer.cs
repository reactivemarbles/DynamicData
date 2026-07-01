// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the SizeExpirer class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class SizeExpirer<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _size field.
    /// </summary>
    private readonly int _size;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="SizeExpirer{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="size">The size value.</param>
    public SizeExpirer(IObservable<IChangeSet<TObject, TKey>> source, int size)
    {
        if (size <= 0)
        {
            throw new ArgumentException("Size limit must be greater than zero");
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _size = size;
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var sizeLimiter = new SizeLimiter<TObject, TKey>(_size);
                var root = new IntermediateCache<TObject, TKey>(_source);

                var subscriber = root.Connect().Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now)).Select(
                    changes =>
                    {
                        var result = sizeLimiter.Change(changes);

                        var removes = result.Where(c => c.Reason == ChangeReason.Remove);
                        root.Edit(updater => removes.ForEach(c => updater.Remove(c.Key)));
                        return result;
                    }).Finally(observer.OnCompleted).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();
                        root.Dispose();
                    });
            });
}
