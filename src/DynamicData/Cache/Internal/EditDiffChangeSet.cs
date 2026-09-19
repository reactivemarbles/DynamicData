// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the EditDiffChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="keySelector">The keySelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class EditDiffChangeSet<TObject, TKey>(IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IEnumerable<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;

    /// <summary>
    /// The _keySelector field.
    /// </summary>
    private readonly Func<TObject, TKey> _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        ObservableChangeSet.Create(
            cache => _source.Subscribe(items => cache.EditDiff(items, _equalityComparer), () => cache.Dispose()),
            _keySelector);
}
