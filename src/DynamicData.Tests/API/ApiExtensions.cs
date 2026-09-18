using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using PublicApiGenerator;

namespace DynamicData.APITests;

/// <summary>Compares the public API with a reviewed, runtime-specific baseline.</summary>
[ExcludeFromCodeCoverage]
public static class ApiExtensions
{
    public static async Task CheckApproval(this Assembly assembly, string[] namespaces,
        [CallerFilePath] string filePath = "", [CallerMemberName] string testName = "")
    {
        var apiText = assembly.GeneratePublicApi(new ApiGeneratorOptions { AllowNamespacePrefixes = namespaces });
        var actual = Normalize(apiText);
        var baseline = Path.Combine(Path.GetDirectoryName(filePath)!,
            $"ApiApprovalTests.{testName}.DotNet{Environment.Version.Major}_0.verified.txt");
        var received = baseline.Replace(".verified.txt", ".received.txt", StringComparison.Ordinal);
        var expected = File.Exists(baseline) ? Normalize(await File.ReadAllTextAsync(baseline)) : null;

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(received, actual);
        }
        else if (File.Exists(received))
        {
            File.Delete(received);
        }

        await Assert.That(actual).IsEqualTo(expected)
            .Because($"The public API must match the reviewed baseline at {baseline}; inspect {received} before updating it.");
    }

    private static string Normalize(string text) => string.Join("\n", text.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n')
        .Where(line => !string.IsNullOrWhiteSpace(line)
            && !line.StartsWith("[assembly: AssemblyVersion(", StringComparison.Ordinal)
            && !line.StartsWith("[assembly: AssemblyFileVersion(", StringComparison.Ordinal)
            && !line.StartsWith("[assembly: AssemblyInformationalVersion(", StringComparison.Ordinal)
            && !line.StartsWith("[assembly: System.Reflection.AssemblyMetadata(", StringComparison.Ordinal)));
}
