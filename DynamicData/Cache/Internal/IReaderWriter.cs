using System;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal interface IReaderWriter<TObject, TKey> : IQuery<TObject, TKey>
    {
        Continuation<IChangeSet<TObject, TKey>> Write(IChangeSet<TObject, TKey> changes);
        Continuation<IChangeSet<TObject, TKey>> Write(Action<IIntermediateUpdater<TObject, TKey>> updateAction);
        Continuation<IChangeSet<TObject, TKey>> Write(Action<ISourceUpdater<TObject, TKey>> updateAction);
    }
}
