using System;
using System.ComponentModel;

namespace DynamicData.Tests.Binding;

public static partial class WhenPropertyChangedFixture
{
    private const double ObservedAmount = 41.375;

    private static decimal ConvertAmount(double amount)
        => (decimal)amount;

    private static Deep1 NewDeepChain(int leaf)
        => new Deep1 { Child = NewDeep2(leaf) };

    private static Deep2 NewDeep2(int leaf)
        => new Deep2 { Child = NewDeep3(leaf) };

    private static Deep3 NewDeep3(int leaf)
        => new Deep3 { Child = NewDeep4(leaf) };

    private static Deep4 NewDeep4(int leaf) =>
        new Deep4 { Child = new Deep5 { Leaf = leaf } };

    private static decimal TruncateAmount(double amount)
        => decimal.Truncate((decimal)amount);

    private sealed class Item : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; init; }

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class KeyedActivable : INotifyPropertyChanged
    {
        private bool _activated;

        public KeyedActivable(int id)
        {
            Id = id;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; }

        public bool Activated
        {
            get => _activated;
            set
            {
                if (_activated == value) return;
                _activated = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Activated)));
            }
        }
    }

    private sealed class Money
    {
        private Money(double amount) => WholeUnits = (int)amount;

        public int WholeUnits { get; }

        public static explicit operator Money(double amount) => new(amount);
    }

    public sealed class ObservablePrice
        : INotifyPropertyChanged
    {
        private double _amount;
        private ObservablePrice? _child;
        private double _otherAmount;
        private PropertyChangedEventHandler? _propertyChanged;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                WasSubscribed = true;
                _propertyChanged += value;
            }

            remove => _propertyChanged -= value;
        }

        public double Amount
        {
            get => ReadError is null ? _amount : throw ReadError;
            set
            {
                _amount = value;
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount)));
            }
        }

        public ObservablePrice? Child
        {
            get => ChildReadError is null ? _child : throw ChildReadError;
            init => _child = value;
        }

        public InvalidOperationException? ChildReadError { get; init; }

        public int HandlerCount => _propertyChanged?.GetInvocationList().Length ?? 0;

        public InvalidOperationException? ReadError { get; init; }

        public double OtherAmount
        {
            get => _otherAmount;
            set
            {
                _otherAmount = value;
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OtherAmount)));
            }
        }

        public bool WasSubscribed { get; private set; }
    }
    
    private interface IHasAge
        : INotifyPropertyChanged
    {
        int Age { get; }
    }

    private sealed class TestModel : INotifyPropertyChanged
    {
        private int _value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class ParentModel : INotifyPropertyChanged
    {
        private ChildModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChildModel? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class ChildModel
        : IHasAge
    {
        private int _age;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
            }
        }
    }

    private sealed class Deep1 : INotifyPropertyChanged
    {
        private Deep2? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep2? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep2 : INotifyPropertyChanged
    {
        private Deep3? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep3? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep3 : INotifyPropertyChanged
    {
        private Deep4? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep4? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep4 : INotifyPropertyChanged
    {
        private Deep5? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Deep5? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Deep5 : INotifyPropertyChanged
    {
        private int _leaf;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Leaf
        {
            get => _leaf;
            set
            {
                _leaf = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }

    private sealed class Level1 : INotifyPropertyChanged
    {
        private Level2? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level2? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level2 : INotifyPropertyChanged
    {
        private Level3? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level3? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level3 : INotifyPropertyChanged
    {
        private Level4? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Level4? Child
        {
            get => _child;
            set
            {
                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class Level4 : INotifyPropertyChanged
    {
        private int _leaf;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Leaf
        {
            get => _leaf;
            set
            {
                _leaf = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Leaf)));
            }
        }
    }
}
