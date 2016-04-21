using System.Reactive.Subjects;

namespace DynamicData.Experimental
{
    internal interface ISubjectWithRefCount<T> : ISubject<T>
    {
        /// <summary>number of subscribers 
        /// </summary>
        /// <value>
        /// The ref count.
        /// </value>
        int RefCount { get; }
    }
}
