using System;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An editable observable list, providing  observable methods
    /// as well as data access methods
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISourceList<T> : IObservableList<T>
    {
        /// <summary>
        /// Edit the inner list within the list's internal locking mechanism
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">The error handler.</param>
        void Edit(Action<IExtendedList<T>> updateAction, Action<Exception> errorHandler = null);

        /// <summary>
        /// Notifies the observer that the source list has finished sending push-based notifications.
        /// </summary>
        void OnCompleted();

        /// <summary>
        /// Notifies the observer that the source list has experienced an error condition.
        /// </summary>
        void OnError(Exception exception);
    }
}
