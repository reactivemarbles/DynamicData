using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Controllers
{
    internal class GroupController<TObject, TGroupKey>
    {
        private readonly Func<TObject, TGroupKey> _groupSelector;
        private readonly ISubject<Func<TObject, TGroupKey>> _groupSubject = new Subject<Func<TObject, TGroupKey>>();
        private readonly ISubject<Unit> _regroupSubject = new ReplaySubject<Unit>();

        public GroupController(Func<TObject, TGroupKey> groupSelector)
        {
            if (groupSelector == null) throw new ArgumentNullException("groupSelector");
            _groupSelector = groupSelector;
        }

        public void ReapplyGroup()
        {
            _regroupSubject.OnNext(Unit.Default);
        }

        public IObservable<Unit> Regrouped
        {
            get { return _regroupSubject.AsObservable(); }
        }

    }
}
