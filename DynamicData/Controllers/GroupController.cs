using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Controller used to inject meta data into a group stream.
    /// </summary>
    [Obsolete("Use IObservable<Unit> overload as it is more in the spirit of Rx")]
    public sealed class GroupController : IDisposable
    {
        private readonly ISubject<Unit> _regroupSubject = new ReplaySubject<Unit>();
        private readonly IDisposable _cleanUp;

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupController"/> class.
        /// </summary>
        public GroupController()
        {
            _cleanUp = Disposable.Create(() => _regroupSubject.OnCompleted());
        }

        /// <summary>
        /// Force all items to re-evaluate whether which group the should belong in
        /// </summary>
        public void RefreshGroup()
        {
            _regroupSubject.OnNext(Unit.Default);
        }

        internal IObservable<Unit> Regrouped => _regroupSubject.AsObservable();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
