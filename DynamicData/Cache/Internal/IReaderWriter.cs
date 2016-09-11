using System;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal interface IReaderWriter<TObject, TKey> : IQuery<TObject, TKey>
    {
        Continuation<IChangeSet<TObject, TKey>> Write(IChangeSet<TObject, TKey> changes);
        Continuation<IChangeSet<TObject, TKey>> Write(Action<ICacheUpdater<TObject, TKey>> updateAction);
        Continuation<IChangeSet<TObject, TKey>> Write(Action<ISourceUpdater<TObject, TKey>> updateAction);
    }
}
