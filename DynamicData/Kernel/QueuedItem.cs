using System;
using System.Threading;

namespace DynamicData.Kernel
{
    internal class QueuedItem<T> : IDisposable
    {
        private readonly T _state;
        private readonly Action<T> _action;
        private readonly Action _completed;
        private readonly CancellationTokenSource _completionSource = new CancellationTokenSource();
        private readonly Action<Exception> _exceptionHandler;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public QueuedItem(T state, Action<T> action, Action<Exception> exceptionHandler = null, Action completed = null)
        {
            _state = state;
            _completed = completed;
            _exceptionHandler = exceptionHandler;
            _action = action;
        }

        public T State
        {
            get { return _state; }
        }

        public Action<T> Action
        {
            get { return _action; }
        }

        public Action<Exception> ExceptionHandler
        {
            get { return _exceptionHandler; }
        }

        public Action Completed
        {
            get { return _completed; }
        }

        public bool IsCancelled
        {
            get { return _completionSource.IsCancellationRequested; }
        }

        public void Dispose()
        {
            _completionSource.Cancel();
        }
    }
}