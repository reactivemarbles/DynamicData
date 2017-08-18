using System;

namespace DynamicData
{
    /// <summary>
    /// Collection subject implemented by SourceList and SourceCache
    /// </summary>
    public interface ICollectionSubject
    {
        /// <summary>
        /// Notifies the observer that the source collection has finished sending push-based notifications.
        /// </summary>
        void OnCompleted();

        /// <summary>
        /// Notifies the observer that the source list has experienced an error condition.
        /// </summary>
        void OnError(Exception exception);
    }
}