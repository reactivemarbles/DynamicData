
namespace DynamicData.PLinq
{
    /// <summary>
    /// Options to specify parallelisation of stream operations.  Only applicable for .Net4 and .Net45 builds 
    /// </summary>
    public class ParallelisationOptions
    {
        private readonly ParallelType _type;
        private readonly int _threshold = 0;

        /// <summary>
        /// The default parallelisation options
        /// </summary>
        public readonly static ParallelisationOptions Default = new ParallelisationOptions(ParallelType.Ordered, 0);

        /// <summary>
        /// Value to be used when no parallelisation should take place
        /// </summary>
        public readonly static ParallelisationOptions None = new ParallelisationOptions(ParallelType.None, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public ParallelisationOptions(ParallelType type = ParallelType.None, int threshold = 0, int maxDegreeOfParallisation = 0)
        {
            _type = type;
            _threshold = threshold;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public ParallelType Type { get { return _type; } }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        /// <value>
        /// The threshold.
        /// </value>
        public int Threshold { get { return _threshold; } }
    }
}
