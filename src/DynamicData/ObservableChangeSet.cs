// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// Creation methods for observable change sets.
/// </summary>
public static class ObservableChangeSet
{
    // Immediate delivers the task's outcome on the thread that completes the task. Without it, a task that completes
    // on a thread with a SynchronizationContext (a UI thread) hands its outcome to the thread pool, later.
    private static readonly TaskObservationOptions TaskObservation = new(ImmediateScheduler.Instance, ignoreExceptionsAfterUnsubscribe: true);

    /// <summary>
    /// Creates an observable cache from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Action> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Create(
            cache =>
            {
                var action = subscribe(cache);
                return Disposable.Create(() => action?.Invoke());
            },
            keySelector);
    }

    /// <summary>
    /// Creates an observable cache from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, IDisposable> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                var disposable = new SingleAssignmentDisposable();

                try
                {
                    disposable.Disposable = subscribe(cache);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(disposable, Disposable.Create(observer.OnCompleted), cache.Connect().SubscribeSafe(observer), cache);
            });
    }

    /// <summary>
    /// Creates an observable cache from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also disposes the <see cref="IDisposable"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="IDisposable"/> returned after the subscription has been disposed is disposed as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task<IDisposable>> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Create((cache, _) => subscribe(cache), keySelector);
    }

    /// <summary>
    /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also disposes the <see cref="IDisposable"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="IDisposable"/> returned after the subscription has been disposed is disposed as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task<IDisposable>> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return CreateFromTask<ISourceCache<TObject, TKey>, IChangeSet<TObject, TKey>, IDisposable>(() => new SourceCache<TObject, TKey>(keySelector), static cache => cache.Connect(), subscribe, static disposable => disposable);
    }

    /// <summary>
    /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also invokes the <see cref="Action"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="Action"/> returned after the subscription has been disposed is invoked as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task<Action>> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Create((cache, _) => subscribe(cache), keySelector);
    }

    /// <summary>
    /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also invokes the <see cref="Action"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="Action"/> returned after the subscription has been disposed is invoked as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task<Action>> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return CreateFromTask<ISourceCache<TObject, TKey>, IChangeSet<TObject, TKey>, Action>(() => new SourceCache<TObject, TKey>(keySelector), static cache => cache.Connect(), subscribe, static action => Disposable.Create(() => action?.Invoke()));
    }

    /// <summary>
    /// Creates an observable cache from a specified asynchronous Subscribe method.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Emitted when the task returned by <paramref name="subscribe"/> completes.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, Task> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return Create((cache, _) => subscribe(cache), keySelector);
    }

    /// <summary>
    /// Creates an observable cache from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="TObject">The type of the elements contained in the observable cache.</typeparam>
    /// <typeparam name="TKey">The type of the specified key.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable cache's Subscribe method. </param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The observable cache with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Emitted when the task returned by <paramref name="subscribe"/> completes.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Create<TObject, TKey>(Func<ISourceCache<TObject, TKey>, CancellationToken, Task> subscribe, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(keySelector);

        return CreateFromTask<ISourceCache<TObject, TKey>, IChangeSet<TObject, TKey>>(() => new SourceCache<TObject, TKey>(keySelector), static cache => cache.Connect(), subscribe);
    }

    /// <summary>
    /// Creates an observable list from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Action> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return Create<T>(
            list =>
            {
                var action = subscribe(list);
                return Disposable.Create(() => action?.Invoke());
            });
    }

    /// <summary>
    /// Creates an observable list from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, IDisposable> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var list = new SourceList<T>();
                IDisposable? disposeAction = null;

                try
                {
                    disposeAction = subscribe(list);
                }
                catch (Exception e)
                {
                    observer.OnError(e);
                }

                return new CompositeDisposable(
                    list.Connect().SubscribeSafe(observer),
                    list,
                    Disposable.Create(
                        () =>
                        {
                            observer.OnCompleted();
                            disposeAction?.Dispose();
                        }));
            });
    }

    /// <summary>
    /// Creates an observable list from a specified Subscribe method implementation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also disposes the <see cref="IDisposable"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="IDisposable"/> returned after the subscription has been disposed is disposed as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task<IDisposable>> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return Create<T>((list, _) => subscribe(list));
    }

    /// <summary>
    /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also disposes the <see cref="IDisposable"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="IDisposable"/> returned after the subscription has been disposed is disposed as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<IDisposable>> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return CreateFromTask<ISourceList<T>, IChangeSet<T>, IDisposable>(static () => new SourceList<T>(), static list => list.Connect(), subscribe, static disposable => disposable);
    }

    /// <summary>
    /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also invokes the <see cref="Action"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="Action"/> returned after the subscription has been disposed is invoked as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task<Action>> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return Create<T>((list, _) => subscribe(list));
    }

    /// <summary>
    /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Never emitted. The stream stays open until the subscription is disposed, which also invokes the <see cref="Action"/> returned by the task.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> An <see cref="Action"/> returned after the subscription has been disposed is invoked as soon as it arrives.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task<Action>> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return CreateFromTask<ISourceList<T>, IChangeSet<T>, Action>(static () => new SourceList<T>(), static list => list.Connect(), subscribe, static action => Disposable.Create(() => action?.Invoke()));
    }

    /// <summary>
    /// Creates an observable list from a specified asynchronous Subscribe method.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Emitted when the task returned by <paramref name="subscribe"/> completes.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, Task> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return Create<T>((list, _) => subscribe(list));
    }

    /// <summary>
    /// Creates an observable list from a specified cancellable asynchronous Subscribe method. The CancellationToken passed to the asynchronous Subscribe method is tied to the returned disposable subscription, allowing best-effort cancellation.
    /// </summary>
    /// <typeparam name="T">The type of the elements contained in the observable list.</typeparam>
    /// <param name="subscribe">  Implementation of the resulting observable list's Subscribe method. </param>
    /// <returns>The observable list with the specified implementation for the Subscribe method.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Emitted when <paramref name="subscribe"/> throws or its task fails.</description></item>
    /// <item><term>OnCompleted</term><description>Emitted when the task returned by <paramref name="subscribe"/> completes.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Create<T>(Func<ISourceList<T>, CancellationToken, Task> subscribe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(subscribe);

        return CreateFromTask<ISourceList<T>, IChangeSet<T>>(static () => new SourceList<T>(), static list => list.Connect(), subscribe);
    }

    // The task is the whole lifetime of the stream, the way it is for Observable.Create: the stream ends when the task does.
    private static IObservable<TChangeSet> CreateFromTask<TCollection, TChangeSet>(Func<TCollection> createCollection, Func<TCollection, IObservable<TChangeSet>> connect, Func<TCollection, CancellationToken, Task> subscribe)
        where TCollection : IDisposable =>
        Observable.Create<TChangeSet>(
            observer =>
            {
                var collection = createCollection();
                var changes = connect(collection).SubscribeSafe(observer);
                var loader = Observable.FromAsync(cancellationToken => subscribe(collection, cancellationToken), TaskObservation).SubscribeSafe(observer.OnError, observer.OnCompleted);

                return new CompositeDisposable(loader, changes, collection);
            });

    // The task only sets the stream up, so the stream stays open after it succeeds, until the subscription is disposed.
    private static IObservable<TChangeSet> CreateFromTask<TCollection, TChangeSet, TCleanUp>(Func<TCollection> createCollection, Func<TCollection, IObservable<TChangeSet>> connect, Func<TCollection, CancellationToken, Task<TCleanUp>> subscribe, Func<TCleanUp, IDisposable> toDisposable)
        where TCollection : IDisposable =>
        Observable.Create<TChangeSet>(
            observer =>
            {
                var collection = createCollection();
                var cancellation = new CancellationDisposable();
                var cleanUp = new SingleAssignmentDisposable();
                var changes = connect(collection).SubscribeSafe(observer);

                // Deliberately not disposed with the subscription, so that clean-up which arrives after unsubscribing is
                // still disposed. It ends when the task does, and by then nothing it delivers reaches the observer.
                _ = Observable.FromAsync(() => subscribe(collection, cancellation.Token), TaskObservation).SubscribeSafe(result => cleanUp.Disposable = toDisposable(result), observer.OnError);

                return new CompositeDisposable(cancellation, changes, cleanUp, collection);
            });
}
