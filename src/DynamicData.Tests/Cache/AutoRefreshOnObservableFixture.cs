using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshOnObservableFixture
{
    public enum NotificationStrategy
    {
        Immediate,
        Asynchronous
    }

    public sealed class Item
        : IDisposable
    {
        public static IObservable<int> ObserveValue(Item item)
            => Observable.Create<int>(observer =>
            {
                observer.OnNext(item._value);
                return item._valueChanged.SubscribeSafe(observer);
            });
            
        public static IObservable<Unit> ObserveValueChanged(Item item)
            => item._valueChanged.Select(static _ => Unit.Default);
            
        public static int SelectId(Item item)
            => item.Id;
    
        public Item()
            => _valueChanged = new();
        
        public required int Id
        {
            get => _id;
            init => _id = value;
        }
            
        public bool HasObservers
            => _valueChanged.HasObservers;
            
        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                        
                _value = value;
                _valueChanged.OnNext(value);
            }
        }
        
        public void Complete()
            => _valueChanged.OnCompleted();
        
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _hasDisposed, true))
                return;
            
            _valueChanged.OnCompleted();
            _valueChanged.Dispose();
        }
        
        public void SetError(Exception error)
            => _valueChanged.OnError(error);
            
        private readonly int            _id;
        private readonly Subject<int>   _valueChanged;
            
        private bool    _hasDisposed;
        private int     _value;
    }
}
