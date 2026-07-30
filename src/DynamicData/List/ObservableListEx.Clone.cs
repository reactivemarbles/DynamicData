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
    /// Applies each changeset to the target list as a side effect, keeping it synchronized with the source.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to clone.</param>
    /// <param name="target">The <c>IList&lt;T&gt;</c> target list to clone changes into.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Lower-level than <c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservableCollection&lt;T&gt;, int)</c>. Uses <c>IList&lt;T&gt;</c>.Clone() to apply all changeset operations directly.</para>
    /// </remarks>
    /// <seealso><c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservableCollection&lt;T&gt;, int)</c></seealso>
    /// <seealso><c>PopulateInto&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, ISourceList&lt;T&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Do(target.Clone);
    }
}
