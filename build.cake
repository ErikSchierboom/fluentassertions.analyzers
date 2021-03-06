#tool "nuget:?package=GitVersion.CommandLine"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./artifacts");
var solutionFile = File("./src/FluentAssertions.Analyzers.sln");
var testCsproj = File("./src/FluentAssertions.Analyzers.Tests/FluentAssertions.Analyzers.Tests.csproj");
var nuspecFile = File("./src/FluentAssertions.Analyzers/FluentAssertions.Analyzers.nuspec");
var version = GetCurrentVersion(Context);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Update-Version")
    .WithCriteria(AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
    {
		version += $"-preview{AppVeyor.Environment.Build.Number}";

		Information($"Version: {version}");
    });

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(buildDir);
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore(solutionFile);
    });

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
    {
        DotNetCoreBuild(solutionFile, new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            OutputDirectory = buildDir
        });
    });

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        DotNetCoreTest(testCsproj, new DotNetCoreTestSettings
        {
            Filter = "TestCategory=Completed",
            Configuration = configuration
        });
    });

Task("Pack")
    .Does(() =>
    {
        var nuGetPackSettings = new NuGetPackSettings
        {
            OutputDirectory = buildDir,
            Version = version
        };
        NuGetPack(nuspecFile, nuGetPackSettings);
    });

Task("Publish-NuGet")
	.WithCriteria(AppVeyor.IsRunningOnAppVeyor)
	.WithCriteria(HasEnvironmentVariable("NUGET_API_KEY"))
	.WithCriteria(HasEnvironmentVariable("NUGET_API_URL"))
    .Does(() =>
	{
		var apiKey = EnvironmentVariable("NUGET_API_KEY");
        var apiUrl = EnvironmentVariable("NUGET_API_URL");
        
		var nupkgFile = File($"{buildDir}/FluentAssertions.Analyzers.{version}.nupkg");
		Information($"Publishing nupkg file '{nupkgFile}'...");

		NuGetPush(nupkgFile, new NuGetPushSettings
		{
            ApiKey = apiKey,
            Source = apiUrl
        });
	});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Update-Version")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

string GetCurrentVersion(ICakeContext context)
{
	return context.XmlPeek(nuspecFile, "nu:package/nu:metadata/nu:version", new XmlPeekSettings
	{
	    Namespaces = new Dictionary<string, string>
		{
			["nu"] = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"
		}
	});
}