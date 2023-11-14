// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive;

namespace DynamicData.Binding;

[DebuggerDisplay("ObservablePropertyPart<{" + nameof(expression) + "}>")]
internal sealed class ObservablePropertyPart(MemberExpression expression)
{
    public Func<object, object> Accessor { get; } = expression.CreateValueAccessor();

    public Func<object, IObservable<Unit>> Factory { get; } = expression.CreatePropertyChangedFactory();
}
