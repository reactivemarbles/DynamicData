// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static class ObservableListEx
{
    /// <summary>
    /// Injects a side effect into a change set observable.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="adaptor">The adaptor.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// adaptor.
    /// </exception>
    public static IObservable<IChangeSet<T>> Adapt<T>(this IObservable<IChangeSet<T>> source, IChangeSetAdaptor<T> adaptor)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();
                return source.Synchronize(locker).Select(
                    changes =>
                    {
                        adaptor.Adapt(changes);
                        return changes;
                    }).SubscribeSafe(observer);
            });
    }

    /// <summary>
    /// Adds a key to the change set result which enables all observable cache features of dynamic data.
    /// </summary>
    /// <remarks>
    /// All indexed changes are dropped i.e. sorting is not supported by this function.
    /// </remarks>
    /// <typeparam name="TObject">The type of  object.</typeparam>
    /// <typeparam name="TKey">The type of  key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
    }

    /// <summary>
    /// Apply a logical And operator between the collections.
    /// Items which are in all of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="others">The others.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> And<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.And, others);
    }

    /// <summary>
    /// Apply a logical And operator between the collections.
    /// Items which are in all of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The sources.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> And<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> And<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.And);

    /// <summary>
    /// Converts the source list to an read only observable list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable list.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Converts the source observable to an read only observable list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable list.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservableList<T> AsObservableList<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Automatically refresh downstream operators when any property changes.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenAnyPropertyChanged();
                }

                return t.WhenAnyPropertyChanged().Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
            },
            changeSetBuffer,
            scheduler);
    }

    /// <summary>
    /// Automatically refresh downstream operators when properties change.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <typeparam name="TProperty">The type of property.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="propertyAccessor">Specify a property to observe changes. When it changes a Refresh is invoked.</param>
    /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refresh invocations.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertyAccessor, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.AutoRefreshOnObservable(
            t =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenPropertyChanged(propertyAccessor, false);
                }

                return t.WhenPropertyChanged(propertyAccessor, false).Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
            },
            changeSetBuffer,
            scheduler);
    }

    /// <summary>
    /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <typeparam name="TAny">A ignored type used for specifying what to auto refresh on.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <param name="reevaluator">An observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    public static IObservable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return new AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }

    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="targetCollection">The target collection.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(targetCollection, options);
    }

    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="targetCollection">The target collection.</param>
    /// <param name="options">The binding options.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, options);
        return source.Adapt(adaptor);
    }

    /// <summary>
    /// Creates a binding to a readonly observable collection which is specified as an 'out' parameter.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>A continuation of the source stream.</returns>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <summary>
    /// Creates a binding to a readonly observable collection which is specified as an 'out' parameter.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="options">The binding options.</param>
    /// <returns>A continuation of the source stream.</returns>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var target = new ObservableCollectionExtended<T>();
        var result = new ReadOnlyObservableCollection<T>(target);
        var adaptor = new ObservableCollectionAdaptor<T>(target, options);
        readOnlyObservableCollection = result;
        return source.Adapt(adaptor);
    }

#if SUPPORTS_BINDINGLIST

    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="bindingList">The target binding list.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, BindingList<T> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

        return source.Adapt(new BindingListAdaptor<T>(bindingList, resetThreshold));
    }

