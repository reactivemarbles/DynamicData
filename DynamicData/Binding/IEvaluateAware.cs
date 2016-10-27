
namespace DynamicData.Binding
{
    /// <summary>
    /// Implement on an object and use in conjunction with InvokeEvaluate operator
    /// to make an object aware of any evaluates
    /// </summary>
    public interface IEvaluateAware
    {
        /// <summary>
        /// Evaluate method
        /// </summary>
        void Evaluate();
    }
}
