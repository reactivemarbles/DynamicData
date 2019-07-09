using System;
using System.Reactive.Linq;

namespace DynamicData.Tests.Domain
{
    public class SelfObservingPerson : IDisposable
    {
        private bool _completed;
        private int _updateCount;
        private Person _person;
        private readonly IDisposable _cleanUp;

        public SelfObservingPerson(IObservable<Person> observable)
        {
            _cleanUp = observable.Finally(() => _completed = true).Subscribe(p =>
            {
                _person = p;
                _updateCount++;
            });
        }

        public Person Person { get { return _person; } }

        public int UpdateCount { get { return _updateCount; } }

        public bool Completed { get { return _completed; } }

        #region Overrides of IDisposable

        /// <summary>
        ///put here the code to dispose all managed and unmanaged resources
        /// </summary>
        public void Dispose()
        {
            _cleanUp.Dispose();
        }

        #endregion
    }
}
