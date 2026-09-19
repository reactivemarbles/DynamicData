// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif

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
    /// Subscribes to the source changeset stream and pipes all changes into the <paramref name="destination"/> <c>ISourceList&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to pipe into a target list.</param>
    /// <param name="destination">The destination <c>ISourceList&lt;T&gt;</c> to receive all changes.</param>
    /// <returns>An <see cref="IDisposable"/> representing the subscription. Dispose to stop piping changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Each changeset is applied to the destination using <c>Clone()</c> inside an <c>Edit()</c> call, producing a single batch update per changeset.</para>
    /// </remarks>
    /// <seealso><c>Clone&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IList&lt;T&gt;)</c></seealso>
    /// <seealso><c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservableCollection&lt;T&gt;, BindingOptions)</c></seealso>
    /// <seealso><c>ObservableCacheEx.PopulateInto&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, ISourceCache&lt;TObject, TKey&gt;)</c></seealso>
    public static IDisposable PopulateInto<T>(this IObservable<IChangeSet<T>> source, ISourceList<T> destination)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }
}
