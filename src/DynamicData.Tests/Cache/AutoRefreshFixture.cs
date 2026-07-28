namespace DynamicData.Tests.Cache;

public static partial class AutoRefreshFixture
{
    public enum NotificationStrategy
    {
        Immediate,
        Asynchronous
    }

    public class Item
        : INotifyPropertyChanged
    {
        public static int SelectId(Item item)
            => item.Id;
    
        public required int Id
        {
            get => _id;
            init => _id = value;
        }
            
        public bool HasSubscriptions
            => PropertyChanged is not null;
            
        public int OtherValue
        {
            get => _otherValue;
            set
            {
                if (_otherValue == value)
                    return;
                        
                _otherValue = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OtherValue)));
            }
        }
            
        public int Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                        
                _value = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public void RaiseAllPropertiesChanged()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        
        private readonly int _id;
            
        private int _otherValue;
        private int _value;
    }
}
