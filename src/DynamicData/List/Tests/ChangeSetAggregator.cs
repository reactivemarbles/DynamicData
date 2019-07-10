// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    public class ChangeSetAggregator<TObject> : IDisposable
    {
        private readonly IDisposable _disposer;
        private readonly IList<IChangeSet<TObject>> _messages = new List<IChangeSet<TObject>>();
        private Exception _error;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public ChangeSetAggregator(IObservable<IChangeSet<TObject>> source)
        {
            var published = source.Publish();

            Data = published.AsObservableList();

            var results = published.Subscribe(updates => _messages.Add(updates), ex => _error = ex);
            var connected = published.Connect();

            _disposer = Disposable.Create(() =>
            {
                Data.Dispose();
                connected.Dispose();
                results.Dispose();
            });
        }

        /// <summary>
        /// A clone of the data
        /// </summary>
        public IObservableList<TObject> Data { get; }

        /// <summary>
        /// All message received
        /// </summary>
        public IList<IChangeSet<TObject>> Messages => _messages;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (isDisposing)
            {
                _disposer?.Dispose();
            }
        }
    }
}
