using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Operators;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Dynamic page controller
    /// </summary>
    [Obsolete("Use IObservable<Func<TObject, bool>> and IObservable<Unit> overloads as they are more in the spirit of Rx")]
    public sealed class PageController : IDisposable
    {
        private readonly ISubject<IPageRequest> _subject = new ReplaySubject<IPageRequest>(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public PageController()
        {
            //    OnNext(PageRequest.Default);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public PageController(IPageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            OnNext(request);
        }

        /// <summary>
        /// Request to change a page
        /// </summary>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public void Change(IPageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            OnNext(request);
        }

        private void OnNext(IPageRequest request)
        {
            _subject.OnNext(request);
        }

        /// <summary>
        /// Observable which is fired when a  page request has been made
        /// </summary>
        /// <value>
        /// The changed.
        /// </value>
        public IObservable<IPageRequest> Changed => _subject.AsObservable();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _subject.OnCompleted();
        }
    }
}
