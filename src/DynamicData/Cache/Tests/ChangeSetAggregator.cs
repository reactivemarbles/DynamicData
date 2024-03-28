// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Diagnostics;

namespace DynamicData.Tests;

/// <summary>
/// Aggregates all events and statistics for a change set to help assertions when testing.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TContext">The type of context.</typeparam>
public sealed class ChangeSetAggregator<TObject, TKey, TContext> : IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable _disposer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey, IContext}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public ChangeSetAggregator(IObservable<IChangeSet<TObject, TKey, TContext>> source)
    {
        var published = source.Publish();

        Data = published.AsObservableCache();

        var results = published.Subscribe(updates => Messages.Add(updates), ex => Error = ex, () => IsCompleted = true);
        var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary, _ => { });
        var connected = published.Connect();

        _disposer = Disposable.Create(
            () =>
            {
                Data.Dispose();
                connected.Dispose();
                summariser.Dispose();
                results.Dispose();
            });
    }

    /// <summary>
    /// Gets the data.
    /// </summary>
    /// <value>
    /// The data.
    /// </value>
    public IObservableCache<TObject, TKey> Data { get; }

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether or not the ChangeSet fired OnCompleted.
    /// </summary>
    /// <value>
    /// Boolean Value.
    /// </value>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets the messages.
    /// </summary>
    /// <value>
    /// The messages.
    /// </value>
    public IList<IChangeSet<TObject, TKey, TContext>> Messages { get; } = new List<IChangeSet<TObject, TKey, TContext>>();

    /// <summary>
    /// Gets the summary.
    /// </summary>
    /// <value>
    /// The summary.
    /// </value>
    public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() => _disposer.Dispose();
}

/// <summary>
/// Aggregates all events and statistics for a change set to help assertions when testing.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public sealed class ChangeSetAggregator<TObject, TKey> : IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable _disposer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public ChangeSetAggregator(IObservable<IChangeSet<TObject, TKey>> source)
    {
        var published = source.Publish();

        Data = published.AsObservableCache();

        var results = published.Subscribe(updates => Messages.Add(updates), ex => Error = ex, () => IsCompleted = true);
        var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary, _ => { });
        var connected = published.Connect();

        _disposer = Disposable.Create(
            () =>
            {
                Data.Dispose();
                connected.Dispose();
                summariser.Dispose();
                results.Dispose();
            });
    }

    /// <summary>
    /// Gets the data.
    /// </summary>
    /// <value>
    /// The data.
    /// </value>
    public IObservableCache<TObject, TKey> Data { get; }

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether or not the ChangeSet fired OnCompleted.
    /// </summary>
    /// <value>
    /// Boolean Value.
    /// </value>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets the messages.
    /// </summary>
    /// <value>
    /// The messages.
    /// </value>
    public IList<IChangeSet<TObject, TKey>> Messages { get; } = new List<IChangeSet<TObject, TKey>>();

    /// <summary>
    /// Gets the summary.
    /// </summary>
    /// <value>
    /// The summary.
    /// </value>
    public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() => _disposer.Dispose();
}
