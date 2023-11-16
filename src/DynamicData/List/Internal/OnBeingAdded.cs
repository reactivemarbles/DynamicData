// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class OnBeingAdded<T>(IObservable<IChangeSet<T>> source, Action<T> callback)
    where T : notnull
{
    private readonly Action<T> _callback = callback ?? throw new ArgumentNullException(nameof(callback));

    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<T>> Run() => _source.Do(RegisterForAddition);

    private void RegisterForAddition(IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    _callback(change.Item.Current);
                    break;

                case ListChangeReason.AddRange:
                    change.Range.ForEach(_callback);
                    break;

                case ListChangeReason.Replace:
                    _callback(change.Item.Current);
                    break;
            }
        }
    }
}
