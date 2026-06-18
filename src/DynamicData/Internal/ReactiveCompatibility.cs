// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

namespace DynamicData.Internal;

/// <summary>
/// Compatibility helpers for the subset of Rx APIs used internally by DynamicData.
/// </summary>
internal static class ReactiveCompatibility;

/// <summary>
/// Creates disposable instances compatible with the old Rx helper surface.
/// </summary>
internal static class Disposable
{
    /// <summary>
    /// Gets a disposable that does nothing when disposed.
    /// </summary>
    public static IDisposable Empty { get; } = ReactiveUI.Primitives.Disposables.EmptyDisposable.Instance;

    /// <summary>
    /// Creates a disposable that invokes the supplied action once.
    /// </summary>
    /// <param name="dispose">The action to invoke.</param>
    /// <returns>A disposable wrapper.</returns>
    public static IDisposable Create(Action dispose) => ReactiveUI.Primitives.Disposables.Scope.Create(dispose);

    /// <summary>
    /// Creates a disposable that invokes the supplied action once with state.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="state">The state passed to the action.</param>
    /// <param name="dispose">The action to invoke.</param>
    /// <returns>A disposable wrapper.</returns>
    public static IDisposable Create<TState>(TState state, Action<TState> dispose) =>
        ReactiveUI.Primitives.Disposables.Scope.Create(() => dispose(state));
}

/// <summary>
/// Scheduler aliases compatible with the old Rx static helper surface.
/// </summary>
internal static class Scheduler
{
    /// <summary>
    /// Gets the default sequencer.
    /// </summary>
    public static IScheduler Default => Sequencer.Default;

    /// <summary>
    /// Normalizes negative due times to zero.
    /// </summary>
    public static TimeSpan Normalize(TimeSpan dueTime) => Sequencer.Normalize(dueTime);
}

/// <summary>
/// A mutable collection of disposables disposed as a group.
/// </summary>
internal sealed class CompositeDisposable : ReactiveUI.Primitives.Disposables.MultipleDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposable"/> class.
    /// </summary>
    public CompositeDisposable()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeDisposable"/> class.
    /// </summary>
    /// <param name="disposables">Initial disposables.</param>
    public CompositeDisposable(params IDisposable[] disposables)
    {
        foreach (var disposable in disposables)
        {
            Add(disposable);
        }
    }
}

/// <summary>
/// A disposable whose inner disposable can be assigned once.
/// </summary>
internal sealed class SingleAssignmentDisposable : IDisposable
{
    private readonly object _locker = new();
    private IDisposable? _disposable;
    private bool _disposed;
    private bool _assigned;

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_locker)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Gets or sets the assigned disposable.
    /// </summary>
    public IDisposable? Disposable
    {
        get
        {
            lock (_locker)
            {
                return _disposable;
            }
        }

        set
        {
            IDisposable? toDispose = null;
            lock (_locker)
            {
                if (_assigned)
                {
                    throw new InvalidOperationException("Disposable has already been assigned.");
                }

                _assigned = true;
                if (_disposed)
                {
                    toDispose = value;
                }
                else
                {
                    _disposable = value;
                }
            }

            toDispose?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IDisposable? toDispose = null;
        lock (_locker)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = _disposable;
            _disposable = null;
        }

        toDispose?.Dispose();
    }
}

/// <summary>
/// A disposable whose inner disposable can be replaced.
/// </summary>
internal sealed class SerialDisposable : IDisposable
{
    private readonly object _locker = new();
    private IDisposable? _disposable;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_locker)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Gets or sets the current disposable, disposing the previous value on replacement.
    /// </summary>
    public IDisposable? Disposable
    {
        get
        {
            lock (_locker)
            {
                return _disposable;
            }
        }

        set
        {
            IDisposable? previous;
            lock (_locker)
            {
                if (_disposed)
                {
                    previous = value;
                    value = null;
                }
                else
                {
                    previous = _disposable;
                    _disposable = value;
                }
            }

            previous?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IDisposable? toDispose = null;
        lock (_locker)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = _disposable;
            _disposable = null;
        }

        toDispose?.Dispose();
    }
}

