using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Virtualisation controller
    /// </summary>
    public class VirtualisingController: IDisposable
    {
        private readonly ISubject<IVirtualRequest> _subject = new ReplaySubject<IVirtualRequest>(1);
        private IVirtualRequest _request;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualisingController"/> class.
        /// </summary>
        public VirtualisingController()
        {
            _request = new VirtualRequest(0, 30);
            _subject.OnNext(_request);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualisingController"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public VirtualisingController(IVirtualRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            _request = request;
            _subject.OnNext(_request);
        }

        /// <summary>
        /// Request to change the virtual results
        /// </summary>
        /// <param name="request">The request.</param>
        /// <exception cref="System.ArgumentNullException">request</exception>
        public void Virualise(IVirtualRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            _request = request;
            _subject.OnNext(_request);
        }

        /// <summary>
        /// Observable which is fired when a change request has been made
        /// </summary>
        /// <value>
        /// The changed.
        /// </value>
        public IObservable<IVirtualRequest> Changed
        {
            get
            {
                return _subject.AsObservable();
            }
        }

        public void Dispose()
        {
            _subject.OnCompleted();
        }
    }
}
