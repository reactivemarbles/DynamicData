using System;
using System.Threading;

namespace DynamicData.Kernel
{
    internal sealed class QueuedAction : IDisposable
    {
        private readonly Action _action;
        private readonly Action _completed;
        private readonly CancellationTokenSource _completionSource = new CancellationTokenSource();
        private readonly Action<Exception> _exceptionHandler;

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        public QueuedAction(Action action, Action<Exception> exceptionHandler = null, Action completed = null)
        {
            _completed = completed;
            _exceptionHandler = exceptionHandler;
            _action = action;
        }

        public Action Action
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