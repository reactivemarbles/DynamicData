using System;
using System.Threading;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Similar to ReaderWriterLockSlim, but still allows reading after disposal.
    /// All operations involving entering and exiting the lock in read mode still work after disposal.
    /// All other operations will throw an NullReferenceException.
    /// </summary>
    /// <remarks>
    /// Before disposing this object, all locks should be released.
    /// Failure to do so will result in a SynchronizationLockException.
    /// </remarks>
    internal class TwoStageRWLock : IDisposable
    {
        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private bool _isDisposed = false;
        private readonly object _lockStateLock = new object();

        public TwoStageRWLock(LockRecursionPolicy policy = LockRecursionPolicy.NoRecursion)
        {
            _lock = new ReaderWriterLockSlim(policy);
        }

        public void EnterReadLock()
        {
            // Don't do lock (_lockStateLock), because if a writer is active
            // and we are trying to read then Dispose() would be blocked.
            bool hasEnteredLock = false;
            while (!hasEnteredLock)
            {
                hasEnteredLock = TryEnterReadLock(100);
            }
        }

        public bool TryEnterReadLock(int millisecondsTimeout)
        {
            bool lockAcquired = Monitor.TryEnter(_lockStateLock, millisecondsTimeout);
            if (!lockAcquired)
            {
                return false;
            }

            bool rwLockAcquired = true;
            if (_lock != null)
            {
                //Technically, the timeout should be (millisecondsTimeout - time already waited)
                //However, fixing this inaccuracy introduces additional cost and a precise timeout is rarely required anyway.
                rwLockAcquired = _lock.TryEnterReadLock(millisecondsTimeout);
            }

            Monitor.Exit(_lockStateLock);
            return rwLockAcquired;
        }

        public bool TryEnterReadLock(TimeSpan timeout)
        {
            return this.TryEnterReadLock(timeout.Milliseconds);
        }

        public void ExitReadLock()
        {
            lock (_lockStateLock)
            {
                if (_lock != null)
                {
                    _lock.ExitReadLock();
                }
            }
        }

        #region Delegations
        public void EnterUpgradeableReadLock()
        {
            _lock.EnterUpgradeableReadLock();
        }

        public void EnterWriteLock()
        {
            _lock.EnterWriteLock();
        }

        public void ExitUpgradeableReadLock()
        {
            _lock.ExitUpgradeableReadLock();
        }

        public void ExitWriteLock()
        {
            _lock.ExitWriteLock();
        }

        public bool TryEnterUpgradeableReadLock(int millisecondsTimeout)
        {
            return _lock.TryEnterUpgradeableReadLock(millisecondsTimeout);
        }

        public bool TryEnterUpgradeableReadLock(TimeSpan timeout)
        {
            return _lock.TryEnterUpgradeableReadLock(timeout);
        }

        public bool TryEnterWriteLock(int millisecondsTimeout)
        {
            return _lock.TryEnterWriteLock(millisecondsTimeout);
        }

        public bool TryEnterWriteLock(TimeSpan timeout)
        {
            return _lock.TryEnterWriteLock(timeout);
        }

        /*
        Unsupported for now:

        public int CurrentReadCount => _lock.CurrentReadCount;

        public bool IsReadLockHeld => _lock.IsReadLockHeld;

        public bool IsUpgradeableReadLockHeld => _lock.IsUpgradeableReadLockHeld;

        public bool IsWriteLockHeld => _lock.IsWriteLockHeld;

        public LockRecursionPolicy RecursionPolicy => _lock.RecursionPolicy;

        public int RecursiveReadCount => _lock.RecursiveReadCount;

        public int RecursiveUpgradeCount => _lock.RecursiveUpgradeCount;

        public int RecursiveWriteCount => _lock.RecursiveWriteCount;

        public int WaitingReadCount => _lock.WaitingReadCount;

        public int WaitingUpgradeCount => _lock.WaitingUpgradeCount;

        public int WaitingWriteCount => _lock.WaitingWriteCount;
        */
        #endregion

        public void Dispose()
        {
            lock (_lockStateLock)
            {
                var l = _lock;
                _lock = null;
                l?.Dispose();
            }
        }
    }
}
