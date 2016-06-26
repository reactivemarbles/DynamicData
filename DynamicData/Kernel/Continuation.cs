using System;

namespace DynamicData.Kernel
{
    internal sealed class Continuation<T>
    {
        private readonly Exception _exception;

        public Continuation(T result)
        {
            Result = result;
        }

        public Continuation(Exception exception)
        {
            _exception = exception;
        }

        public void Then(Action<T> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            if (!ReferenceEquals(Result, null))
                onComplete(Result);
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
                onComplete(Result);
            }
        }

        public void OnError(Action<Exception> action)
        {
            if (action != null && _exception != null)
                action(_exception);
        }

        public T Result { get; }
    }
}
