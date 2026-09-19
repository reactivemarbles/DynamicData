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
    /// Applies changeset mutations to a target <c>IObservableCollection&lt;T&gt;</c> for UI data binding.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to bind to a collection.</param>
    /// <param name="targetCollection">The <c>IObservableCollection&lt;T&gt;</c> target collection to keep in sync.</param>
    /// <param name="resetThreshold">When a changeset exceeds this many changes, the collection is reset instead of applying individual changes.</param>
    /// <returns>A continuation of the source changeset stream (allows further chaining).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="targetCollection"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Delegates to <c>Adapt&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IChangeSetAdaptor&lt;T&gt;)</c> with an internal collection adaptor.
    /// Each changeset is applied to the target collection on the calling thread. For UI binding, ensure the source is
    /// observed on the UI thread (e.g., via <c>ObserveOn</c>).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item inserted at the specified index in the target collection.</description></item>
    /// <item><term>AddRange</term><description>Items inserted as a range. If the count exceeds <paramref name="resetThreshold"/>, the collection is cleared and repopulated.</description></item>
    /// <item><term>Replace</term><description>Item at the specified index is replaced.</description></item>
    /// <item><term>Remove</term><description>Item at the specified index is removed.</description></item>
    /// <item><term>RemoveRange/Clear</term><description>Items removed from the collection.</description></item>
    /// <item><term>Moved</term><description>Item is moved between positions in the collection.</description></item>
    /// <item><term>Refresh</term><description>Depends on the adaptor implementation.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservableCollection&lt;T&gt;, BindingOptions)</c></seealso>
    /// <seealso><c>Bind&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, out ReadOnlyObservableCollection&lt;T&gt;, int)</c></seealso>
    /// <seealso><c>Clone&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IList&lt;T&gt;)</c></seealso>
    /// <seealso><c>Adapt&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IChangeSetAdaptor&lt;T&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IObservableCollection&lt;TObject&gt;, int)</c></seealso>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(targetCollection);

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(targetCollection, options);
    }

    /// <summary>
    /// Binds the source changeset stream to <paramref name="targetCollection"/>, with fine-grained <see cref="BindingOptions"/> control over reset threshold and other behaviors.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <param name="source">The source value.</param>
    /// <param name="targetCollection">The targetCollection value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, BindingOptions options)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(targetCollection);

        var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, options);
        return source.Adapt(adaptor);
    }

    /// <summary>
    /// Constructs a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and binds the changeset stream to it.
    /// Use this overload when you need a read-only view (typically for UI binding) without managing the backing collection yourself.
    /// The created collection is returned via the <paramref name="readOnlyObservableCollection"/> output parameter.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <param name="resetThreshold">The resetThreshold value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>The created collection is backed by an internal <c>ObservableCollectionExtended&lt;T&gt;</c>. Callers receive only the read-only wrapper.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <summary>
    /// Constructs a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and binds the changeset stream to it,
    /// with fine-grained <see cref="BindingOptions"/> control over reset threshold and other behaviors.
    /// The created collection is returned via the <paramref name="readOnlyObservableCollection"/> output parameter.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <para>The created collection is backed by an internal <c>ObservableCollectionExtended&lt;T&gt;</c>. Callers receive only the read-only wrapper.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, BindingOptions options)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        var target = new ObservableCollectionExtended<T>();
        var result = new ReadOnlyObservableCollection<T>(target);
        var adaptor = new ObservableCollectionAdaptor<T>(target, options);
        readOnlyObservableCollection = result;
        return source.Adapt(adaptor);
    }
#if SUPPORTS_BINDINGLIST

    /// <summary>
    /// Binds the source changeset stream to a WinForms <c>BindingList&lt;T&gt;</c>, keeping <paramref name="bindingList"/> in sync.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <param name="source">The source value.</param>
    /// <param name="bindingList">The bindingList value.</param>
    /// <param name="resetThreshold">The resetThreshold value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<T>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this IObservable<IChangeSet<T>> source, BindingList<T> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(bindingList);

        return source.Adapt(new BindingListAdaptor<T>(bindingList, resetThreshold));
    }
#endif
}
