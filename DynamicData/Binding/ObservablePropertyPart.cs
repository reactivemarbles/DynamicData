using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive;

namespace DynamicData.Binding
{
    [DebuggerDisplay("ObservablePropertyPart<{_expression}>")]
    internal sealed class ObservablePropertyPart
    {
        // ReSharper disable once NotAccessedField.Local
        private readonly MemberExpression _expression;
        public Func<object, IObservable<Unit>> Factory { get; }
        public Func<object, object> Accessor { get; }

        public ObservablePropertyPart(MemberExpression expression)
        {
            _expression = expression;
            Factory = expression.CreatePropertyChangedFactory();
            Accessor = expression.CreateValueAccessor();
        }
    }
}