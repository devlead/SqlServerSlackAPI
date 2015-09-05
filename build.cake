///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target              = Argument<string>("target", "Default");
var configuration       = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions           = GetFiles("./**/*.sln");
var solutionDirs        = solutions.Select(solution => solution.GetDirectory());
var isLocalBuild        = !AppVeyor.IsRunningOnAppVeyor;
var isPullRequest       = AppVeyor.Environment.PullRequest.IsPullRequest;
var releaseNotes        = ParseReleaseNotes("./ReleaseNotes.md");
var version             = releaseNotes.Version.ToString();
var binDir              = "./src/SqlServerSlackAPI/bin/" + configuration;
var nugetRoot           = "./nuget/";
var semVersion          = isLocalBuild
                                ? version
                                : string.Concat(version, "-build-", AppVeyor.Environment.Build.Number);
var assemblyInfo        = new AssemblyInfoSettings {
                                Title                   = "SqlServerSlackAPI",
                                Description             = "SQL Server Slack API",
                                Product                 = "SqlServerSlackAPI",
                                Company                 = "WCOM AB",
                                Version                 = version,
                                FileVersion             = version,
                                InformationalVersion    = semVersion,
                                Copyright               = string.Format("Copyright © WCOM AB {0}", DateTime.Now.Year),
                                CLSCompliant            = true
                            };
var nuGetPackSettings   = new NuGetPackSettings {
                                Id                      = assemblyInfo.Product,
                                Version                 = assemblyInfo.InformationalVersion,
                                Title                   = assemblyInfo.Title,
                                Authors                 = new[] {assemblyInfo.Company},
                                Owners                  = new[] {assemblyInfo.Company},
                                Description             = assemblyInfo.Description,
                                Summary                 = "SQL Server CLR Assembly for communicating with Slack chat service API",
                                ProjectUrl              = new Uri("https://github.com/WCOMAB/SqlServerSlackAPI"),
                                IconUrl                 = new Uri("http://cdn.rawgit.com/WCOMAB/nugetpackages/master/Chocolatey/icons/wcom.png"),
                                LicenseUrl              = new Uri("https://github.com/WCOMAB/SqlServerSlackAPI/blob/master/LICENSE.md"),
                                Copyright               = assemblyInfo.Copyright,
                                ReleaseNotes            = releaseNotes.Notes.ToArray(),
                                Tags                    = new [] {"SQL", "SQLCLR", "Slack"},
                                RequireLicenseAcceptance= false,
                                Symbols                 = false,
                                NoPackageAnalysis       = true,
                                Files                   = new [] {new NuSpecContent {Source = "SqlServerSlackAPI.dll"}},
                                BasePath                = binDir,
                                OutputDirectory         = nugetRoot
                            };

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
});

Teardown(() =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var solutionDir in solutionDirs)
    {
        Information("Cleaning {0}", solutionDir);
        CleanDirectories(solutionDir + "/**/bin/" + configuration);
        CleanDirectories(solutionDir + "/**/obj/" + configuration);
    }
});

Task("Restore")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}", solution);
        NuGetRestore(solution);
    }
});

Task("SolutionInfo")
    .Does(() =>
{
    var file = "./src/SolutionInfo.cs";
    CreateAssemblyInfo(file, assemblyInfo);
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("SolutionInfo")
    .Does(() =>
{
    // Build all solutions.
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);
        MSBuild(solution, settings =>
            settings.SetPlatformTarget(PlatformTarget.MSIL)
                .WithProperty("TreatWarningsAsErrors","true")
                .WithTarget("Build")
                .SetConfiguration(configuration));
    }
});

Task("Sign")
    .IsDependentOn("Build")
    .Does(() =>
{
    // Sign all output assemblies
    foreach(var solutionDir in solutionDirs)
    {
        var assemblies = GetFiles(string.Format(
                "{0}/**/bin/{1}/*.dll",
                solutionDir,
                configuration));
        Sign(
            assemblies,
            new SignToolSignSettings {
                TimeStampUri = new Uri("http://timestamp.digicert.com"),
                CertPath = "./src/SqlServerSlackAPI/SqlServerSlackAPI.pfx",
                Password = "SqlServerSlackAPI"
            });
    }
});



Task("Create-NuGet-Package")
    .IsDependentOn("Sign")
    .Does(() =>
{
    if (!DirectoryExists(nugetRoot))
    {
        CreateDirectory(nugetRoot);
    }
    NuGetPack(nuGetPackSettings);
});

Task("Default")
    .IsDependentOn("Create-NuGet-Package");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
