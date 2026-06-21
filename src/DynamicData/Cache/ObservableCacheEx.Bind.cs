// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <param name="refreshThreshold">The number of changes before a reset notification is triggered.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <seealso cref="ObservableListEx.Bind"/>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, int refreshThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(destination);

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = refreshThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = refreshThreshold };

        return source?.Bind(destination, new ObservableCollectionAdaptor<TObject, TKey>(options)) ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <param name="options">The <see cref="BindingOptions"/> that controls binding behavior.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source?.Bind(destination, new ObservableCollectionAdaptor<TObject, TKey>(options)) ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Binds the results to the specified binding collection using the specified update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <param name="updater">The <see cref="IObservableCollectionAdaptor{TObject, TKey}"/> that applies changes to the bound collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, IObservableCollectionAdaptor<TObject, TKey> updater)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);
        ArgumentExceptionHelper.ThrowIfNull(updater);

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
                source.SynchronizeSafe(InternalEx.NewLock()).Select(
                    changes =>
                    {
                        updater.Adapt(changes, destination);
                        return changes;
                    }).SubscribeSafe(observer));
    }

    /// <summary>
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="readOnlyObservableCollection">The output <see cref="ReadOnlyObservableCollection{TObject}"/> that will be populated with the results.</param>
    /// <param name="options">The <see cref="BindingOptions"/> that controls binding behavior.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        return source.Bind(target, new ObservableCollectionAdaptor<TObject, TKey>(options));
    }

    /// <summary>
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="readOnlyObservableCollection">The output <see cref="ReadOnlyObservableCollection{TObject}"/> that will be populated with the results.</param>
    /// <param name="resetThreshold">The number of changes before a reset notification is triggered.</param>
    /// <param name="useReplaceForUpdates">When <see langword="true"/>, uses Replace instead of Remove/Add for updates in the bound collection. Not all platforms support replace notifications.</param>
    /// <param name="adaptor">An optional <see cref="IObservableCollectionAdaptor{TObject, TKey}"/> that controls how the target collection is updated.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold, bool useReplaceForUpdates = BindingOptions.DefaultUseReplaceForUpdates, IObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (adaptor is not null)
        {
            var target = new ObservableCollectionExtended<TObject>();
            readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
            return source.Bind(target, adaptor);
        }

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold && useReplaceForUpdates == BindingOptions.DefaultUseReplaceForUpdates
            ? defaults
            : defaults with { ResetThreshold = resetThreshold, UseReplaceForUpdates = useReplaceForUpdates };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source.Bind(destination, DynamicDataOptions.Binding);
    }

    /// <summary>
    ///  Binds the results to the specified observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <param name="options">The <see cref="BindingOptions"/> that controls binding behavior.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        var updater = new SortedObservableCollectionAdaptor<TObject, TKey>(options);
        return source.Bind(destination, updater);
    }

    /// <summary>
    /// Binds the results to the specified binding collection using the specified update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
    /// <param name="updater">The <see cref="ISortedObservableCollectionAdaptor{TObject, TKey}"/> that applies changes to the bound collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, IObservableCollection<TObject> destination, ISortedObservableCollectionAdaptor<TObject, TKey> updater)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);
        ArgumentExceptionHelper.ThrowIfNull(updater);

        return Observable.Create<ISortedChangeSet<TObject, TKey>>(
            observer =>
                source.SynchronizeSafe(InternalEx.NewLock()).Select(
                    changes =>
                    {
                        updater.Adapt(changes, destination);
                        return changes;
                    }).SubscribeSafe(observer));
    }

    /// <summary>
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="readOnlyObservableCollection">The output <see cref="ReadOnlyObservableCollection{TObject}"/> that will be populated with the results.</param>
    /// <param name="options">The <see cref="BindingOptions"/> that controls binding behavior.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, BindingOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        var target = new ObservableCollectionExtended<TObject>();
        var result = new ReadOnlyObservableCollection<TObject>(target);
        var updater = new SortedObservableCollectionAdaptor<TObject, TKey>(options);
        readOnlyObservableCollection = result;
        return source.Bind(target, updater);
    }

    /// <summary>
    /// Binds the results to the specified readonly observable collection using the default update algorithm.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="readOnlyObservableCollection">The output <see cref="ReadOnlyObservableCollection{TObject}"/> that will be populated with the results.</param>
    /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection.</param>
    /// <param name="useReplaceForUpdates">When <see langword="true"/>, uses Replace instead of Remove/Add for updates in the bound collection. Not all platforms support replace notifications.</param>
    /// <param name="adaptor">An <see cref="IChangeSetAdaptor{TObject, TKey}"/> that specify an adaptor to change the algorithm to update the target collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold, bool useReplaceForUpdates = BindingOptions.DefaultUseReplaceForUpdates, ISortedObservableCollectionAdaptor<TObject, TKey>? adaptor = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold && useReplaceForUpdates == BindingOptions.DefaultUseReplaceForUpdates
            ? defaults
            : defaults with { ResetThreshold = resetThreshold, UseReplaceForUpdates = useReplaceForUpdates };

        adaptor ??= new SortedObservableCollectionAdaptor<TObject, TKey>(options);

        var target = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(target);
        return source.Bind(target, adaptor);
    }

#if SUPPORTS_BINDINGLIST
    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="TObject">The object type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="bindingList">The <see cref="BindingList{TObject}"/> that will receive the changes.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(bindingList);

        return source.Adapt(new BindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
    }

    /// <summary>
    /// Binds a clone of the observable change set to the target observable collection.
    /// </summary>
    /// <typeparam name="TObject">The object type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to bind to a collection.</param>
    /// <param name="bindingList">The <see cref="BindingList{TObject}"/> that will receive the changes.</param>
    /// <param name="resetThreshold">The reset threshold.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// targetCollection.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, BindingList<TObject> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(bindingList);

        return source.Adapt(new SortedBindingListAdaptor<TObject, TKey>(bindingList, resetThreshold));
    }
#endif
}
