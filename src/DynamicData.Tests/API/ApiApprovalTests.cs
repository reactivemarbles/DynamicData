using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace DynamicData.APITests
{
    /// <summary>
    /// Tests for handling API approval.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [UsesVerify]
    public class ApiApprovalTests
    {
        /// <summary>
        /// Tests to make sure the API of DynamicData project is approved.
        /// </summary>
        [Fact]
        public Task DynamicDataTests() => typeof(VirtualRequest).Assembly.CheckApproval(["DynamicData"]);
    }
}
