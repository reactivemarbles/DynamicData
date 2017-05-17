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

        public void Then(Action<T> onChangeComplete)
        {
            if (onChangeComplete == null) throw new ArgumentNullException(nameof(onChangeComplete));

            if (!ReferenceEquals(Result, null))
                onChangeComplete(Result);
        }

        public void Then(Action<T> onChangeComplete, Action<Exception> onError)
        {
            if (onChangeComplete == null) throw new ArgumentNullException(nameof(onChangeComplete));

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
                onChangeComplete(Result);
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
