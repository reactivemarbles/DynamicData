﻿// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;

namespace DynamicData.Binding;

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
