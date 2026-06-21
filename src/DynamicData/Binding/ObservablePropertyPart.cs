// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Linq.Expressions;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

[DebuggerDisplay("ObservablePropertyPart<{" + nameof(expression) + "}>")]
internal sealed class ObservablePropertyPart(Expression expression)
{
    public Func<object, object?> Invoker { get; } = expression.CreateInvoker();

    public Func<object, IObservable<Unit>> Factory { get; } = expression.CreatePropertyChangedFactory();
}
