using System;
using System.Collections;
using System.Collections.Generic;
using DynamicData.Operators;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal sealed class PageChangeSet<T> : IPageChangeSet<T>
    {
        private readonly IChangeSet<T> _virtualChangeSet;

        public PageChangeSet(IChangeSet<T> virtualChangeSet, IPageResponse response)
        {
            _virtualChangeSet = virtualChangeSet ?? throw new ArgumentNullException(nameof(virtualChangeSet));

            Response = response ?? throw new ArgumentNullException(nameof(response));
        }

        public IPageResponse Response { get; }

        #region Delegating members


        int IChangeSet.Adds => _virtualChangeSet.Adds;

        int IChangeSet.Removes => _virtualChangeSet.Removes;

        int IChangeSet.Moves => _virtualChangeSet.Moves;

        public int Count => _virtualChangeSet.Count;

        int IChangeSet.Capacity
        {
            get => _virtualChangeSet.Capacity;
            set => _virtualChangeSet.Capacity = value;
        }

        int IChangeSet<T>.Replaced => _virtualChangeSet.Replaced;

        int IChangeSet<T>.TotalChanges => _virtualChangeSet.TotalChanges;


        public int Refreshes => _virtualChangeSet.Refreshes;

        #endregion

        public IEnumerator<Change<T>> GetEnumerator()
        {
            return _virtualChangeSet.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}