/// <summary>
/// Represents a subject that is both an observer and observable.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal interface ISubject<T> : IObservable<T>, IObserver<T>
{
    /// <summary>
    /// Gets a value indicating whether the subject currently has observers.
    /// </summary>
    bool HasObservers { get; }

    /// <summary>
    /// Gets a value indicating whether the subject has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}

/// <summary>
/// A multicast subject.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class Subject<T> : ISubject<T>, IDisposable
{
    private readonly object _locker = new();
    private readonly List<IObserver<T>> _observers = [];
    private Exception? _error;
    private bool _completed;
    private bool _disposed;

    /// <inheritdoc />
    public bool HasObservers
    {
        get
        {
            lock (_locker)
            {
                return _observers.Count != 0;
            }
        }
    }

    /// <inheritdoc />
    public bool IsDisposed
    {
        get
        {
            lock (_locker)
            {
                return _disposed;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_locker)
        {
            _disposed = true;
            _observers.Clear();
        }
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
        IObserver<T>[] observers;
        lock (_locker)
        {
            if (_disposed || _completed || _error is not null)
            {
                return;
            }

            _completed = true;
            observers = [.. _observers];
            _observers.Clear();
        }

        foreach (var observer in observers)
        {
            observer.OnCompleted();
        }
    }

    /// <inheritdoc />
    public void OnError(Exception error)
    {
        IObserver<T>[] observers;
        lock (_locker)
        {
            if (_disposed || _completed || _error is not null)
            {
                return;
            }

            _error = error;
            observers = [.. _observers];
            _observers.Clear();
        }

        foreach (var observer in observers)
        {
            observer.OnError(error);
        }
    }

    /// <inheritdoc />
    public void OnNext(T value)
    {
        IObserver<T>[] observers;
        lock (_locker)
        {
            if (_disposed || _completed || _error is not null)
            {
                return;
            }

            observers = [.. _observers];
        }

        foreach (var observer in observers)
        {
            observer.OnNext(value);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        lock (_locker)
        {
            if (_error is not null)
            {
                observer.OnError(_error);
                return Disposable.Empty;
            }

            if (_completed)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            if (_disposed)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            _observers.Add(observer);
        }

        return Disposable.Create(() =>
        {
            lock (_locker)
            {
                _observers.Remove(observer);
            }
        });
    }
}

/// <summary>
/// A subject that replays its most recent value.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class BehaviorSubject<T> : ISubject<T>, IDisposable
{
    private readonly BehaviorSignal<T> _signal;

    /// <summary>
    /// Initializes a new instance of the <see cref="BehaviorSubject{T}"/> class.
    /// </summary>
    /// <param name="value">The initial value.</param>
    public BehaviorSubject(T value) => _signal = new(value);

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public T Value => _signal.Value;

    /// <inheritdoc />
    public bool HasObservers => _signal.HasObservers;

    /// <inheritdoc />
    public bool IsDisposed => _signal.IsDisposed;

    /// <inheritdoc />
    public void Dispose() => _signal.Dispose();

    /// <inheritdoc />
    public void OnCompleted() => _signal.OnCompleted();

    /// <inheritdoc />
    public void OnError(Exception error) => _signal.OnError(error);

    /// <inheritdoc />
    public void OnNext(T value) => _signal.OnNext(value);

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer) => _signal.Subscribe(observer);
}

/// <summary>
/// A subject that replays buffered values to new subscribers.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class ReplaySubject<T> : ISubject<T>, IDisposable
{
    private readonly object _locker = new();
    private readonly List<T> _values = [];
    private readonly List<IObserver<T>> _observers = [];
    private readonly int? _bufferSize;
    private Exception? _error;
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySubject{T}"/> class.
    /// </summary>
    public ReplaySubject()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySubject{T}"/> class.
    /// </summary>
    /// <param name="bufferSize">The maximum number of values to replay.</param>
    public ReplaySubject(int bufferSize)
    {
        if (bufferSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }

        _bufferSize = bufferSize;
    }

    /// <inheritdoc />
    public bool HasObservers
    {
        get
        {
            lock (_locker)
            {
                return _observers.Count != 0;
            }
        }
    }

    /// <inheritdoc />
    public bool IsDisposed
    {
        get
        {
            lock (_locker)
            {
                return _disposed;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_locker)
        {
            _disposed = true;
            _observers.Clear();
            _values.Clear();
        }
    }

    /// <inheritdoc />
    public void OnCompleted()
    {
        IObserver<T>[] observers;
        lock (_locker)
        {
            ThrowIfDisposed();
            if (_completed || _error is not null)
            {
                return;
            }

            _completed = true;
            observers = [.. _observers];
            _observers.Clear();
        }

        foreach (var observer in observers)
        {
            observer.OnCompleted();
        }
    }

    /// <inheritdoc />
    public void OnError(Exception error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        IObserver<T>[] observers;
        lock (_locker)
        {
            ThrowIfDisposed();
            if (_completed || _error is not null)
            {
                return;
            }

            _error = error;
            observers = [.. _observers];
            _observers.Clear();
        }

        foreach (var observer in observers)
        {
            observer.OnError(error);
        }
    }

    /// <inheritdoc />
    public void OnNext(T value)
    {
        IObserver<T>[] observers;
        lock (_locker)
        {
            ThrowIfDisposed();
            if (_completed || _error is not null)
            {
                return;
            }

            _values.Add(value);
            if (_bufferSize is { } bufferSize)
            {
                while (_values.Count > bufferSize)
                {
                    _values.RemoveAt(0);
                }
            }

            observers = [.. _observers];
        }

        foreach (var observer in observers)
        {
            observer.OnNext(value);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (observer is null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        T[] values;
        Exception? error;
        var completed = false;

        lock (_locker)
        {
            ThrowIfDisposed();
            values = [.. _values];
            error = _error;
            completed = _completed;
            if (error is null && !completed)
            {
                _observers.Add(observer);
            }
        }

        foreach (var value in values)
        {
            observer.OnNext(value);
        }

        if (error is not null)
        {
            observer.OnError(error);
            return Disposable.Empty;
        }

        if (completed)
        {
            observer.OnCompleted();
            return Disposable.Empty;
        }

        return Disposable.Create(() =>
        {
            lock (_locker)
            {
                _observers.Remove(observer);
            }
        });
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}

/// <summary>
/// Factory methods compatible with the subset of the old Rx Observable surface used internally.
/// </summary>
internal static class Observable
{
    /// <summary>
    /// Creates an observable from a subscribe function.
    /// </summary>
    public static IObservable<T> Create<T>(Func<IObserver<T>, IDisposable> subscribe) =>
        new AnonymousObservable<T>(subscribe);

    /// <summary>
    /// Creates an observable from an asynchronous subscribe function.
    /// </summary>
    public static IObservable<T> Create<T>(Func<IObserver<T>, Task<IDisposable>> subscribe) =>
        Create<T>(observer =>
        {
            var subscription = new SerialDisposable();
            try
            {
                var task = subscribe(observer);
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    subscription.Disposable = task.Result;
                }
                else
                {
                    _ = AwaitSubscription(task, observer, subscription);
                }
            }
            catch (Exception error)
            {
                observer.OnError(error);
            }

            return subscription;
        });

    /// <summary>
    /// Creates an observable from an asynchronous subscribe function.
    /// </summary>
    public static IObservable<T> Create<T>(Func<IObserver<T>, CancellationToken, Task<IDisposable>> subscribe) =>
        Create<T>(observer =>
        {
            var cancellation = new CancellationTokenSource();
            var subscription = new CompositeDisposable(cancellation);
            try
            {
                var task = subscribe(observer, cancellation.Token);
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    subscription.Add(task.Result);
                }
                else
                {
                    _ = AwaitSubscription(task, observer, subscription, cancellation.Token);
                }
            }
            catch (Exception error)
            {
                if (!cancellation.IsCancellationRequested)
                {
                    observer.OnError(error);
                }
            }

            return subscription;
        });

    /// <summary>
    /// Defers observable creation until subscription.
    /// </summary>
    public static IObservable<T> Defer<T>(Func<IObservable<T>> factory) =>
        ReactiveUI.Primitives.Signals.Signal.Lazy(factory);

    /// <summary>
    /// Defers asynchronous value creation until subscription.
    /// </summary>
    public static IObservable<T> Defer<T>(Func<Task<T>> factory) => FromAsync(factory);

    /// <summary>
    /// Defers asynchronous observable creation until subscription.
    /// </summary>
    public static IObservable<T> Defer<T>(Func<Task<IObservable<T>>> factory) =>
        FromAsync(factory).Switch();

    /// <summary>
    /// Creates an observable that completes immediately.
    /// </summary>
    public static IObservable<T> Empty<T>() => ReactiveUI.Primitives.Signals.Signal.None<T>();

    /// <summary>
    /// Creates an observable that never completes.
    /// </summary>
    public static IObservable<T> Never<T>() => Create<T>(_ => Disposable.Empty);

    /// <summary>
    /// Creates an observable that emits a single value.
    /// </summary>
    public static IObservable<T> Return<T>(T value) => ReactiveUI.Primitives.Signals.Signal.Emit(value);

    /// <summary>
    /// Creates an observable that terminates with an error.
    /// </summary>
    public static IObservable<T> Throw<T>(Exception error) => ReactiveUI.Primitives.Signals.Signal.Fail<T>(error);

    /// <summary>
    /// Creates an observable range.
    /// </summary>
    public static IObservable<int> Range(int start, int count) => ReactiveUI.Primitives.Signals.Signal.Sequence(start, count);

    /// <summary>
    /// Creates an observable range on a sequencer.
    /// </summary>
    public static IObservable<int> Range(int start, int count, IScheduler scheduler) =>
        Enumerable.Range(start, count).ToObservable(scheduler);

    /// <summary>
    /// Creates an observable from an asynchronous value factory.
    /// </summary>
    public static IObservable<T> FromAsync<T>(Func<Task<T>> taskFactory) =>
        Observable.Create<T>(observer =>
        {
            try
            {
                return taskFactory().ToObservable().Subscribe(observer);
            }
            catch (Exception error)
            {
                observer.OnError(error);
                return Disposable.Empty;
            }
        });

    /// <summary>
    /// Creates an observable from an asynchronous value factory.
    /// </summary>
    public static IObservable<T> FromAsync<T>(Func<CancellationToken, Task<T>> taskFactory) =>
        Observable.Create<T>(observer =>
        {
            var cancellation = new CancellationTokenSource();
            var subscription = new SerialDisposable();

            try
            {
                subscription.Disposable = taskFactory(cancellation.Token).ToObservable().Subscribe(observer);
            }
            catch (Exception error)
            {
                observer.OnError(error);
            }

            return Disposable.Create(() =>
            {
                cancellation.Cancel();
                cancellation.Dispose();
                subscription.Dispose();
            });
        });

    /// <summary>
    /// Creates an observable from an asynchronous action factory.
    /// </summary>
    public static IObservable<Unit> FromAsync(Func<Task> taskFactory) =>
        FromAsync(async () =>
        {
            await taskFactory().ConfigureAwait(false);
            return Unit.Default;
        });

    /// <summary>
    /// Creates a timer observable.
    /// </summary>
    public static IObservable<long> Timer(TimeSpan dueTime) => ReactiveUI.Primitives.Signals.Signal.After(dueTime);

    /// <summary>
    /// Creates a timer observable.
    /// </summary>
    public static IObservable<long> Timer(TimeSpan dueTime, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.After(dueTime, scheduler);

    /// <summary>
    /// Creates a timer observable.
    /// </summary>
    public static IObservable<long> Timer(TimeSpan dueTime, TimeSpan period, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.After(dueTime, period, scheduler);

    /// <summary>
    /// Creates an observable that emits a sequential value at each interval.
    /// </summary>
    public static IObservable<long> Interval(TimeSpan period) =>
        ReactiveUI.Primitives.Signals.Signal.Every(period);

    /// <summary>
    /// Creates an observable that emits a sequential value at each interval.
    /// </summary>
    public static IObservable<long> Interval(TimeSpan period, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.Every(period, scheduler);

    /// <summary>
    /// Merges observable sources.
    /// </summary>
    public static IObservable<T> Merge<T>(params IObservable<T>[] sources) => sources.Merge();

    /// <summary>
    /// Merges observable sources.
    /// </summary>
    public static IObservable<T> Merge<T>(IEnumerable<IObservable<T>> sources) => sources.Merge();

    /// <summary>
    /// Concatenates observable sources.
    /// </summary>
    public static IObservable<T> Concat<T>(params IObservable<T>[] sources) => sources.ToObservable().Concat();

    /// <summary>
    /// Concatenates observable sources.
    /// </summary>
    public static IObservable<T> Concat<T>(IEnumerable<IObservable<T>> sources) => sources.ToObservable().Concat();

    /// <summary>
    /// Switches to the latest inner observable.
    /// </summary>
    public static IObservable<T> Switch<T>(IObservable<IObservable<T>> source) => source.Switch();

    /// <summary>
    /// Projects values to enumerable sequences.
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TResult>(
        IObservable<TSource> source,
        Func<TSource, IEnumerable<TResult>> selector) =>
        source.SelectMany(selector);

    /// <summary>
    /// Converts an event into event-pattern values.
    /// </summary>
    public static IObservable<EventPattern<TEventArgs>> FromEventPattern<TEventHandler, TEventArgs>(
        Action<TEventHandler> addHandler,
        Action<TEventHandler> removeHandler)
        where TEventHandler : Delegate
        where TEventArgs : EventArgs =>
        Create<EventPattern<TEventArgs>>(observer =>
        {
            if (addHandler is null)
            {
                throw new ArgumentNullException(nameof(addHandler));
            }

            if (removeHandler is null)
            {
                throw new ArgumentNullException(nameof(removeHandler));
            }

            void Handler(object? sender, TEventArgs eventArgs) =>
                observer.OnNext(new EventPattern<TEventArgs>(sender, eventArgs));

            Action<object?, TEventArgs> action = Handler;
            var handler = (TEventHandler)Delegate.CreateDelegate(typeof(TEventHandler), action.Target, action.Method);
            addHandler(handler);
            return Disposable.Create(handler, removeHandler);
        });

    private static async Task AwaitSubscription<T>(
        Task<IDisposable> task,
        IObserver<T> observer,
        SerialDisposable subscription)
    {
        try
        {
            subscription.Disposable = await task.ConfigureAwait(false);
        }
        catch (Exception error)
        {
            observer.OnError(error);
        }
    }

    private static async Task AwaitSubscription<T>(
        Task<IDisposable> task,
        IObserver<T> observer,
        CompositeDisposable subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            subscription.Add(await task.ConfigureAwait(false));
        }
        catch (Exception error)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                observer.OnError(error);
            }
        }
    }
}

/// <summary>
/// Observer factory helpers.
/// </summary>
internal static class Observer
{
    /// <summary>
    /// Creates an observer from callbacks.
    /// </summary>
    public static IObserver<T> Create<T>(Action<T> onNext) =>
        new AnonymousObserver<T>(onNext, Throw, static () => { });

    /// <summary>
    /// Creates an observer from callbacks.
    /// </summary>
    public static IObserver<T> Create<T>(Action<T> onNext, Action<Exception> onError) =>
        new AnonymousObserver<T>(onNext, onError, static () => { });

    /// <summary>
    /// Creates an observer from callbacks.
    /// </summary>
    public static IObserver<T> Create<T>(Action<T> onNext, Action onCompleted) =>
        new AnonymousObserver<T>(onNext, Throw, onCompleted);

    /// <summary>
    /// Creates an observer from callbacks.
    /// </summary>
    public static IObserver<T> Create<T>(Action<T> onNext, Action<Exception> onError, Action onCompleted) =>
        new AnonymousObserver<T>(onNext, onError, onCompleted);

    private static void Throw(Exception error) => ExceptionDispatchInfo.Capture(error).Throw();
}

/// <summary>
/// Observer implementation backed by delegates.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class AnonymousObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception> _onError;
    private readonly Action _onCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObserver{T}"/> class.
    /// </summary>
    public AnonymousObserver(Action<T> onNext, Action<Exception> onError, Action onCompleted)
    {
        _onNext = onNext;
        _onError = onError;
        _onCompleted = onCompleted;
    }

    /// <inheritdoc />
    public void OnCompleted() => _onCompleted();

    /// <inheritdoc />
    public void OnError(Exception error) => _onError(error);

    /// <inheritdoc />
    public void OnNext(T value) => _onNext(value);
}

/// <summary>
/// Observable implementation backed by a subscribe delegate.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class AnonymousObservable<T> : IObservable<T>
{
    private readonly Func<IObserver<T>, IDisposable> _subscribe;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousObservable{T}"/> class.
    /// </summary>
    public AnonymousObservable(Func<IObserver<T>, IDisposable> subscribe) => _subscribe = subscribe;

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer)
    {
        try
        {
            return _subscribe(observer) ?? Disposable.Empty;
        }
        catch (Exception error)
        {
            observer.OnError(error);
            return Disposable.Empty;
        }
    }
}

/// <summary>
/// Rx-style extension methods used by DynamicData internals.
/// </summary>
internal static class ObservableCompatibilityExtensions
{
    /// <summary>
    /// Subscribes with no value handler.
    /// </summary>
    public static IDisposable Subscribe<T>(this IObservable<T> source) =>
        source.Subscribe(Observer.Create<T>(static _ => { }));

    /// <summary>
    /// Subscribes with a value handler.
    /// </summary>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext) =>
        source.Subscribe(Observer.Create(onNext));

    /// <summary>
    /// Subscribes with value and error handlers.
    /// </summary>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError) =>
        source.Subscribe(Observer.Create(onNext, onError));

    /// <summary>
    /// Subscribes with value and completion handlers.
    /// </summary>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action onCompleted) =>
        source.Subscribe(Observer.Create(onNext, onCompleted));

    /// <summary>
    /// Subscribes with value, error, and completion handlers.
    /// </summary>
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError, Action onCompleted) =>
        source.Subscribe(Observer.Create(onNext, onError, onCompleted));

    /// <summary>
    /// Subscribes an observer to the source.
    /// </summary>
    public static IDisposable SubscribeSafe<T>(this IObservable<T> source, IObserver<T> observer)
    {
        var subscription = new SerialDisposable();
        var stopped = 0;

        void StopWithError(Exception error)
        {
            if (Interlocked.Exchange(ref stopped, 1) == 0)
            {
                try
                {
                    observer.OnError(error);
                }
                finally
                {
                    subscription.Dispose();
                }
            }
        }

        subscription.Disposable = source.Subscribe(
            value =>
            {
                if (Volatile.Read(ref stopped) != 0)
                {
                    return;
                }

                try
                {
                    observer.OnNext(value);
                }
                catch (Exception error)
                {
                    StopWithError(error);
                }
            },
            error =>
            {
                StopWithError(error);
            },
            () =>
            {
                if (Interlocked.Exchange(ref stopped, 1) == 0)
                {
                    try
                    {
                        observer.OnCompleted();
                    }
                    finally
                    {
                        subscription.Dispose();
                    }
                }
            });

        return subscription;
    }

    /// <summary>
    /// Hides the identity of an observable.
    /// </summary>
    public static IObservable<T> AsObservable<T>(this IObservable<T> source) =>
        Observable.Create<T>(source.Subscribe);

    /// <summary>
    /// Projects each value.
    /// </summary>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector) =>
        ReactiveUI.Primitives.Signals.Signal.Create<TResult>(observer =>
            source.Subscribe(
                value =>
                {
                    TResult result;
                    try
                    {
                        result = selector(value);
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                        return;
                    }

                    observer.OnNext(result);
                },
                observer.OnError,
                observer.OnCompleted));

    /// <summary>
    /// Projects each value with its index.
    /// </summary>
    public static IObservable<TResult> Select<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, int, TResult> selector)
        => Observable.Create<TResult>(observer =>
        {
            var index = -1;
            return source.Subscribe(
                value =>
                {
                    TResult result;
                    try
                    {
                        result = selector(value, Interlocked.Increment(ref index));
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                        return;
                    }

                    observer.OnNext(result);
                },
                observer.OnError,
                observer.OnCompleted);
        });

    /// <summary>
    /// Filters values.
    /// </summary>
    public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
            source.Subscribe(
                value =>
                {
                    bool keep;
                    try
                    {
                        keep = predicate(value);
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                        return;
                    }

                    if (keep)
                    {
                        observer.OnNext(value);
                    }
                },
                observer.OnError,
                observer.OnCompleted));

    /// <summary>
    /// Projects values to observable sequences and merges the results.
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IObservable<TResult>> selector) =>
        source.Select(selector).Merge();

    /// <summary>
    /// Projects each value to the same observable sequence and merges the results.
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        IObservable<TResult> other) =>
        source.Select(_ => other).Merge();

    /// <summary>
    /// Projects values to enumerable sequences and merges the results.
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IEnumerable<TResult>> selector) =>
        ReactiveUI.Primitives.Signals.Signal.Create<TResult>(observer =>
            source.Subscribe(
                value =>
                {
                    IEnumerable<TResult> values;
                    try
                    {
                        values = selector(value);
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                        return;
                    }

                    foreach (var result in values)
                    {
                        observer.OnNext(result);
                    }
                },
                observer.OnError,
                observer.OnCompleted));

    /// <summary>
    /// Projects values with a result selector.
    /// </summary>
    public static IObservable<TResult> SelectMany<TSource, TCollection, TResult>(
        this IObservable<TSource> source,
        Func<TSource, IObservable<TCollection>> collectionSelector,
        Func<TSource, TCollection, TResult> resultSelector) =>
        source.SelectMany(value => collectionSelector(value).Select(inner => resultSelector(value, inner)));

    /// <summary>
    /// Merges two observable sources.
    /// </summary>
    public static IObservable<T> Merge<T>(this IObservable<T> first, IObservable<T> second) =>
        new[] { first, second }.Merge();

    /// <summary>
    /// Merges observable sources.
    /// </summary>
    public static IObservable<T> Merge<T>(this IEnumerable<IObservable<T>> sources) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var disposables = new CompositeDisposable();
            var remaining = 1;
            var gate = new object();
            var stopped = false;

            void CompleteOne()
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    if (--remaining == 0)
                    {
                        stopped = true;
                        observer.OnCompleted();
                    }
                }
            }

            void Fail(Exception error)
            {
                var shouldDispose = false;
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                    observer.OnError(error);
                    shouldDispose = true;
                }

                if (shouldDispose)
                {
                    disposables.Dispose();
                }
            }

            foreach (var source in sources)
            {
                lock (gate)
                {
                    if (stopped)
                    {
                        break;
                    }

                    remaining++;
                }

                disposables.Add(source.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            if (!stopped)
                            {
                                observer.OnNext(value);
                            }
                        }
                    },
                    Fail,
                    CompleteOne));
            }

            CompleteOne();
            return disposables;
        });

    /// <summary>
    /// Merges a sequence of observable sources.
    /// </summary>
    public static IObservable<T> Merge<T>(this IObservable<IObservable<T>> sources) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            var outerCompleted = false;
            var active = 0;
            var gate = new object();
            var stopped = false;

            void Fail(Exception error)
            {
                var shouldDispose = false;
                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    stopped = true;
                    observer.OnError(error);
                    shouldDispose = true;
                }

                if (shouldDispose)
                {
                    subscriptions.Dispose();
                }
            }

            void TryComplete()
            {
                if (!stopped && outerCompleted && active == 0)
                {
                    stopped = true;
                    observer.OnCompleted();
                }
            }

            subscriptions.Add(sources.Subscribe(
                inner =>
                {
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        active++;
                    }

                    var innerSubscription = new SingleAssignmentDisposable();
                    subscriptions.Add(innerSubscription);
                    innerSubscription.Disposable = inner.Subscribe(
                        value =>
                        {
                            lock (gate)
                            {
                                if (!stopped)
                                {
                                    observer.OnNext(value);
                                }
                            }
                        },
                        Fail,
                        () =>
                        {
                            subscriptions.Remove(innerSubscription);
                            lock (gate)
                            {
                                if (stopped)
                                {
                                    return;
                                }

                                active--;
                                TryComplete();
                            }
                        });
                },
                Fail,
                () =>
                {
                    lock (gate)
                    {
                        if (stopped)
                        {
                            return;
                        }

                        outerCompleted = true;
                        TryComplete();
                    }
                }));

            return subscriptions;
        });

    /// <summary>
    /// Merges observable sources with a maximum concurrency.
    /// </summary>
    public static IObservable<T> Merge<T>(this IEnumerable<IObservable<T>> sources, int maxConcurrent)
    {
        if (maxConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
        }

        return ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            var enumerator = sources.GetEnumerator();
            var gate = new object();
            var active = 0;
            var stopped = false;

            bool SubscribeNext()
            {
                IObservable<T>? next = null;
                lock (gate)
                {
                    if (stopped)
                    {
                        return false;
                    }

                    if (enumerator.MoveNext())
                    {
                        next = enumerator.Current;
                        active++;
                    }
                    else if (active == 0)
                    {
                        stopped = true;
                        observer.OnCompleted();
                        return false;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (next is null)
                {
                    return false;
                }

                var inner = new SingleAssignmentDisposable();
                subscriptions.Add(inner);
                inner.Disposable = next.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            if (!stopped)
                            {
                                observer.OnNext(value);
                            }
                        }
                    },
                    error =>
                    {
                        lock (gate)
                        {
                            if (stopped)
                            {
                                return;
                            }

                            stopped = true;
                        }

                        observer.OnError(error);
                        subscriptions.Dispose();
                    },
                    () =>
                    {
                        subscriptions.Remove(inner);
                        lock (gate)
                        {
                            active--;
                        }

                        SubscribeNext();
                    });

                return true;
            }

            for (var i = 0; i < maxConcurrent; i++)
            {
                if (!SubscribeNext())
                {
                    break;
                }
            }

            subscriptions.Add(enumerator);
            return subscriptions;
        });
    }

    /// <summary>
    /// Concatenates observable sources.
    /// </summary>
    public static IObservable<T> Concat<T>(this IObservable<T> first, IObservable<T> second) =>
        new[] { first, second }.ToObservable().Concat();

    /// <summary>
    /// Concatenates observable sources.
    /// </summary>
    public static IObservable<T> Concat<T>(this IObservable<IObservable<T>> sources) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            var queue = new Queue<IObservable<T>>();
            var gate = new object();
            var outerCompleted = false;
            var active = false;

            void Drain()
            {
                IObservable<T>? next = null;
                lock (gate)
                {
                    if (active || queue.Count == 0)
                    {
                        if (outerCompleted && !active && queue.Count == 0)
                        {
                            observer.OnCompleted();
                        }

                        return;
                    }

                    active = true;
                    next = queue.Dequeue();
                }

                var inner = new SingleAssignmentDisposable();
                subscriptions.Add(inner);
                inner.Disposable = next.Subscribe(
                    observer.OnNext,
                    observer.OnError,
                    () =>
                    {
                        subscriptions.Remove(inner);
                        lock (gate)
                        {
                            active = false;
                        }

                        Drain();
                    });
            }

            subscriptions.Add(sources.Subscribe(
                source =>
                {
                    lock (gate)
                    {
                        queue.Enqueue(source);
                    }

                    Drain();
                },
                observer.OnError,
                () =>
                {
                    lock (gate)
                    {
                        outerCompleted = true;
                    }

                    Drain();
                }));

            return subscriptions;
        });

    /// <summary>
    /// Concatenates task results in source order.
    /// </summary>
    public static IObservable<T> Concat<T>(this IObservable<Task<T>> source) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var disposables = new CompositeDisposable();
            var gate = new object();
            var queue = new Queue<Task<T>>();
            var outerCompleted = false;
            var active = false;

            void Drain()
            {
                Task<T>? next = null;
                lock (gate)
                {
                    if (active)
                    {
                        return;
                    }

                    if (queue.Count == 0)
                    {
                        if (outerCompleted)
                        {
                            observer.OnCompleted();
                        }

                        return;
                    }

                    active = true;
                    next = queue.Dequeue();
                }

                void Finish()
                {
                    lock (gate)
                    {
                        active = false;
                    }

                    Drain();
                }

                if (next.IsCompleted)
                {
                    EmitCompletedTask(next, observer, Finish);
                }
                else
                {
                    _ = AwaitTask(next, observer, Finish);
                }
            }

            disposables.Add(source.Subscribe(
                task =>
                {
                    lock (gate)
                    {
                        queue.Enqueue(task);
                    }

                    Drain();
                },
                observer.OnError,
                () =>
                {
                    lock (gate)
                    {
                        outerCompleted = true;
                    }

                    Drain();
                }));

            return disposables;
        });

    private static async Task AwaitTask<T>(Task<T> task, IObserver<T> observer, Action completed)
    {
        try
        {
            observer.OnNext(await task.ConfigureAwait(false));
        }
        catch (Exception error)
        {
            observer.OnError(error);
            return;
        }

        completed();
    }

    private static void EmitCompletedTask<T>(Task<T> task, IObserver<T> observer, Action completed)
    {
        if (task.IsCanceled)
        {
            observer.OnError(new TaskCanceledException(task));
            return;
        }

        if (task.IsFaulted)
        {
            observer.OnError(task.Exception?.InnerException ?? (Exception?)task.Exception ?? new InvalidOperationException("Task faulted without an exception."));
            return;
        }

        observer.OnNext(task.Result);
        completed();
    }

    /// <summary>
    /// Switches to the latest inner observable.
    /// </summary>
    public static IObservable<T> Switch<T>(this IObservable<IObservable<T>> sources) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var subscriptions = new CompositeDisposable();
            var current = new SerialDisposable();
            var gate = new object();
            var outerCompleted = false;
            var active = 0;
            subscriptions.Add(current);

            void TryComplete()
            {
                if (outerCompleted && active == 0)
                {
                    observer.OnCompleted();
                }
            }

            subscriptions.Add(sources.Subscribe(
                inner =>
                {
                    Interlocked.Increment(ref active);
                    var mine = active;
                    current.Disposable = inner.Subscribe(
                        observer.OnNext,
                        observer.OnError,
                        () =>
                        {
                            if (Volatile.Read(ref active) == mine)
                            {
                                Interlocked.Decrement(ref active);
                                lock (gate)
                                {
                                    TryComplete();
                                }
                            }
                        });
                },
                observer.OnError,
                () =>
                {
                    lock (gate)
                    {
                        outerCompleted = true;
                        TryComplete();
                    }
                }));

            return subscriptions;
        });

    /// <summary>
    /// Prefixes values to an observable.
    /// </summary>
    public static IObservable<T> StartWith<T>(this IObservable<T> source, params T[] values) =>
        values.ToObservable().Concat(source);

    /// <summary>
    /// Prefixes a value to an observable.
    /// </summary>
    public static IObservable<T> Prepend<T>(this IObservable<T> source, T value) =>
        source.StartWith(value);

    /// <summary>
    /// Accumulates values.
    /// </summary>
    public static IObservable<TAccumulate> Scan<TSource, TAccumulate>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator)
        => Observable.Create<TAccumulate>(observer =>
        {
            var current = seed;
            return source.Subscribe(
                value =>
                {
                    try
                    {
                        current = accumulator(current, value);
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                        return;
                    }

                    observer.OnNext(current);
                },
                observer.OnError,
                observer.OnCompleted);
        });

    /// <summary>
    /// Buffers values by count.
    /// </summary>
    public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, int count) =>
        ReactiveUI.Primitives.Signals.Signal.Create<IList<T>>(observer =>
        {
            var buffer = new List<T>(count);
            return source.Subscribe(
                value =>
                {
                    buffer.Add(value);
                    if (buffer.Count < count)
                    {
                        return;
                    }

                    observer.OnNext(buffer.ToArray());
                    buffer.Clear();
                },
                observer.OnError,
                () =>
                {
                    if (buffer.Count != 0)
                    {
                        observer.OnNext(buffer.ToArray());
                    }

                    observer.OnCompleted();
                });
        });

    /// <summary>
    /// Buffers values by time.
    /// </summary>
    public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan) =>
        source.Buffer(timeSpan, Sequencer.Default);

    /// <summary>
    /// Buffers values by time.
    /// </summary>
    public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, TimeSpan timeSpan, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.Create<IList<T>>(observer =>
        {
            var gate = new object();
            var buffer = new List<T>();
            var disposables = new CompositeDisposable();

            void Flush()
            {
                T[] values;
                lock (gate)
                {
                    if (buffer.Count == 0)
                    {
                        return;
                    }

                    values = [.. buffer];
                    buffer.Clear();
                }

                observer.OnNext(values);
            }

            disposables.Add(source.Subscribe(
                value =>
                {
                    lock (gate)
                    {
                        buffer.Add(value);
                    }
                },
                observer.OnError,
                () =>
                {
                    Flush();
                    observer.OnCompleted();
                }));

            disposables.Add(scheduler.ScheduleRecurringAction(timeSpan, Flush));
            return disposables;
        });

    /// <summary>
    /// Distinct values.
    /// </summary>
    public static IObservable<T> Distinct<T>(this IObservable<T> source) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var seen = new HashSet<T>();
            return source.Subscribe(
                value =>
                {
                    if (seen.Add(value))
                    {
                        observer.OnNext(value);
                    }
                },
                observer.OnError,
                observer.OnCompleted);
        });

    /// <summary>
    /// Emits values when they differ from the previous value.
    /// </summary>
    public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source) =>
        source.DistinctUntilChanged(EqualityComparer<T>.Default);

    /// <summary>
    /// Emits values when they differ from the previous value.
    /// </summary>
    public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source, IEqualityComparer<T> comparer) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var hasValue = false;
            var previous = default(T);
            return source.Subscribe(
                value =>
                {
                    if (!hasValue || !comparer.Equals(previous!, value))
                    {
                        hasValue = true;
                        previous = value;
                        observer.OnNext(value);
                    }
                },
                observer.OnError,
                observer.OnCompleted);
        });

    /// <summary>
    /// Takes a fixed number of values.
    /// </summary>
    public static IObservable<T> Take<T>(this IObservable<T> source, int count) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            if (count <= 0)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            var seen = 0;
            var subscription = new SerialDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (seen >= count)
                    {
                        return;
                    }

                    observer.OnNext(value);
                    if (++seen == count)
                    {
                        observer.OnCompleted();
                        subscription.Dispose();
                    }
                },
                observer.OnError,
                observer.OnCompleted);
            return subscription;
        });

    /// <summary>
    /// Takes values until the other observable produces a value or error.
    /// </summary>
    public static IObservable<T> TakeUntil<T, TOther>(this IObservable<T> source, IObservable<TOther> other) =>
        Observable.Create<T>(observer =>
        {
            var disposables = new CompositeDisposable();
            var gate = new object();
            var stopped = false;

            void Stop(Action terminal)
            {
                var shouldStop = false;
                lock (gate)
                {
                    if (!stopped)
                    {
                        stopped = true;
                        shouldStop = true;
                    }
                }

                if (shouldStop)
                {
                    terminal();
                    disposables.Dispose();
                }
            }

            disposables.Add(source.Subscribe(
                value =>
                {
                    lock (gate)
                    {
                        if (!stopped)
                        {
                            observer.OnNext(value);
                        }
                    }
                },
                error => Stop(() => observer.OnError(error)),
                () => Stop(observer.OnCompleted)));

            disposables.Add(other.Subscribe(
                _ => Stop(observer.OnCompleted),
                error => Stop(() => observer.OnError(error)),
                static () => { }));

            return disposables;
        });

    /// <summary>
    /// Skips a fixed number of values.
    /// </summary>
    public static IObservable<T> Skip<T>(this IObservable<T> source, int count)
    {
        var seen = 0;
        return source.Where(_ => seen++ >= count);
    }

    /// <summary>
    /// Skips values while a predicate is true.
    /// </summary>
    public static IObservable<T> SkipWhile<T>(this IObservable<T> source, Func<T, bool> predicate)
    {
        var skipping = true;
        return source.Where(value =>
        {
            if (!skipping)
            {
                return true;
            }

            skipping = predicate(value);
            return !skipping;
        });
    }

    /// <summary>
    /// Invokes an action for each value.
    /// </summary>
    public static IObservable<T> Do<T>(this IObservable<T> source, Action<T> onNext) =>
        source.Do(onNext, static () => { });

    /// <summary>
    /// Invokes actions for values and errors.
    /// </summary>
    public static IObservable<T> Do<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError) =>
        source.Do(onNext, onError, static () => { });

    /// <summary>
    /// Invokes actions for values and completion.
    /// </summary>
    public static IObservable<T> Do<T>(this IObservable<T> source, Action<T> onNext, Action onCompleted) =>
        source.Do(onNext, static _ => { }, onCompleted);

    /// <summary>
    /// Invokes actions for values, errors, and completion.
    /// </summary>
    public static IObservable<T> Do<T>(
        this IObservable<T> source,
        Action<T> onNext,
        Action<Exception> onError,
        Action onCompleted) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
            source.Subscribe(
                value =>
                {
                    onNext(value);
                    observer.OnNext(value);
                },
                error =>
                {
                    onError(error);
                    observer.OnError(error);
                },
                () =>
                {
                    onCompleted();
                    observer.OnCompleted();
                }));

    /// <summary>
    /// Invokes an action when the subscription terminates.
    /// </summary>
    public static IObservable<T> Finally<T>(this IObservable<T> source, Action finallyAction) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var invoked = 0;

            void InvokeFinally()
            {
                if (Interlocked.Exchange(ref invoked, 1) == 0)
                {
                    finallyAction();
                }
            }

            var subscription = source.Subscribe(
                observer.OnNext,
                error =>
                {
                    try
                    {
                        observer.OnError(error);
                    }
                    finally
                    {
                        InvokeFinally();
                    }
                },
                () =>
                {
                    try
                    {
                        observer.OnCompleted();
                    }
                    finally
                    {
                        InvokeFinally();
                    }
                });

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                InvokeFinally();
            });
        });

    /// <summary>
    /// Throttles values by dropping pending values until no new value arrives for the duration.
    /// </summary>
    public static IObservable<T> Throttle<T>(this IObservable<T> source, TimeSpan dueTime, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
        {
            var gate = new object();
            var version = 0;
            var disposables = new CompositeDisposable();
            var timer = new SerialDisposable();
            disposables.Add(timer);

            disposables.Add(source.Subscribe(
                value =>
                {
                    var id = Interlocked.Increment(ref version);
                    timer.Disposable = scheduler.Schedule(dueTime, () =>
                    {
                        if (Volatile.Read(ref version) == id)
                        {
                            lock (gate)
                            {
                                observer.OnNext(value);
                            }
                        }
                    });
                },
                observer.OnError,
                observer.OnCompleted));

            return disposables;
        });

    /// <summary>
    /// Observes values on a sequencer.
    /// </summary>
    public static IObservable<T> ObserveOn<T>(this IObservable<T> source, IScheduler scheduler) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
            source.Subscribe(
                value => scheduler.Schedule(() => observer.OnNext(value)),
                error => scheduler.Schedule(() => observer.OnError(error)),
                () => scheduler.Schedule(observer.OnCompleted)));

    /// <summary>
    /// Synchronizes observer calls with a lock.
    /// </summary>
    public static IObservable<T> Synchronize<T>(this IObservable<T> source) => source.Synchronize(new object());

    /// <summary>
    /// Synchronizes observer calls with a lock.
    /// </summary>
    public static IObservable<T> Synchronize<T>(this IObservable<T> source, object gate) =>
        ReactiveUI.Primitives.Signals.Signal.Create<T>(observer =>
            source.Subscribe(
                value =>
                {
                    lock (gate)
                    {
                        observer.OnNext(value);
                    }
                },
                error =>
                {
                    lock (gate)
                    {
                        observer.OnError(error);
                    }
                },
                () =>
                {
                    lock (gate)
                    {
                        observer.OnCompleted();
                    }
                }));

    /// <summary>
    /// Creates a connectable observable using a subject.
    /// </summary>
    public static IConnectableObservable<T> Publish<T>(this IObservable<T> source) =>
        new ConnectableObservable<T>(source, new Subject<T>());

    /// <summary>
    /// Creates a connectable observable that replays all values to late subscribers.
    /// </summary>
    public static IConnectableObservable<T> Replay<T>(this IObservable<T> source) =>
        new ConnectableObservable<T>(source, new ReplaySubject<T>());

    /// <summary>
    /// Creates a connectable observable that replays buffered values to late subscribers.
    /// </summary>
    public static IConnectableObservable<T> Replay<T>(this IObservable<T> source, int bufferSize) =>
        new ConnectableObservable<T>(source, new ReplaySubject<T>(bufferSize));

    /// <summary>
    /// Creates a connectable observable and applies a selector.
    /// </summary>
    public static IObservable<TResult> Publish<T, TResult>(this IObservable<T> source, Func<IObservable<T>, IObservable<TResult>> selector) =>
        Observable.Create<TResult>(observer =>
        {
            var connectable = source.Publish();
            var subscription = selector(connectable).Subscribe(observer);
            var connection = connectable.Connect();
            return new CompositeDisposable(subscription, connection);
        });

    /// <summary>
    /// Shares a single subscription while observers are present.
    /// </summary>
    public static IObservable<T> RefCount<T>(this IConnectableObservable<T> source)
    {
        var gate = new object();
        var count = 0;
        IDisposable? connection = null;

        return Observable.Create<T>(observer =>
        {
            var subscription = source.Subscribe(observer);

            lock (gate)
            {
                count++;
                connection ??= source.Connect();
            }

            return Disposable.Create(() =>
            {
                subscription.Dispose();

                IDisposable? connectionToDispose = null;
                lock (gate)
                {
                    count--;
                    if (count == 0)
                    {
                        connectionToDispose = connection;
                        connection = null;
                    }
                }

                connectionToDispose?.Dispose();
            });
        });
    }

    /// <summary>
    /// Connects when the required number of observers subscribe.
    /// </summary>
    public static IObservable<T> AutoConnect<T>(this IConnectableObservable<T> source) => source.AutoConnect(1);

    /// <summary>
    /// Connects when the required number of observers subscribe.
    /// </summary>
    public static IObservable<T> AutoConnect<T>(this IConnectableObservable<T> source, int minObservers) =>
        minObservers <= 0
            ? AutoConnectImmediately(source)
            : Observable.Create<T>(observer =>
            {
                var subscription = source.Subscribe(observer);
                var count = Interlocked.Increment(ref AutoConnectState<T>.Counts.GetValue(source, static _ => new StrongBox<int>()).Value);
                if (count == minObservers)
                {
                    AutoConnectState<T>.Connections.GetValue(source, static item => new SerialDisposable()).Disposable = source.Connect();
                }

                return subscription;
            });

    private static IObservable<T> AutoConnectImmediately<T>(IConnectableObservable<T> source)
    {
        var connection = AutoConnectState<T>.Connections.GetValue(source, static _ => new SerialDisposable());
        if (connection.Disposable is null)
        {
            connection.Disposable = source.Connect();
        }

        return Observable.Create<T>(source.Subscribe);
    }

    /// <summary>
    /// Converts an enumerable to an observable.
    /// </summary>
    public static IObservable<T> ToObservable<T>(this IEnumerable<T> source) =>
        Observable.Create<T>(observer =>
        {
            try
            {
                foreach (var value in source)
                {
                    observer.OnNext(value);
                }

                observer.OnCompleted();
            }
            catch (Exception error)
            {
                observer.OnError(error);
            }

            return Disposable.Empty;
        });

    /// <summary>
    /// Converts an enumerable to an observable.
    /// </summary>
    public static IObservable<T> ToObservable<T>(this IEnumerable<T> source, IScheduler scheduler) =>
        Observable.Create<T>(observer => scheduler.Schedule(() =>
        {
            foreach (var value in source)
            {
                observer.OnNext(value);
            }

            observer.OnCompleted();
        }));

    /// <summary>
    /// Converts a task to an observable.
    /// </summary>
    public static IObservable<T> ToObservable<T>(this Task<T> task) =>
        Observable.Create<T>(observer =>
        {
            if (task.IsCompleted)
            {
                EmitCompletedTask(
                    task,
                    observer,
                    static () => { });
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    observer.OnCompleted();
                }

                return Disposable.Empty;
            }

            var disposed = 0;
            _ = AwaitTask(
                task,
                observer,
                () =>
                {
                    if (Volatile.Read(ref disposed) == 0)
                    {
                        observer.OnCompleted();
                    }
                });

            return Disposable.Create(() => Interlocked.Exchange(ref disposed, 1));
        });

    /// <summary>
    /// Blocks until the observable completes and returns the collected values.
    /// </summary>
    public static IEnumerable<T> ToEnumerable<T>(this IObservable<T> source)
    {
        var values = new List<T>();
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        using var subscription = source.Subscribe(
            values.Add,
            error =>
            {
                failure = error;
                completed.Set();
            },
            completed.Set);

        completed.Wait();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return values;
    }

    /// <summary>
    /// Collects values into a list.
    /// </summary>
    public static IObservable<IList<T>> ToList<T>(this IObservable<T> source) =>
        source.Aggregate((IList<T>)new List<T>(), (list, value) =>
        {
            list.Add(value);
            return list;
        });

    /// <summary>
    /// Converts the observable to a task for the final value.
    /// </summary>
    public static Task<T> ToTask<T>(this IObservable<T> source)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = new SerialDisposable();
        var hasValue = false;
        var lastValue = default(T);
        subscription.Disposable = source.Subscribe(
            value =>
            {
                hasValue = true;
                lastValue = value;
            },
            error =>
            {
                subscription.Dispose();
                completion.TrySetException(error);
            },
            () =>
            {
                subscription.Dispose();
                if (hasValue)
                {
                    completion.TrySetResult(lastValue!);
                }
                else
                {
                    completion.TrySetException(new InvalidOperationException("Sequence contains no elements."));
                }
            });

        return completion.Task;
    }

    /// <summary>
    /// Gets an awaiter for the final observable value.
    /// </summary>
    public static TaskAwaiter<T> GetAwaiter<T>(this IObservable<T> source) =>
        source.ToTask().GetAwaiter();

    /// <summary>
    /// Supplies a default value when the source is empty.
    /// </summary>
    public static IObservable<T> DefaultIfEmpty<T>(this IObservable<T> source, T defaultValue) =>
        Observable.Create<T>(observer =>
        {
            var seen = false;
            return source.Subscribe(
                value =>
                {
                    seen = true;
                    observer.OnNext(value);
                },
                observer.OnError,
                () =>
                {
                    if (!seen)
                    {
                        observer.OnNext(defaultValue);
                    }

                    observer.OnCompleted();
                });
        });

    /// <summary>
    /// Aggregates values.
    /// </summary>
    public static IObservable<TAccumulate> Aggregate<TSource, TAccumulate>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> accumulator) =>
        Observable.Create<TAccumulate>(observer =>
        {
            var current = seed;
            return source.Subscribe(
                value => current = accumulator(current, value),
                observer.OnError,
                () =>
                {
                    observer.OnNext(current);
                    observer.OnCompleted();
                });
        });

    /// <summary>
    /// Collects values into an array.
    /// </summary>
    public static IObservable<T[]> ToArray<T>(this IObservable<T> source) =>
        source.Aggregate(new List<T>(), (list, value) =>
        {
            list.Add(value);
            return list;
        }).Select(static list => list.ToArray());

    /// <summary>
    /// Determines whether any values match the predicate.
    /// </summary>
    public static IObservable<bool> Any<T>(this IObservable<T> source, Func<T, bool> predicate) =>
        Observable.Create<bool>(observer =>
        {
            var subscription = new SerialDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (!predicate(value))
                    {
                        return;
                    }

                    observer.OnNext(true);
                    observer.OnCompleted();
                    subscription.Dispose();
                },
                observer.OnError,
                () =>
                {
                    observer.OnNext(false);
                    observer.OnCompleted();
                });
            return subscription;
        });

    /// <summary>
    /// Determines whether all values match the predicate.
    /// </summary>
    public static IObservable<bool> All<T>(this IObservable<T> source, Func<T, bool> predicate) =>
        Observable.Create<bool>(observer =>
        {
            var subscription = new SerialDisposable();
            subscription.Disposable = source.Subscribe(
                value =>
                {
                    if (predicate(value))
                    {
                        return;
                    }

                    observer.OnNext(false);
                    observer.OnCompleted();
                    subscription.Dispose();
                },
                observer.OnError,
                () =>
                {
                    observer.OnNext(true);
                    observer.OnCompleted();
                });
            return subscription;
        });

    /// <summary>
    /// Counts source values.
    /// </summary>
    public static IObservable<int> Count<T>(this IObservable<T> source) =>
        source.Aggregate(0, static (count, _) => count + 1);

    /// <summary>
    /// Emits the last value, or the default value when the source is empty.
    /// </summary>
    public static IObservable<T> LastOrDefaultAsync<T>(this IObservable<T> source) =>
        Observable.Create<T>(observer =>
        {
            var last = default(T);
            return source.Subscribe(
                value => last = value,
                observer.OnError,
                () =>
                {
                    observer.OnNext(last!);
                    observer.OnCompleted();
                });
        });

    /// <summary>
    /// Combines latest values from two sources.
    /// </summary>
    public static IObservable<TResult> CombineLatest<TFirst, TSecond, TResult>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        Func<TFirst, TSecond, TResult> resultSelector) =>
        Observable.Create<TResult>(observer =>
        {
            var gate = new object();
            var firstHasValue = false;
            var secondHasValue = false;
            var firstValue = default(TFirst);
            var secondValue = default(TSecond);
            var firstCompleted = false;
            var secondCompleted = false;

            void TryComplete()
            {
                if (firstCompleted && secondCompleted)
                {
                    observer.OnCompleted();
                }
            }

            return new CompositeDisposable(
                first.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            firstValue = value;
                            firstHasValue = true;
                            if (secondHasValue)
                            {
                                observer.OnNext(resultSelector(firstValue!, secondValue!));
                            }
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (gate)
                        {
                            firstCompleted = true;
                            TryComplete();
                        }
                    }),
                second.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            secondValue = value;
                            secondHasValue = true;
                            if (firstHasValue)
                            {
                                observer.OnNext(resultSelector(firstValue!, secondValue!));
                            }
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (gate)
                        {
                            secondCompleted = true;
                            TryComplete();
                        }
                    }));
        });

    /// <summary>
    /// Combines latest values from three sources.
    /// </summary>
    public static IObservable<TResult> CombineLatest<T1, T2, T3, TResult>(
        this IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        Func<T1, T2, T3, TResult> resultSelector) =>
        source1.CombineLatest(source2, ValueTuple.Create).CombineLatest(source3, (pair, value3) => resultSelector(pair.Item1, pair.Item2, value3));

    /// <summary>
    /// Combines latest values from four sources.
    /// </summary>
    public static IObservable<TResult> CombineLatest<T1, T2, T3, T4, TResult>(
        this IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        IObservable<T4> source4,
        Func<T1, T2, T3, T4, TResult> resultSelector) =>
        source1.CombineLatest(source2, source3, (value1, value2, value3) => (value1, value2, value3))
            .CombineLatest(source4, (tuple, value4) => resultSelector(tuple.value1, tuple.value2, tuple.value3, value4));

    /// <summary>
    /// Combines latest values from five sources.
    /// </summary>
    public static IObservable<TResult> CombineLatest<T1, T2, T3, T4, T5, TResult>(
        this IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        IObservable<T4> source4,
        IObservable<T5> source5,
        Func<T1, T2, T3, T4, T5, TResult> resultSelector) =>
        source1.CombineLatest(source2, source3, source4, (value1, value2, value3, value4) => (value1, value2, value3, value4))
            .CombineLatest(source5, (tuple, value5) => resultSelector(tuple.value1, tuple.value2, tuple.value3, tuple.value4, value5));

    /// <summary>
    /// Combines latest values from six sources.
    /// </summary>
    public static IObservable<TResult> CombineLatest<T1, T2, T3, T4, T5, T6, TResult>(
        this IObservable<T1> source1,
        IObservable<T2> source2,
        IObservable<T3> source3,
        IObservable<T4> source4,
        IObservable<T5> source5,
        IObservable<T6> source6,
        Func<T1, T2, T3, T4, T5, T6, TResult> resultSelector) =>
        source1.CombineLatest(source2, source3, source4, source5, (value1, value2, value3, value4, value5) => (value1, value2, value3, value4, value5))
            .CombineLatest(source6, (tuple, value6) => resultSelector(tuple.value1, tuple.value2, tuple.value3, tuple.value4, tuple.value5, value6));

    /// <summary>
    /// Zips two observable sources.
    /// </summary>
    public static IObservable<TResult> Zip<TFirst, TSecond, TResult>(
        this IObservable<TFirst> first,
        IObservable<TSecond> second,
        Func<TFirst, TSecond, TResult> resultSelector) =>
        Observable.Create<TResult>(observer =>
        {
            var gate = new object();
            var firstQueue = new Queue<TFirst>();
            var secondQueue = new Queue<TSecond>();
            var firstCompleted = false;
            var secondCompleted = false;

            void Drain()
            {
                while (firstQueue.Count != 0 && secondQueue.Count != 0)
                {
                    observer.OnNext(resultSelector(firstQueue.Dequeue(), secondQueue.Dequeue()));
                }

                if ((firstCompleted && firstQueue.Count == 0) || (secondCompleted && secondQueue.Count == 0))
                {
                    observer.OnCompleted();
                }
            }

            return new CompositeDisposable(
                first.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            firstQueue.Enqueue(value);
                            Drain();
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (gate)
                        {
                            firstCompleted = true;
                            Drain();
                        }
                    }),
                second.Subscribe(
                    value =>
                    {
                        lock (gate)
                        {
                            secondQueue.Enqueue(value);
                            Drain();
                        }
                    },
                    observer.OnError,
                    () =>
                    {
                        lock (gate)
                        {
                            secondCompleted = true;
                            Drain();
                        }
                    }));
        });

    /// <summary>
    /// Ignores all values and preserves termination.
    /// </summary>
    public static IObservable<T> IgnoreElements<T>(this IObservable<T> source) =>
        Observable.Create<T>(observer => source.Subscribe(static _ => { }, observer.OnError, observer.OnCompleted));

    /// <summary>
    /// Handles errors by switching to a replacement observable.
    /// </summary>
    public static IObservable<T> Catch<T, TException>(this IObservable<T> source, Func<TException, IObservable<T>> handler)
        where TException : Exception =>
        Observable.Create<T>(observer =>
        {
            var subscription = new SerialDisposable();
            subscription.Disposable = source.Subscribe(
                observer.OnNext,
                error =>
                {
                    if (error is TException typed)
                    {
                        subscription.Disposable = handler(typed).Subscribe(observer);
                    }
                    else
                    {
                        observer.OnError(error);
                    }
                },
                observer.OnCompleted);
            return subscription;
        });

    private static class AutoConnectState<T>
    {
        public static readonly ConditionalWeakTable<IConnectableObservable<T>, StrongBox<int>> Counts = new();
        public static readonly ConditionalWeakTable<IConnectableObservable<T>, SerialDisposable> Connections = new();
    }
}

