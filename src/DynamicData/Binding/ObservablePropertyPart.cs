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

/// <summary>
/// Provides members for the ObservablePropertyPart class.
/// </summary>
/// <param name="expression">The expression value.</param>
[DebuggerDisplay("ObservablePropertyPart<{" + nameof(expression) + "}>")]
internal sealed class ObservablePropertyPart(Expression expression)
{
    /// <summary>
    /// Gets the Invoker value.
    /// </summary>
    public Func<object, object?> Invoker { get; } = expression.CreateInvoker();

    /// <summary>
    /// Gets the Factory value.
    /// </summary>
    public Func<object, IObservable<Unit>> Factory { get; } = expression.CreatePropertyChangedFactory();
}
