// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests;

/// <summary>
/// Aggregates all events and statistics for a change set to help assertions when testing.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
public class ChangeSetAggregator<TObject> : IDisposable
    where TObject : notnull
{
    private readonly IDisposable _disposer;

    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public ChangeSetAggregator(IObservable<IChangeSet<TObject>> source)
    {
        var published = source.Publish();

        Data = published.AsObservableList();

        var results = published.Subscribe(
            onNext: Messages.Add,
            onError: error => Exception = error,
            onCompleted: () => IsCompleted = true);
        var connected = published.Connect();

        _disposer = Disposable.Create(
            () =>
            {
                Data.Dispose();
                connected.Dispose();
                results.Dispose();
            });
    }

    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets a clone of the data.
    /// </summary>
    public IObservableList<TObject> Data { get; }

    /// <summary>
    /// Gets a value indicating whether the source stream emitted a completion.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets all message received.
    /// </summary>
    public IList<IChangeSet<TObject>> Messages { get; } = new List<IChangeSet<TObject>>();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources.
    /// </summary>
    /// <param name="isDisposing">If the method is being called by the Dispose method.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (isDisposing)
        {
            _disposer.Dispose();
        }
    }
}
