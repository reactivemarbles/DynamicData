using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reactive;

namespace DynamicData.Binding
{
    [DebuggerDisplay("ObservablePropertyPart<{_expresson}>")]
    internal sealed class ObservablePropertyPart
    {
        private readonly MemberExpression _expresson;
        public Func<object, IObservable<Unit>> Factory { get; }
        public Func<object, object> Accessor { get; }

        public ObservablePropertyPart(MemberExpression expresson)
        {
            _expresson = expresson;
            Factory = expresson.CreatePropertyChangedFactory();
            Accessor = expresson.CreateValueAccessor();
        }
    }
}