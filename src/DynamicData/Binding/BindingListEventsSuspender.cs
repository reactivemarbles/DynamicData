#if SUPPORTS_BINDINGLIST

using System;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace DynamicData.Binding
{
    internal sealed class BindingListEventsSuspender<T>: IDisposable
    {
        private readonly IDisposable _cleanUp;

        public BindingListEventsSuspender(BindingList<T> list)
        {
            list.RaiseListChangedEvents = false;

            _cleanUp = Disposable.Create(() =>
            {
                list.RaiseListChangedEvents = true;
                list.ResetBindings();
            });
        }

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
#endif