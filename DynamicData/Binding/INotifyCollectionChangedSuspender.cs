using System;

namespace DynamicData.Binding
{
    /// <summary>
    /// Represents an observable collection where collection changed and count notifications can be suspended 
    /// </summary>
    public interface INotifyCollectionChangedSuspender
    {
        /// <summary>
        /// Suspends notifications. When disposed, a reset notification is fired
        /// </summary>
        IDisposable SuspendNotifications();

        /// <summary>
        /// Suspends count notifications
        /// </summary>
        IDisposable SuspendCount();
    }
}