#endif

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will  be fired.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, scheduler);

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will  be fired.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
    /// <param name="initialPauseState">if set to <c>true</c> [initial pause state].</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return BufferIf(source, pauseIfTrueSelector, initialPauseState, null, scheduler);
    }

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will  be fired.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
    /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

    /// <summary>
    /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
    /// When a resume signal has been received the batched updates will  be fired.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified.</param>
    /// <param name="initialPauseState">if set to <c>true</c> [initial pause state].</param>
    /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return new BufferIf<T>(source, pauseIfTrueSelector, initialPauseState, timeOut, scheduler).Run();
    }

    /// <summary>
    /// Buffers changes for an initial period only. After the period has elapsed, not further buffering occurs.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <param name="source">The source change set.</param>
    /// <param name="initialBuffer">The period to buffer, measure from the time that the first item arrives.</param>
    /// <param name="scheduler">The scheduler to buffer on.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject>> BufferInitial<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
        where TObject : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

    /// <summary>
    /// Cast the changes to another form.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination>> Cast<TDestination>(this IObservable<IChangeSet<object>> source)
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => changes.Transform(t => (TDestination)t));
    }

    /// <summary>
    /// <para>Cast the changes to another form.</para>
    /// <para>Alas, I had to add the converter due to type inference issues. The converter can be avoided by CastToObject() first.</para>
    /// </summary>
    /// <typeparam name="TSource">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="conversionFactory">The conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Cast the underlying type of an object. Use before a Cast function.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<object>> CastToObject<T>(this IObservable<IChangeSet<T>> source)
        where T : class => source.Select(changes => changes.Transform(t => (object)t));

    /// <summary>
    /// Clones the target list as a side effect of the stream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="target">The target of the clone.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Do(target.Clone);
    }

    /// <summary>
    /// <para>Convert the object using the specified conversion function.</para>
    /// <para>This is a lighter equivalent of Transform and is designed to be used with non-disposable objects.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="conversionFactory">The conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    [Obsolete("Prefer Cast as it is does the same thing but is semantically correct")]
    public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Defer the subscription until the stream has been inflated with data.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<T>(source).Run();
    }

    /// <summary>
    /// Defer the subscription until the cache has been inflated with data.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().DeferUntilLoaded();
    }

    /// <summary>
    /// <para>Disposes each item when no longer required.</para>
    /// <para>
    /// Individual items are disposed after removal or replacement changes have been sent downstream.
    /// All items previously-published on the stream are disposed after the stream finalizes.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>A continuation of the original stream.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<T>(source).Run();
    }

    /// <summary>
    /// Selects distinct values from the source, using the specified value selector.
    /// </summary>
    /// <typeparam name="TObject">The type of the source.</typeparam>
    /// <typeparam name="TValue">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The transform factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return new Distinct<TObject, TValue>(source, valueSelector).Run();
    }

    /// <summary>
    /// Apply a logical Except operator between the collections.
    /// Items which are in the source and not in the others are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="others">The others.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Except, others);
    }

    /// <summary>
    /// Apply a logical Except operator between the collections.
    /// Items which are in the source and not in the others are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The sources.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Except<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Except<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Except);

    /// <summary>
    /// Removes items from the cache according to the value specified by the time selector function.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="timeSelector">Selector returning when to expire the item. Return null for non-expiring item.</param>
    /// <param name="pollingInterval">Enter the polling interval to optimise expiry timers, if omitted 1 timer is created for each unique expiry time.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the enumerable of items.</returns>
    public static IObservable<IEnumerable<T>> ExpireAfter<T>(
                this ISourceList<T> source,
                Func<T, TimeSpan?> timeSelector,
                TimeSpan? pollingInterval = null,
                IScheduler? scheduler = null)
            where T : notnull
        => List.Internal.ExpireAfter<T>.Create(
            source: source,
            timeSelector: timeSelector,
            pollingInterval: pollingInterval,
            scheduler: scheduler);

    /// <summary>
    /// Filters the source using the specified valueSelector.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicate">The valueSelector.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new Filter<T>(source, predicate).Run();
    }

    /// <summary>
    /// Filters source using the specified filter observable predicate.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicate">The predicate which indicates which items should be included.</param>
    /// <param name="filterPolicy">Should the filter clear and replace, or calculate a diff-set.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// filterController.</exception>
    public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, IObservable<Func<T, bool>> predicate, ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new Filter<T>(source, predicate, filterPolicy).Run();
    }

    /// <summary>
    /// <para>Filters source on the specified observable property using the specified predicate.</para>
    /// <para>The filter will automatically reapply when a property changes.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="objectFilterObservable">The filter property selector. When the observable changes the filter will be re-evaluated.</param>
    /// <param name="propertyChangedThrottle">The property changed throttle.</param>
    /// <param name="scheduler">The scheduler used when throttling.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject>> FilterOnObservable<TObject>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> objectFilterObservable, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new FilterOnObservable<TObject>(source, objectFilterObservable, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// <para>Filters source on the specified property using the specified predicate.</para>
    /// <para>The filter will automatically reapply when a property changes.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertySelector">The property selector. When the property changes the filter specified will be re-evaluated.</param>
    /// <param name="predicate">A predicate based on the object which contains the changed property.</param>
    /// <param name="propertyChangedThrottle">The property changed throttle.</param>
    /// <param name="scheduler">The scheduler used when throttling.</param>
    /// <returns>An observable which emits the change set.</returns>
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

        return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Convert the result of a buffer operation to a change set.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        where T : notnull => source.Where(x => x.Count != 0).Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));

    /// <summary>
    /// Provides a call back for each item change.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<Change<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.ForEach(action));
    }

    /// <summary>
    /// <para>Provides a call back for each item change.</para>
    /// <para>Range changes are flattened, so there is only need to check for Add, Replace, Remove and Clear.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="action">The action.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<ItemChange<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.Flatten().ForEach(action));
    }

    /// <summary>
    ///  Groups the source on the value returned by group selector factory.  The groupings contains an inner observable list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroup">The type of the group.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupSelector">The group selector.</param>
    /// <param name="regrouper">Force the grouping function to recalculate the group value.
    /// For example if you have a time based grouping with values like `Last Minute', 'Last Hour', 'Today' etc regrouper is used to refresh these groupings.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelector.
    /// </exception>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOn<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroup> groupSelector, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelector.ThrowArgumentNullExceptionIfNull(nameof(groupSelector));

        return new GroupOn<TObject, TGroup>(source, groupSelector, regrouper).Run();
    }

    /// <summary>
    /// Groups the source using the property specified by the property selector.  The resulting groupings contains an inner observable list.
    /// Groups are re-applied when the property value changed.
    /// When there are likely to be a large number of group property changes specify a throttle to improve performance.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroup">The type of the group.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertySelector">The property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">The property changed throttle.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOnProperty<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnProperty<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    /// Groups the source using the property specified by the property selector.  The resulting groupings are immutable.
    /// Groups are re-applied when the property value changed.
    /// When there are likely to be a large number of group property changes specify a throttle to improve performance.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroup">The type of the group.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertySelector">The property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">The property changed throttle.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroup>>> GroupOnPropertyWithImmutableState<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnPropertyWithImmutableState<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }

    /// <summary>
    ///  Groups the source on the value returned by group selector factory. Each update produces immutable grouping.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="groupSelectorKey">The group selector key.</param>
    /// <param name="regrouper">Force the grouping function to recalculate the group value.
    /// For example if you have a time based grouping with values like `Last Minute', 'Last Hour', 'Today' etc regrouper is used to refresh these groupings.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// groupSelectorKey.
    /// </exception>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> GroupWithImmutableState<TObject, TGroupKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelectorKey.ThrowArgumentNullExceptionIfNull(nameof(groupSelectorKey));

        return new GroupOnImmutable<TObject, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }

    /// <summary>
    /// Limits the size of the source cache to the specified limit.
    /// Notifies which items have been removed from the source list.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="sizeLimit">The size limit.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>An observable which emits a enumerable of items.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <exception cref="ArgumentException">sizeLimit cannot be zero.</exception>
    public static IObservable<IEnumerable<T>> LimitSizeTo<T>(this ISourceList<T> source, int sizeLimit, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (sizeLimit <= 0)
        {
            throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));
        }

        var locker = new object();
        var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? GlobalConfig.DefaultScheduler, locker);

        return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
    }

    /// <summary>
    /// Dynamically merges the observable which is selected from each item in the stream, and un-merges the item
    /// when it is no longer part of the stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="observableSelector">The observable selector.</param>
    /// <returns>An observable which emits the destination value.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// observableSelector.</exception>
    public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeMany<T, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer).Run();
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges both observable changesets into a single stream of ChangeSet events.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="other">The Other Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IObservable<IChangeSet<TObject>> other, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        other.ThrowArgumentNullExceptionIfNull(nameof(other));

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  Merges the source changeset and the collection of other changesets together into a single stream of ChangeSet events.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="others">The Other Observable ChangeSets.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IEnumerable<IObservable<IChangeSet<TObject>>> others, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Operator similiar to Merge except it is ChangeSet aware.  All of the observable changesets are merged together into a single stream of ChangeSet events.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="equalityComparer"><see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="scheduler">(Optional) <see cref="IScheduler"/> instance to use when enumerating the collection.</param>
    /// <param name="completable">Whether or not the result Observable should complete if all the changesets complete.</param>
    /// <returns>The result from merging the changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new MergeChangeSets<TObject>(source, equalityComparer, completable, scheduler).Run();
    }

    /// <summary>
    /// Merges all of the Cache Observable ChangeSets into a single ChangeSets that correctly handles removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The SourceList of Observable Cache ChangeSets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservableList<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer);
    }

    /// <summary>
    /// Merges each Observable ChangeSet in the ObservableList into a single stream of ChangeSets that correctly handles removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The List Observable ChangeSet of Cache Observable ChangeSets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject>>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer);
    }

    /// <summary>
    /// Merges each Observable ChangeSet in the ObservableList into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The SourceList of Observable Cache ChangeSets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(comparer);
    }

    /// <summary>
    /// Merges all of the Cache Observable ChangeSets into a single ChangeSets while correctly handling multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The SourceList of Observable Cache ChangeSets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().MergeChangeSets(equalityComparer, comparer);
    }

    /// <summary>
    /// Merges all of the Cache Observable ChangeSets into a single ChangeSets while correctly handling multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The List Observable ChangeSet of Cache Observable ChangeSets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeChangeSets(comparer);
    }

    /// <summary>
    /// Merges each Observable ChangeSet in the ObservableList into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The List Observable ChangeSet of Cache Observable ChangeSets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeManyChangeSets(static src => src, equalityComparer, comparer);
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is List ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TDestination}"/> and merges the result children together into a single stream of ChangeSets that correctly handles removal of the parent items and other changes to the source list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <returns>The result from merging the children list changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TDestination : notnull
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (observableSelector == null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        return new MergeManyListChangeSets<TObject, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is Cache ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TDestination, TDestinationKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// Operator similiar to MergeMany except it is Cache ChangeSet aware.  It uses <paramref name="observableSelector"/> to transform each item in the source into a child <see cref="IChangeSet{TDestination, TDestinationKey}"/> and merges the result children together into a single stream of ChangeSets that correctly handles multiple Keys and removal of the parent items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The Source Observable ChangeSet.</param>
    /// <param name="observableSelector">Factory Function used to create child changesets.</param>
    /// <param name="equalityComparer">Optional <see cref="IEqualityComparer{T}"/> instance to determine if two elements are the same.</param>
    /// <param name="comparer">Optional <see cref="IComparer{T}"/> instance to determine which element to emit if the same key is emitted from multiple child changesets.</param>
    /// <returns>The result from merging the child changesets together.</returns>
    /// <exception cref="ArgumentNullException">Parameter was null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Prevents an empty notification.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> NotEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(s => s.Count != 0);
    }

    /// <summary>
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="addAction">The add action.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> OnItemAdded<T>(this IObservable<IChangeSet<T>> source, Action<T> addAction)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        addAction.ThrowArgumentNullExceptionIfNull(nameof(addAction));

        return new OnBeingAdded<T>(source, addAction).Run();
    }

    /// <summary>
    /// Callback for each item as and when it is being refreshed in the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="refreshAction">The refresh action.</param>
    /// <returns>An observable which emits a change set with items being added.</returns>
    public static IObservable<IChangeSet<TObject>> OnItemRefreshed<TObject>(this IObservable<IChangeSet<TObject>> source, Action<TObject> refreshAction)
        where TObject : notnull
    {
        var refreshAction2 = refreshAction;
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (refreshAction2 == null)
        {
            throw new ArgumentNullException(nameof(refreshAction));
        }

        return source.Do((IChangeSet<TObject> changes) =>
            changes.Where((Change<TObject> c) => c.Reason == ListChangeReason.Refresh).ForEach((Change<TObject> c) => refreshAction2(c.Item.Current)));
    }

    /// <summary>
    /// Callback for each item as and when it is being removed from the stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="removeAction">The remove action.</param>
    /// <param name="invokeOnUnsubscribe"> Should the remove action be invoked when the subscription is disposed.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// removeAction.
    /// </exception>
    public static IObservable<IChangeSet<T>> OnItemRemoved<T>(this IObservable<IChangeSet<T>> source, Action<T> removeAction, bool invokeOnUnsubscribe = true)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));

        return new OnBeingRemoved<T>(source, removeAction, invokeOnUnsubscribe).Run();
    }

    /// <summary>
    /// Apply a logical Or operator between the collections.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Or<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Apply a logical Or operator between the collections.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="others">The others.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Or, others);
    }

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Or<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Or);

    /// <summary>
    /// Applies paging to the data source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="requests">Observable to control page requests.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IPageChangeSet<T>> Page<T>(this IObservable<IChangeSet<T>> source, IObservable<IPageRequest> requests)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        requests.ThrowArgumentNullExceptionIfNull(nameof(requests));

        return new Pager<T>(source, requests).Run();
    }

    /// <summary>
    /// list.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// destination.
    /// </exception>
    public static IDisposable PopulateInto<T>(this IObservable<IChangeSet<T>> source, ISourceList<T> destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <summary>
    ///  The latest copy of the cache is exposed for querying after each modification to the underlying data.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="resultSelector">The result selector.</param>
    /// <returns>An observable which emits the destination value.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// resultSelector.
    /// </exception>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new QueryWhenChanged<T>(source).Run();
    }

    /// <summary>
    /// List equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> RefCount<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<T>(source).Run();
    }

    /// <summary>
    /// <para>Removes the index from all changes.</para>
    /// <para>NB: This operator has been introduced as a temporary fix for creating an Or operator using merge many.</para>
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> RemoveIndex<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
    }

    /// <summary>
    /// Reverse sort of the change set.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// comparer.
    /// </exception>
    public static IObservable<IChangeSet<T>> Reverse<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        var reverser = new Reverser<T>();
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(reverser.Reverse(changes)));
    }

    /// <summary>
    /// Defer the subscription until loaded and skip initial change set.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Sorts the sequence using the specified comparer.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer used for sorting.</param>
    /// <param name="options">For improved performance, specify SortOptions.UseBinarySearch. This can only be used when the values which are sorted on are immutable.</param>
    /// <param name="resort">OnNext of this observable causes data to resort. This is required when the value which is sorted on mutable.</param>
    /// <param name="comparerChanged">An observable comparer used to change the comparer on which the sorted list i.</param>
    /// <param name="resetThreshold">Since sorting can be slow for large record sets, the reset threshold is used to force the list re-ordered. </param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// comparer.</exception>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IComparer<T> comparer, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, IObservable<IComparer<T>>? comparerChanged = null, int resetThreshold = 50)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparer.ThrowArgumentNullExceptionIfNull(nameof(comparer));

        return new Sort<T>(source, comparer, options, resort, comparerChanged, resetThreshold).Run();
    }

    /// <summary>
    /// Sorts the sequence using the specified observable comparer.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparerChanged">An observable comparer used to change the comparer on which the sorted list i.</param>
    /// <param name="options">For improved performance, specify SortOptions.UseBinarySearch. This can only be used when the values which are sorted on are immutable.</param>
    /// <param name="resort">OnNext of this observable causes data to resort. This is required when the value which is sorted on mutable.</param>
    /// <param name="resetThreshold">Since sorting can be slow for large record sets, the reset threshold is used to force the list re-ordered. </param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// comparer.</exception>
    public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source, IObservable<IComparer<T>> comparerChanged, SortOptions options = SortOptions.None, IObservable<Unit>? resort = null, int resetThreshold = 50)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        comparerChanged.ThrowArgumentNullExceptionIfNull(nameof(comparerChanged));

        return new Sort<T>(source, null, options, resort, comparerChanged, resetThreshold).Run();
    }

    /// <summary>
    /// Prepends an empty change set to the source.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source observable of change set values.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.StartWith(ChangeSet<T>.Empty);

    /// <summary>
    /// Subscribes to each item when it is added to the stream and unsubscribes when it is removed.  All items will be unsubscribed when the stream is disposed.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="subscriptionFactory">The subscription function.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// subscriptionFactory.</exception>
    /// <remarks>
    /// Subscribes to each item when it is added or updates and unsubscribes when it is removed.
    /// </remarks>
    public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source, Func<T, IDisposable> subscriptionFactory)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        subscriptionFactory.ThrowArgumentNullExceptionIfNull(nameof(subscriptionFactory));

        return new SubscribeMany<T>(source, subscriptionFactory).Run();
    }

    /// <summary>
    /// Suppress  refresh notifications.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source observable change set.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> SuppressRefresh<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.WhereReasonsAreNot(ListChangeReason.Refresh);

    /// <summary>
    /// Transforms an observable sequence of observable lists into a single sequence
    /// producing values only from the most recent observable sequence.
    /// Each time a new inner observable sequence is received, unsubscribe from the
    /// previous inner observable sequence and clear the existing result set.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>
    /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources" /> is null.</exception>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Transforms an observable sequence of observable changes sets into an observable sequence
    /// producing values only from the most recent observable sequence.
    /// Each time a new inner observable sequence is received, unsubscribe from the
    /// previous inner observable sequence and clear the existing result set.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>
    /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources" /> is null.</exception>
    public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Switch<T>(sources).Run();
    }

    /// <summary>
    /// Converts the change set into a fully formed collection. Each change in the source results in a new collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.QueryWhenChanged(items => items);

    /// <summary>
    /// Converts the observable to an observable change set.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return ToObservableChangeSet(source, null, -1, scheduler);
    }

    /// <summary>
    /// Converts the observable to an observable change set, allowing time expiry to be specified.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source, Func<T, TimeSpan?> expireAfter, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        expireAfter.ThrowArgumentNullExceptionIfNull(nameof(expireAfter));

        return ToObservableChangeSet(source, expireAfter, -1, scheduler);
    }

    /// <summary>
    /// Converts the observable to an observable change set, with a specified limit of how large the list can be.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source, int limitSizeTo, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return ToObservableChangeSet(source, null, limitSizeTo, scheduler);
    }

    /// <summary>
    /// Converts the observable to an observable change set, allowing size and time limit to be specified.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
    /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit. Supply -1 to disable size limiting.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source, Func<T, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new ToObservableChangeSet<T>(source, expireAfter, limitSizeTo, scheduler).Run();
    }

    /// <summary>
    /// Converts the observable to an observable change set.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source, IScheduler? scheduler = null)
        where T : notnull => ToObservableChangeSet<T>(source, null, -1, scheduler);

    /// <summary>
    /// Converts the observable to an observable change set, allowing size and time limit to be specified.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source, int limitSizeTo, IScheduler? scheduler = null)
        where T : notnull => ToObservableChangeSet<T>(source, null, limitSizeTo, scheduler);

    /// <summary>
    /// Converts the observable to an observable change set, allowing size to be specified.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source, Func<T, TimeSpan?> expireAfter, IScheduler? scheduler = null)
        where T : notnull => ToObservableChangeSet(source, expireAfter, -1, scheduler);

    /// <summary>
    /// Converts the observable to an observable change set, allowing size and time limit to be specified.
    /// Change set observes observable change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache.</param>
    /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit.</param>
    /// <param name="scheduler">The scheduler (only used for time expiry).</param>
    /// <returns>An observable which emits a change set.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source, Func<T, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new ToObservableChangeSet<T>(source, expireAfter, limitSizeTo, scheduler).Run();
    }

    /// <summary>
    /// Limits the size of the result set to the specified number of items.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="numberOfItems">The number of items.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Top<T>(this IObservable<IChangeSet<T>> source, int numberOfItems)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }

    /// <summary>
    /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TSortKey">The sort key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="sort">The sort function.</param>
    /// <param name="sortOrder">The sort order. Defaults to ascending.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        where TObject : notnull => source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort)) : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));

    /// <summary>
    /// Converts the change set into a fully formed sorted collection. Each change in the source results in a new sorted collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The sort comparer.</param>
    /// <returns>An observable which emits the read only collection.</returns>
    public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source, IComparer<TObject> comparer)
        where TObject : notnull => source.QueryWhenChanged(
            query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform function.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, _, idx) => transformFactory(t, idx), transformOnRefresh);
    }

    /// <summary>
    /// <para>Projects each update item to a new form using the specified transform function.</para>
    /// <para>*** Annoyingly when using this overload you will have to explicitly specify the generic type arguments as type inference fails.</para>
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform function.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform<TSource, TDestination>((t, previous, _) => transformFactory(t, previous), transformOnRefresh);
    }

    /// <summary>
    /// <para>Projects each update item to a new form using the specified transform function.</para>
    /// <para>*** Annoyingly when using this overload you will have to explicitly specify the generic type arguments as type inference fails.</para>
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new Transformer<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, i) => transformFactory(t, i), transformOnRefresh);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, d, _) => transformFactory(t, d), transformOnRefresh);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received.</param>
    /// <returns>A an observable change set of the transformed object.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// valueSelector.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }

    /// <summary>
    /// Equivalent to a select many transform. To work, the key must individually identify each child.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">The selector function which selects the enumerable.</param>
    /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// manySelector.
    /// </exception>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
    }

    /// <summary>
    /// Flatten the nested observable collection, and  observe subsequently observable collection changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">The selector function which selects the enumerable.</param>
    /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <summary>
    /// Flatten the nested observable collection, and  observe subsequently observable collection changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">The selector function which selects the enumerable.</param>
    /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <summary>
    /// Flatten the nested observable list, and observe subsequent observable collection changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">The selector function which selects the enumerable.</param>
    /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <summary>
    /// Virtualises the source using parameters provided via the requests observable.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="requests">The requests.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IVirtualChangeSet<T>> Virtualise<T>(this IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        requests.ThrowArgumentNullExceptionIfNull(nameof(requests));

        return new Virtualiser<T>(source, requests).Run();
    }

    /// <summary>
    /// Watches each item in the collection and notifies when any of them has changed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes.</param>
    /// <returns>An observable which emits the object.</returns>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject>(this IObservable<IChangeSet<TObject>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }

    /// <summary>
    /// Watches each item in the collection and notifies when any of them has changed.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertyAccessor">The property accessor.</param>
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <returns>An observable which emits the property value.</returns>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue));
    }

    /// <summary>
    /// Watches each item in the collection and notifies when any of them has changed.
    /// </summary>
    /// <typeparam name="TObject">The type of object.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertyAccessor">The property accessor.</param>
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <returns>An observable which emits the value.</returns>
    public static IObservable<TValue?> WhenValueChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue).Select(pv => pv.Value));
    }

    /// <summary>
    /// Includes changes for the specified reasons only.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="reasons">The reasons.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentException">Must enter at least 1 reason.</exception>
    public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            changes =>
            {
                var filtered = changes.Where(change => matches.Contains(change.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }

    /// <summary>
    /// Excludes updates for the specified reasons.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="reasons">The reasons.</param>
    /// <returns>An observable which emits the change set.</returns>
    /// <exception cref="ArgumentException">Must enter at least 1 reason.</exception>
    public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        if (reasons.Length == 1 && reasons[0] == ListChangeReason.Refresh)
        {
            // If only refresh changes are removed, then there's no need to remove the indexes
            return source.Select(changes =>
            {
                var filtered = changes.Where(c => c.Reason != ListChangeReason.Refresh);
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            updates =>
            {
                var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }

    /// <summary>
    /// Apply a logical Xor operator between the collections.
    /// Items which are only in one of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="others">The others.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservable<IChangeSet<T>> source, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <summary>
    /// Apply a logical Xor operator between the collections.
    /// Items which are only in one of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The sources.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this ICollection<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservable<IChangeSet<T>>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<IObservableList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="sources">The source.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<T>> Xor<T>(this IObservableList<ISourceList<T>> sources)
        where T : notnull => sources.Combine(CombineOperator.Xor);

    private static IObservable<IChangeSet<T>> Combine<T>(this ICollection<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Combiner<T>(sources, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservable<IChangeSet<T>> source, CombineOperator type, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = source.EnumerateOne().Union(others).ToList();
        return new Combiner<T>(items, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<ISourceList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservableList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new DynamicCombiner<T>(sources, type).Run();
    }
}
