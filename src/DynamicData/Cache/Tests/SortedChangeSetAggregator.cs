// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Diagnostics;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests;

/// <summary>
/// Aggregates all events and statistics for a sorted change set to help assertions when testing.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public class SortedChangeSetAggregator<TObject, TKey> : IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable _disposer;

    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedChangeSetAggregator{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public SortedChangeSetAggregator(IObservable<ISortedChangeSet<TObject, TKey>> source)
    {
        var published = source.Publish();

        var error = published.Subscribe(_ => { }, ex => Error = ex);
        var results = published.Subscribe(updates => Messages.Add(updates));
        Data = published.AsObservableCache();
        var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary);

        var connected = published.Connect();
        _disposer = Disposable.Create(
            () =>
            {
                connected.Dispose();
                summariser.Dispose();
                results.Dispose();
                error.Dispose();
            });
    }

    /// <summary>
    /// Gets the data of the steam cached in-order to apply assertions.
    /// </summary>
    public IObservableCache<TObject, TKey> Data { get; }

    /// <summary>
    /// Gets and error.
    /// </summary>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Gets record of all received messages.
    /// </summary>
    /// <value>
    /// The messages.
    /// </value>
    public IList<ISortedChangeSet<TObject, TKey>> Messages { get; } = new List<ISortedChangeSet<TObject, TKey>>();

    /// <summary>
    /// Gets the aggregated change summary.
    /// </summary>
    /// <value>
    /// The summary.
    /// </value>
    public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged responses.
    /// </summary>
    /// <param name="isDisposing">If being called by the Dispose method.</param>
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
