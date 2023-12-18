// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace DynamicData.Experimental;

/// <summary>
/// A subject with a count of the number of subscribers.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SubjectWithRefCount{T}"/> class.
/// </remarks>
/// <param name="subject">The subject to perform reference counting on.</param>
internal sealed class SubjectWithRefCount<T>(ISubject<T>? subject = null) : ISubjectWithRefCount<T>
{
    private readonly ISubject<T> _subject = subject ?? new Subject<T>();

    private int _refCount;

    /// <summary>Gets number of subscribers.</summary>
    /// <value>
    /// The ref count.
    /// </value>
    public int RefCount => _refCount;

    /// <summary>
    /// Notifies the observer that the provider has finished sending push-based notifications.
    /// </summary>
    public void OnCompleted() => _subject.OnCompleted();

    /// <summary>
    /// Notifies the observer that the provider has experienced an error condition.
    /// </summary>
    /// <param name="error">An object that provides additional information about the error.</param>
    public void OnError(Exception error) => _subject.OnError(error);

    /// <summary>
    /// Provides the observer with new data.
    /// </summary>
    /// <param name="value">The current notification information.</param>
    public void OnNext(T value) => _subject.OnNext(value);

    /// <summary>
    /// Notifies the provider that an observer is to receive notifications.
    /// </summary>
    /// <returns>
    /// The observer's interface that enables resources to be disposed.
    /// </returns>
    /// <param name="observer">The object that is to receive notifications.</param>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        Interlocked.Increment(ref _refCount);
        var subscriber = _subject.Subscribe(observer);

        return Disposable.Create(
            () =>
            {
                Interlocked.Decrement(ref _refCount);
                subscriber.Dispose();
            });
    }
}
