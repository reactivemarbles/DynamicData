// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class DeferUntilLoaded<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _result;

    public DeferUntilLoaded(IObservableCache<TObject, TKey> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        _result = source.CountChanged.Where(count => count != 0).Take(1).Select(_ => new ChangeSet<TObject, TKey>()).Concat(source.Connect()).NotEmpty();
    }

    public DeferUntilLoaded(IObservable<IChangeSet<TObject, TKey>> source) => _result = source.MonitorStatus().Where(status => status == ConnectionStatus.Loaded).Take(1).Select(_ => new ChangeSet<TObject, TKey>()).Concat(source).NotEmpty();

    public IObservable<IChangeSet<TObject, TKey>> Run() => _result;
}
