using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class ReaderWriter<T> : IDisposable
    {
        private ChangeAwareList<T> _data = new ChangeAwareList<T>();
        private readonly TwoStageRWLock _lock = new TwoStageRWLock(LockRecursionPolicy.SupportsRecursion);
        private bool _updateInProgress = false;

        public IChangeSet<T> Write(IChangeSet<T> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            IChangeSet<T> result;

            _lock.EnterWriteLock();
            try
            {
                _data.Clone(changes);
                result = _data.CaptureChanges();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            return result;
        }

        public IChangeSet<T> Write(Action<IExtendedList<T>> updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            IChangeSet<T> result;

            // Write straight to the list, no preview
            _lock.EnterWriteLock();
            try
            {
                _updateInProgress = true;
                updateAction(_data);
                _updateInProgress = false;
                result = _data.CaptureChanges();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return result;
        }
        
        public IChangeSet<T> WriteWithPreview(Action<IExtendedList<T>> updateAction, Action<IChangeSet<T>> previewHandler)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            if (previewHandler == null)
                throw new ArgumentNullException(nameof(previewHandler));

            IChangeSet<T> result;

            // Make a copy, apply changes on the main list, perform the preview callback with the old list and swap the lists again to finalize the update.
            _lock.EnterWriteLock();
            try
            {
                ChangeAwareList<T> copy = new ChangeAwareList<T>(_data, false);

                _updateInProgress = true;
                updateAction(_data);
                _updateInProgress = false;

                result = _data.CaptureChanges();

                InternalEx.Swap(ref _data, ref copy);

                previewHandler(result);

                InternalEx.Swap(ref _data, ref copy);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return result;
        }

        /// <summary>
        /// Perform a recursive write operation.
        /// Changes are added to the topmost change tracker.
        /// Use only during an invocation of Write/WriteWithPreview.
        /// </summary>
        public void WriteNested(Action<IExtendedList<T>> updateAction) 
        {
            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            _lock.EnterWriteLock();
            try
            {
                if (!_updateInProgress)
                {
                    throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
                }

                updateAction(_data);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerable<T> Items
        {
            get
            {
                IEnumerable<T> result;
                _lock.EnterReadLock();
                try
                {
                    result = _data.ToArray();
                }
                finally
                {
                    _lock.ExitReadLock();
                }
                return result;
            }
        }

        public int Count => _data.Count;

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
