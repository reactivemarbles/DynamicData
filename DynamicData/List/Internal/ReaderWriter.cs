using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.List.Internal
{
    internal sealed class ReaderWriter<T>
    {
        private readonly ChangeAwareList<T> _data = new ChangeAwareList<T>();
        private readonly object _locker = new object();

        public IChangeSet<T> Write(IChangeSet<T> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            IChangeSet<T> result;
            lock (_locker)
            {
                _data.Clone(changes);
                result = _data.CaptureChanges();
            }
            return result;
        }

        public IChangeSet<T> Write(Action<IExtendedList<T>> updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));

            IChangeSet<T> result;
            lock (_locker)
            {
                updateAction(_data);
                result = _data.CaptureChanges();
            }
            return result;
        }

        public IEnumerable<T> Items
        {
            get
            {
                IEnumerable<T> result;
                lock (_locker)
                {
                    result = _data.ToArray();
                }
                return result;
            }
        }

        public int Count => _data.Count;
    }
}
