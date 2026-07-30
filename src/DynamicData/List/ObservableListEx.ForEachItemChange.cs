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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Invokes <paramref name="action"/> for every individual <c>ItemChange&lt;TObject&gt;</c> in each changeset.
    /// Range changes are flattened into individual item changes first, so the callback only receives Add, Replace, Remove, and Refresh.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to observe each item-level change in.</param>
    /// <param name="action">The <c>Action&lt;ItemChange&lt;TObject&gt;&gt;</c> action invoked for each individual item change.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Unlike <c>ForEachChange&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Action&lt;Change&lt;TObject&gt;&gt;)</c>, this operator flattens
    /// <b>AddRange</b>, <b>RemoveRange</b>, and <b>Clear</b> into individual <c>ItemChange&lt;TObject&gt;</c> entries before invoking the callback.
    /// </para>
    /// </remarks>
    /// <seealso><c>ForEachChange&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Action&lt;Change&lt;TObject&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<ItemChange<TObject>> action)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(changes => changes.Flatten().ForEach(action));
    }
}
