using System;

namespace DynamicData.Kernel
{
    public class DoubleCheck<T>
        where T : class
    {
        private readonly Func<T> _factory;
        private readonly object _locker = new object();
        private volatile T _value;

        public DoubleCheck(Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            _factory = factory;
        }

        public T Value
        {
            get
            {
                if (_value == null)
                    lock (_locker)
                        if (_value == null)
                        {
                            _value = _factory();
                        }
                return _value;
            }
        }
    }
}