using System.Collections.Generic;

namespace DynamicData.Operators
{
    /// <summary>
    /// Container to specify comparer and comparison optimisations
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class ComparerOptions<TObject>
    {
        private readonly IComparer<TObject> _comparer;
        private readonly SortOptimisations _type;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComparerOptions{TObject}"/> class.
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        /// <param name="type">The type.</param>
        public ComparerOptions(IComparer<TObject> comparer, 
            SortOptimisations type
            )
        {
            _comparer = comparer;
            _type = type;
        }

        public IComparer<TObject> Comparer
        {
            get { return _comparer; }
        }

        public SortOptimisations Type
        {
            get { return _type; }
        }
    }
}