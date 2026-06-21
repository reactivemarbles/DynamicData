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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Excludes updates for the specified reasons.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter by excluding change reasons.</param>
    /// <param name="reasons">The <see cref="ChangeReason"/> values to filter by.</param>
    /// <returns>An observable which emits a change set with items not matching the reasons.</returns>
    /// <exception cref="ArgumentNullException">reasons.</exception>
    /// <exception cref="ArgumentException">Must select at least on reason.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes will cause memory leaks in downstream caches, since items are never cleaned up.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(reasons);
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must select at least one reason");
        }

        var hashed = new HashSet<ChangeReason>(reasons);

        return source.Select(updates => new ChangeSet<TObject, TKey>(updates.Where(u => !hashed.Contains(u.Reason)))).NotEmpty();
    }
}
