using System;
using System.Collections.Generic;

namespace DynamicData.List.Internal
{
    internal sealed class AnonymousObservableList<T> : IObservableList<T>
    {
        private readonly ISourceList<T> _sourceList;

        public AnonymousObservableList(IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _sourceList = new SourceList<T>(source);
        }

        public AnonymousObservableList(ISourceList<T> sourceList)
        {
            _sourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));
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
