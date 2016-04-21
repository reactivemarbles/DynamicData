using System;

namespace DynamicData.Kernel
{
    internal sealed class Continuation<T>
    {
        private readonly Exception _exception;
        private readonly T _result;

        public Continuation(T result)
        {
            _result = result;
        }

        public Continuation(Exception exception)
        {
            _exception = exception;
        }

        public void Then(Action<T> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            if (!ReferenceEquals(_result, null))
                onComplete(_result);
        }

        public void Then(Action<T> onComplete, Action<Exception> onError)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            if (_exception != null)
            {
                if (onError != null)
                {
                    onError(_exception);
                }
                else
                {
                    throw _exception;
                }
            }
            else
            {
                onComplete(_result);
            }
        }

        public void OnError(Action<Exception> action)
        {
            if (action != null && _exception != null)
                action(_exception);
        }

        public T Result { get { return _result; } }
    }
}
