// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

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
    /// Wraps an <c>IObservableCache&lt;TObject, TKey&gt;</c> in a read-only facade, hiding the mutable API.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>IObservableCache&lt;TObject, TKey&gt;</c> to operate on.</param>
    /// <returns>A read-only <c>IObservableCache&lt;TObject, TKey&gt;</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>AsObservableCache&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, bool)</c></seealso>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new AnonymousObservableCache<TObject, TKey>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a queryable, read-only <c>IObservableCache&lt;TObject, TKey&gt;</c>.
    /// The cache subscribes to the source on first access and maintains a live snapshot of all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to materialize into a read-only cache.</param>
    /// <param name="applyLocking">If <see langword="true"/> (default), all cache operations are synchronized. Set to <see langword="false"/> when the caller guarantees single-threaded access.</param>
    /// <returns>A read-only observable cache that reflects the current state of the pipeline.</returns>
    /// <remarks>
    /// <para>
    /// Disposing the returned cache unsubscribes from the source stream. The cache's <c>Connect()</c>
    /// method provides a changeset stream of its own, which re-emits the current state on each new subscriber.
    /// </para>
    /// <para>When <paramref name="applyLocking"/> is <see langword="false"/>, a <c>LockFreeObservableCache&lt;TObject, TKey&gt;</c> is used internally.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>AsObservableCache&lt;TObject, TKey&gt;(IObservableCache&lt;TObject, TKey&gt;)</c></seealso>
    /// <seealso><c>PopulateInto&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, ISourceCache&lt;TObject, TKey&gt;)</c></seealso>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, bool applyLocking = true)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (applyLocking)
        {
            return new AnonymousObservableCache<TObject, TKey>(source);
        }

        return new LockFreeObservableCache<TObject, TKey>(source);
    }
}
