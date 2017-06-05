using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    [DebuggerDisplay("PropertyChainPart<{_expresson}>")]
    internal sealed class PropertyChainPart
    {
        private readonly MemberExpression _expresson;
        public Func<object, IObservable<Unit>> Factory { get; }
        public Func<object, object> Accessor { get; }

        public PropertyChainPart(MemberExpression expresson)
        {
            _expresson = expresson;
            Factory = expresson.CreatePropertyChangedFactory();
            Accessor = expresson.CreateValueAccessor();
        }
    }
}