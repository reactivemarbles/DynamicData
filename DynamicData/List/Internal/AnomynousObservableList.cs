using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class AnomynousObservableList<T> : IObservableList<T>
    {
        private readonly ISourceList<T> _sourceList;

        public AnomynousObservableList(IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _sourceList = new SourceList<T>(source);
        }

        public AnomynousObservableList(ISourceList<T> sourceList)
        {
            if (sourceList == null) throw new ArgumentNullException(nameof(sourceList));
            _sourceList = sourceList;
        }

        public IObservable<int> CountChanged => _sourceList.CountChanged;

        public IEnumerable<T> Items => _sourceList.Items;

        public int Count => _sourceList.Count;

        public IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate = null)
        {
            return _sourceList.Connect(predicate);
        }

        public void Dispose()
        {
            _sourceList.Dispose();
        }
    }
}
