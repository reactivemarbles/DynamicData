using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Enables dynamic filtering of the stream
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use IObservable<Func<TObject, bool>> and IObservable<Unit> overloads as they are more in the spirit of Rx")]
    public sealed class FilterController<T> : IDisposable
    {
        private readonly ISubject<Func<T, bool>> _filterSubject = new ReplaySubject<Func<T, bool>>(1);
        private readonly ISubject<Func<T, bool>> _reevaluteSubject = new Subject<Func<T, bool>>();

        private Func<T, bool> _filter;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterController{T}"/> class.
        /// </summary>
        /// <param name="defaultFilter">The default filter.</param>
        public FilterController(Func<T, bool> defaultFilter = null)
        {
            _filter = defaultFilter ?? (t => true);
            _filterSubject.OnNext(_filter);
        }

        /// <summary>
        /// Change the current filter.
        /// </summary>
        /// <param name="filter">The filter. Set to null to include all items</param>
        public void Change(Func<T, bool> filter = null)
        {
            _filter = filter ?? (t => true);
            _filterSubject.OnNext(filter);
        }

        /// <summary>
        /// Changes the filter to include all items
        /// </summary>
        public void ChangeToIncludeAll()
        {
            _filter = t => true;
            _filterSubject.OnNext(_filter);
        }

        /// <summary>
        /// Changes the filter to include all items
        /// </summary>
        public void ChangeToExcludeAll()
        {
            _filter = t => false;
            _filterSubject.OnNext(_filter);
        }

        /// <summary>
        ///     Reevaluates all items.
        /// </summary>
        public void Reevaluate()
        {
            _filterSubject.OnNext(_filter);
        }

        /// <summary>
        ///     Evaluates the filter for items specified by the item selector.
        /// </summary>
        /// <param name="itemSelector">The item selector.</param>
        public void Reevaluate(Func<T, bool> itemSelector)
        {
            if (itemSelector == null) throw new ArgumentNullException(nameof(itemSelector));
            _reevaluteSubject.OnNext(itemSelector);
        }

        /// <summary>
        /// Observable which is fired when the filter is changed
        /// </summary>
        /// <value>
        /// The filter changed.
        /// </value>
        public IObservable<Func<T, bool>> FilterChanged => _filterSubject.AsObservable();

        /// <summary>
        /// Observable which is fired when the re-evaluate is invoked
        /// </summary>
        /// <value>
        /// The evaluate changed.
        /// </value>
        public IObservable<Func<T, bool>> EvaluateChanged => _reevaluteSubject.AsObservable();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _filterSubject.OnCompleted();
            _reevaluteSubject.OnCompleted();
        }
    }
}
