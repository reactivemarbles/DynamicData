#load nuget:https://www.myget.org/F/reactiveui?package=ReactiveUI.Cake.Recipe&prerelease

Environment.SetVariableNames();

// Whitelisted Packages
var packageWhitelist = new[] 
{ 
    MakeAbsolute(File("./src/DynamicData/DynamicData.csproj")),
    MakeAbsolute(File("./src/DynamicData.Profile/DynamicData.Profile.csproj")),
    MakeAbsolute(File("./src/DynamicData.Benchmarks/DynamicData.Benchmarks.csproj")),
};

var packageTestWhitelist = new[]
{
    MakeAbsolute(File("./src/DynamicData.Tests/DynamicData.Tests.csproj")),
};

BuildParameters.SetParameters(context: Context, 
                            buildSystem: BuildSystem,
                            title: "DynamicData",
                            whitelistPackages: packageWhitelist,
                            whitelistTestPackages: packageTestWhitelist,
                            artifactsDirectory: "./artifacts",
                            sourceDirectory: "./src");

ToolSettings.SetToolSettings(context: Context, usePrereleaseMsBuild: true);

Build.Run();
