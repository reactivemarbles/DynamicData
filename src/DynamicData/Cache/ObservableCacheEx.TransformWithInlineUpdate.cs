// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> and does not provide an error handler (factory exceptions propagate as OnError).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, false);
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload does not provide an error handler (factory exceptions propagate as OnError). The <c>transformOnRefresh</c> parameter controls Refresh behavior.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, transformOnRefresh: transformOnRefresh).Run();
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> but includes an error handler for factory/update action exceptions.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, errorHandler, false);
    }

    /// <summary>
    /// Projects each item using a transform factory for Add, and mutates the existing transformed
    /// item in place (via an update action) for Update, preserving the original object reference.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items. Must be a reference type since items are mutated in place.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform with in-place mutation on updates.</param>
    /// <param name="transformFactory">A <see cref="Func{T, TResult}"/> that called on Add (and optionally Refresh) to create a new <typeparamref name="TDestination"/>.</param>
    /// <param name="updateAction">A <see cref="Action{T}"/> that called on Update. Receives <c>(existingTransformed, newSource)</c>. Mutate the existing transformed item to reflect the new source value. Example: <c>(vm, model) =&gt; vm.Value = model.Value</c>.</param>
    /// <param name="errorHandler">A <see cref="Action{T}"/> that called when <paramref name="transformFactory"/> or <paramref name="updateAction"/> throws. The faulting item is skipped.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh changes call <paramref name="updateAction"/> on the existing item.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// This is useful when the destination type is a ViewModel that should maintain its identity across updates.
    /// Instead of replacing the entire ViewModel, the update action patches the existing instance.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="transformFactory"/>, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls <paramref name="updateAction"/> on the EXISTING transformed item (same reference), emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove.</description></item>
    ///   <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is true, calls <paramref name="updateAction"/>. Otherwise forwarded as Refresh.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, <paramref name="updateAction"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, errorHandler, transformOnRefresh).Run();
    }
}
