using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    /// <summary>
    /// Controller used to force the groups to be re-asssed
    /// </summary>
    public sealed class GroupController: IDisposable
    {
        private readonly ISubject<Unit> _regroupSubject = new ReplaySubject<Unit>();

        private readonly IDisposable _cleanUp;
    


        public GroupController()
        {
            _cleanUp = Disposable.Create(() => _regroupSubject.OnCompleted());
        }

        public void RefreshGroup()
        {
            _regroupSubject.OnNext(Unit.Default);
        }

        public IObservable<Unit> Regrouped
        {
            get { return _regroupSubject.AsObservable(); }
        }

        public void Dispose()
        {
           _cleanUp.Dispose();
        }
    }
}
