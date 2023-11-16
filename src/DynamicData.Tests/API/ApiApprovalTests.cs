using System.Diagnostics.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace DynamicData.APITests
{
    /// <summary>
    /// Tests for handling API approval.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [UsesVerify]
    public class ApiApprovalTests : ApiApprovalBase
    {
        /// <summary>
        /// Tests to make sure the DynamicData project is approved.
        /// </summary>
        [Fact]
        public void DynamicDataTests() => CheckApproval(typeof(VirtualRequest).Assembly);
    }
}
