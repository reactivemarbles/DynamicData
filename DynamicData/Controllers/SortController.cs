using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Enables dynamic inline sorting
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use IObservable<IChangeSet<TObject, TKey>> and IObservable<Unit> as it is more in the spirit of Rx")]
    public sealed class SortController<T> : IDisposable
    {
        private readonly ISubject<IComparer<T>> _sortSubject = new ReplaySubject<IComparer<T>>(1);
        private readonly ISubject<Unit> _resortSubject = new Subject<Unit>();
        private IComparer<T> _defaultSort;
        private IComparer<T> _currentSort;

        /// <summary>
        /// Initializes a new instance of the <see cref="SortController{T}"/> class.
        /// </summary>
        /// <param name="defaultSort">The default sort.</param>
        /// <exception cref="System.ArgumentNullException">defaultSort</exception>
        public SortController(IComparer<T> defaultSort)
        {
            if (defaultSort == null) throw new ArgumentNullException(nameof(defaultSort));
            SetDefaultSort(defaultSort);
        }

        /// <summary>
        /// Sets the default sort.
        /// </summary>
        /// <param name="defaultSort">The default sort.</param>
        /// <exception cref="System.ArgumentNullException">defaultSort</exception>
        public void SetDefaultSort(IComparer<T> defaultSort)
        {
            if (defaultSort == null) throw new ArgumentNullException(nameof(defaultSort));
            _defaultSort = defaultSort;
            Change(defaultSort);
        }

        /// <summary>
        /// Changes the sort back to the default comparer
        /// </summary>
        public void Reset()
        {
            Change(_defaultSort);
        }

        /// <summary>
        /// Reapplies the current sort.  Useful when a sorting on properties or methods which can dynamically change.
        /// </summary>
        public void Resort()
        {
            _resortSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Changes the sort comparer.
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        /// <exception cref="System.ArgumentNullException">comparer</exception>
        public void Change(IComparer<T> comparer)
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            _currentSort = comparer;
            _sortSubject.OnNext(_currentSort);
        }

        /// <summary>
        /// Observable which is fired when the sort comparer is changed
        /// </summary>
        /// <value>
        /// The changed.
        /// </value>
        public IObservable<IComparer<T>> ComparerChanged => _sortSubject.AsObservable();

        /// <summary>
        /// Observable which is fired when the sort comparer is changed
        /// </summary>
        /// <value>
        /// The changed.
        /// </value>
        public IObservable<Unit> SortAgain => _resortSubject.AsObservable();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _sortSubject.OnCompleted();
            _resortSubject.OnCompleted();
        }
    }
}
