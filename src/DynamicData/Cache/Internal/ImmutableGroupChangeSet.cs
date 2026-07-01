// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ImmutableGroupChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
internal sealed class ImmutableGroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>, IImmutableGroupChangeSet<TObject, TKey, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The Empty field.
    /// </summary>
    public static new readonly IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Empty = new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableGroupChangeSet{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    public ImmutableGroupChangeSet(IEnumerable<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableGroupChangeSet{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    private ImmutableGroupChangeSet()
    {
    }
}
