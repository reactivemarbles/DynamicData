// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class ChangeSetMergeContainer<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public ChangeSetMergeContainer(IObservable<IChangeSet<TObject, TKey>> source)
    {
        Source = source.IgnoreSameReferenceUpdate().Do(Clone);
    }

    public Cache<TObject, TKey> Cache { get; } = new();

    public IObservable<IChangeSet<TObject, TKey>> Source { get; }

    private void Clone(IChangeSet<TObject, TKey> changes) => Cache.Clone(changes);
}
