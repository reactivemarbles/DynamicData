using System.Diagnostics.CodeAnalysis;

namespace DynamicData.APITests
{
    /// <summary>
    /// Tests for handling API approval.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ApiApprovalTests
    {
        /// <summary>
        /// Tests to make sure the API of DynamicData project is approved.
        /// </summary>
        [Fact]
        public Task DynamicDataTests() => typeof(VirtualRequest).Assembly.CheckApproval(["DynamicData"]);

        /// <summary>
        /// Tests to make sure the API of DynamicData.Reactive project is approved.
        /// </summary>
        [Fact]
        public Task DynamicDataReactiveTests()
        {
            var reactiveAssemblyPath = Path.Combine(AppContext.BaseDirectory, "DynamicData.Reactive.dll");
            var reactiveAssembly = System.Reflection.Assembly.LoadFrom(reactiveAssemblyPath);

            return reactiveAssembly.CheckApproval(["DynamicData.Reactive"]);
        }
    }
}
