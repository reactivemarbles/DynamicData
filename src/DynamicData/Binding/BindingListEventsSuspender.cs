// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

/// <summary>
/// Provides members for the BindingListEventsSuspender class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class BindingListEventsSuspender<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : IDisposable
{
    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingListEventsSuspender{T}"/> class.
    /// </summary>
    /// <param name="list">The list value.</param>
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

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _cleanUp.Dispose();
}
