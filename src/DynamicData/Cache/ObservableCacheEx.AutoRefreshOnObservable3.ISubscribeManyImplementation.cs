// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable CA1822
#pragma warning disable RCS1001
#pragma warning disable RCS1037
#pragma warning disable SA1008
#pragma warning disable SA1019
#pragma warning disable SA1025
#pragma warning disable SA1028
#pragma warning disable SA1124
#pragma warning disable SA1502
#pragma warning disable SA1505
#pragma warning disable SA1507
#pragma warning disable SA1508
#pragma warning disable SA1519
#pragma warning disable SA1600

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private interface ISubscribeManyImplementation<TObject, TKey, out TChild>
        where TObject : notnull
        where TKey : notnull
    {
        void AfterSourceNext(IChangeSet<TObject, TKey> upstreamChanges);
        
        IObservable<TChild> BuildChildSource(
            TObject item,
            TKey    key);
        
        void OnChildSourceNext(
            TObject item,
            TKey    key);
            
        void ProcessSourceChange(Change<TObject, TKey> change);
    }
}
