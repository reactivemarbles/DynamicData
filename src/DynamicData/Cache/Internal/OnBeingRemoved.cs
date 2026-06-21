// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the OnBeingRemoved class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="removeAction">The removeAction value.</param>
internal sealed class OnBeingRemoved<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> removeAction)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _removeAction field.
    /// </summary>
    private readonly Action<TObject, TKey> _removeAction = removeAction ?? throw new ArgumentNullException(nameof(removeAction));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var cache = new Cache<TObject, TKey>();
                var subscriber = _source.SynchronizeSafe()
                    .Do(changes => RegisterForRemoval(changes, cache))
                    .SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();
                        cache.KeyValues.ForEach(kvp => _removeAction(kvp.Value, kvp.Key));
                        cache.Clear();
                    });
            });

    /// <summary>
    /// Executes the RegisterForRemoval operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <param name="cache">The cache value.</param>
    private void RegisterForRemoval(IChangeSet<TObject, TKey> changes, Cache<TObject, TKey> cache)
    {
        changes.ForEach(
            change =>
            {
                if (change.Reason is ChangeReason.Remove)
                {
                    _removeAction(change.Current, change.Key);
                }
            });
        cache.Clone(changes);
    }
}
