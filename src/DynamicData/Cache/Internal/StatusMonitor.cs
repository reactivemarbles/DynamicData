// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class StatusMonitor<T>(IObservable<T> source)
{
    public IObservable<ConnectionStatus> Run() =>
        source.Select(static _ => ConnectionStatus.Loaded)
            .Concat(Observable.Return(ConnectionStatus.Completed))
            .Catch<ConnectionStatus, Exception>(static error => Observable.Return(ConnectionStatus.Errored).Concat(Observable.Throw<ConnectionStatus>(error)))
            .StartWith(ConnectionStatus.Pending)
            .DistinctUntilChanged();
}
