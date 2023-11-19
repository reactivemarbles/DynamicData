using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiffEngine;
using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace DynamicData.APITests
{
    /// <summary>
    /// Api Approval Base.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [UsesVerify]
    public abstract class ApiApprovalBase
    {
        private static readonly Regex _removeCoverletSectionRegex = new(@"^namespace Coverlet\.Core\.Instrumentation\.Tracker.*?^}", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Check Approval.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>
        /// A <see cref="Task" /> representing the result of the asynchronous operation.
        /// </returns>
        protected static void CheckApproval(Assembly assembly, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null)
        {
            var targetFrameworkName = Assembly.GetExecutingAssembly().GetTargetFrameworkName();

            var sourceDirectory = Path.GetDirectoryName(filePath);

            Assert.NotNull(targetFrameworkName);
            Assert.NotNull(sourceDirectory);

            var approvedFileName = Path.Combine(sourceDirectory, $"ApiApprovalTests.{memberName}.{targetFrameworkName}.approved.txt");
            var receivedFileName = Path.Combine(sourceDirectory, $"ApiApprovalTests.{memberName}.{targetFrameworkName}.received.txt");

            if (!File.Exists(receivedFileName))
            {
                File.Create(receivedFileName).Close();
            }

            if (!File.Exists(approvedFileName))
            {
                File.Create(approvedFileName).Close();
            }

            var approvedPublicApi = File.ReadAllText(approvedFileName);

            var generatorOptions = new ApiGeneratorOptions { AllowNamespacePrefixes = ["DynamicData"] };
            var receivedPublicApi = Filter(assembly.GeneratePublicApi(generatorOptions));

            if (!string.Equals(receivedPublicApi, approvedPublicApi, StringComparison.InvariantCulture))
            {
                File.WriteAllText(receivedFileName, receivedPublicApi);
                DiffRunner.Launch(receivedFileName, approvedFileName);
            }

            Assert.Equal(approvedPublicApi, receivedPublicApi);
        }

        private static string Filter(string text)
        {
            text = _removeCoverletSectionRegex.Replace(text, string.Empty);
            return string.Join(Environment.NewLine, text.Split(
                    new[]
                    {
                    Environment.NewLine
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(l =>
                    !l.StartsWith("[assembly: AssemblyVersion(", StringComparison.InvariantCulture) &&
                    !l.StartsWith("[assembly: AssemblyFileVersion(", StringComparison.InvariantCulture) &&
                    !l.StartsWith("[assembly: AssemblyInformationalVersion(", StringComparison.InvariantCulture) &&
                    !string.IsNullOrWhiteSpace(l)));
        }
    }
}
/// <summary>
/// Extension methods that help to get the target framework for a assembly.
/// </summary>
internal static class TargetFrameworkExtensions
{
    /// <summary>
    /// Gets the target framework for an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to get the target framework for.</param>
    /// <returns>The target framework or null if not known.</returns>
    internal static string? GetTargetFrameworkName(this Assembly assembly)
    {
        var targetFrameworkAttribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        return GetTargetFrameworkName(targetFrameworkAttribute?.FrameworkName);
    }

    private static string? GetTargetFrameworkName(string? frameworkName) =>
        frameworkName switch
        {
            ".NETCoreApp,Version=v8.0" => "net8.0",
            ".NETCoreApp,Version=v7.0" => "net7.0",
            ".NETCoreApp,Version=v6.0" => "net6.0",
            ".NETCoreApp,Version=v5.0" => "net5.0",
            ".NETCoreApp,Version=v3.1" => "netcoreapp3.1",
            ".NETCoreApp,Version=v3.0" => "netcoreapp3.0",
            ".NETCoreApp,Version=v2.2" => "netcoreapp2.2",
            ".NETCoreApp,Version=v2.1" => "netcoreapp2.1",
            ".NETCoreApp,Version=v2.0" => "netcoreapp2.0",
            ".NETCoreApp,Version=v1.1" => "netcoreapp1.1",
            ".NETCoreApp,Version=v1.0" => "netcoreapp1.0",
            ".NETStandard,Version=v2.1" => "netstandard2.1",
            ".NETStandard,Version=v2.0" => "netstandard2.0",
            ".NETStandard,Version=v1.6" => "netstandard1.6",
            ".NETStandard,Version=v1.5" => "netstandard1.5",
            ".NETStandard,Version=v1.4" => "netstandard1.4",
            ".NETStandard,Version=v1.3" => "netstandard1.3",
            ".NETStandard,Version=v1.2" => "netstandard1.2",
            ".NETStandard,Version=v1.1" => "netstandard1.1",
            ".NETStandard,Version=v1.0" => "netstandard1.0",
            ".NETFramework,Version=v4.8" => "net48",
            ".NETFramework,Version=v4.7.2" => "net472",
            ".NETFramework,Version=v4.7.1" => "net471",
            ".NETFramework,Version=v4.7" => "net47",
            ".NETFramework,Version=v4.6.2" => "net462",
            ".NETFramework,Version=v4.6.1" => "net461",
            ".NETFramework,Version=v4.6" => "net46",
            ".NETFramework,Version=v4.5.2" => "net452",
            ".NETFramework,Version=v4.5.1" => "net451",
            ".NETFramework,Version=v4.5" => "net45",
            ".NETFramework,Version=v4.0.3" => "net403",
            ".NETFramework,Version=v4.0" => "net40",
            ".NETFramework,Version=v3.5" => "net35",
            ".NETFramework,Version=v2.0" => "net20",
            ".NETFramework,Version=v1.1" => "net11",
            _ => null,
        };
}
