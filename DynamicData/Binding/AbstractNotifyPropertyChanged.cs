using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Threading;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Binding
{
    /// <summary>
    /// Base class for implementing notify property changes
    /// </summary>
    public abstract class AbstractNotifyPropertyChanged : INotifyPropertyChanged
    {
        private HashSet<string> _suspendedNotifications;

        /// <summary>
        /// Occurs when a property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Invokes on property changed
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_suspendedNotifications != null)
            {
                _suspendedNotifications.Add(propertyName);
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// If the value has changed, sets referenced backing field and raise notify property changed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="backingField">The backing field.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void SetAndRaise<T>(ref T backingField, T newValue, [CallerMemberName] string propertyName = null)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            SetAndRaise(ref backingField, newValue, EqualityComparer<T>.Default, propertyName);
        }

        /// <summary>
        /// If the value has changed, sets referenced backing field and raise notify property changed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="backingField">The backing field.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void SetAndRaise<T>(ref T backingField, T newValue, IEqualityComparer<T> comparer, [CallerMemberName] string propertyName = null)
        {
            comparer = comparer ?? EqualityComparer<T>.Default;
            if (comparer.Equals(backingField, newValue)) return;
            backingField = newValue;
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Suspends notifications. When disposed, a reset notification is fired
        /// </summary>
        /// <returns></returns>
        public IDisposable SuspendNotifications(bool invokePropertyChangeEventWhenDisposed = true)
        {
            Interlocked.Exchange(ref _suspendedNotifications, new HashSet<string>());

            return Disposable.Create(() =>
            {
                var notifications = _suspendedNotifications;
                Interlocked.Exchange(ref _suspendedNotifications, null);

                if (invokePropertyChangeEventWhenDisposed)
                {
                    notifications.ForEach(property =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
                    });
                }
            });
        }
    }
}
