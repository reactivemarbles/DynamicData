
namespace DynamicData.PLinq
{
    /// <summary>
    /// Options to specify parallelisation of stream operations.  Only applicable for .Net4 and .Net45 builds 
    /// </summary>
    public class ParallelisationOptions
    {
        /// <summary>
        /// The default parallelisation options
        /// </summary>
        public static readonly ParallelisationOptions Default = new ParallelisationOptions(ParallelType.Ordered, 0);

        /// <summary>
        /// Value to be used when no parallelisation should take place
        /// </summary>
        public static readonly ParallelisationOptions None = new ParallelisationOptions(ParallelType.None, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public ParallelisationOptions(ParallelType type = ParallelType.None, int threshold = 0, int maxDegreeOfParallisation = 0)
        {
            Type = type;
            Threshold = threshold;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public ParallelType Type { get; }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        /// <value>
        /// The threshold.
        /// </value>
        public int Threshold { get; } = 0;
    }
}