/// <summary>
/// Represents a connectable observable source.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal interface IConnectableObservable<out T> : IObservable<T>
{
    /// <summary>
    /// Connects the source.
    /// </summary>
    /// <returns>The connection lifetime.</returns>
    IDisposable Connect();
}

/// <summary>
/// A simple connectable observable implementation.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class ConnectableObservable<T> : IConnectableObservable<T>
{
    private readonly IObservable<T> _source;
    private readonly ISubject<T> _subject;
    private readonly object _locker = new();
    private IDisposable? _connection;
    private bool _terminated;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectableObservable{T}"/> class.
    /// </summary>
    /// <param name="source">The source observable.</param>
    /// <param name="subject">The multicast subject.</param>
    public ConnectableObservable(IObservable<T> source, ISubject<T> subject)
    {
        _source = source;
        _subject = subject;
    }

    /// <inheritdoc />
    public IDisposable Connect()
    {
        lock (_locker)
        {
            if (_terminated)
            {
                return Disposable.Empty;
            }

            if (_connection is null)
            {
                var subscription = _source.Subscribe(
                    _subject.OnNext,
                    error =>
                    {
                        lock (_locker)
                        {
                            _terminated = true;
                            _connection = null;
                        }

                        _subject.OnError(error);
                    },
                    () =>
                    {
                        lock (_locker)
                        {
                            _terminated = true;
                            _connection = null;
                        }

                        _subject.OnCompleted();
                    });

                if (_terminated)
                {
                    subscription.Dispose();
                    return Disposable.Empty;
                }

                IDisposable? connection = null;
                connection = Disposable.Create(() =>
                {
                    subscription.Dispose();
                    lock (_locker)
                    {
                        if (ReferenceEquals(_connection, connection))
                        {
                            _connection = null;
                        }
                    }
                });
                _connection = connection;
            }

            return _connection;
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(IObserver<T> observer) => _subject.Subscribe(observer);
}

/// <summary>
/// A disposable that invokes two disposable actions.
/// </summary>
internal sealed class CompositeActionDisposable : IDisposable
{
    private readonly IDisposable _first;
    private readonly Action _second;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeActionDisposable"/> class.
    /// </summary>
    public CompositeActionDisposable(IDisposable first, Action second)
    {
        _first = first;
        _second = second;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _first.Dispose();
        _second();
    }
}
