// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the GroupChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
internal sealed class GroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>, IGroupChangeSet<TObject, TKey, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The Empty field.
    /// </summary>
    public static new readonly IGroupChangeSet<TObject, TKey, TGroupKey> Empty = new GroupChangeSet<TObject, TKey, TGroupKey>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupChangeSet{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    public GroupChangeSet(IEnumerable<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupChangeSet{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    private GroupChangeSet()
    {
    }
}
