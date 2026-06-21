// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

internal sealed class BindingListEventsSuspender<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : IDisposable
{
    private readonly IDisposable _cleanUp;

    public BindingListEventsSuspender(BindingList<T> list)
    {
        list.RaiseListChangedEvents = false;

        _cleanUp = Disposable.Create(
            () =>
            {
                list.RaiseListChangedEvents = true;
                list.ResetBindings();
            });
    }

    public void Dispose() => _cleanUp.Dispose();
}
