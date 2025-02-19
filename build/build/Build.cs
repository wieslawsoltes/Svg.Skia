using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.CI.AzurePipelines;
using System;
using Nuke.Common.Tools.GitVersion;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion] 
    readonly GitVersion GitVersion;

    [Parameter("configuration")]
    public string Configuration { get; set; }

    [Parameter("version-suffix")]
    public string VersionSuffix { get; set; }

    public bool IsRunningOnAzure { get; set; }

    public string Version { get; set; }

    [Parameter("publish-framework")]
    public string PublishFramework { get; set; }

    [Parameter("publish-runtime")]
    public string PublishRuntime { get; set; }

    [Parameter("publish-project")]
    public string PublishProject { get; set; }

    [Parameter("publish-self-contained")]
    public bool PublishSelfContained { get; set; } = true;

    AbsolutePath SourceDirectory => RootDirectory / "src";

    AbsolutePath TestsDirectory => RootDirectory / "tests";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    protected override void OnBuildInitialized()
    {
        Configuration = Configuration ?? "Release";
        VersionSuffix = VersionSuffix ?? "";
        Version = new Version(GitVersion.Major, GitVersion.Minor, GitVersion.Patch).ToString();
        IsRunningOnAzure = Host is AzurePipelines || Environment.GetEnvironmentVariable("LOGNAME") == "vsts";

        if(IsRunningOnAzure)
        {
            // Always use branch name as minor part of version (must be an integer, i.e. complete naming release/2)
            var minor = int.Parse(AzurePipelines.Instance.SourceBranchName);
            var gruntVersion = new Version(GitVersion.Major, minor, GitVersion.Patch);
            Version = gruntVersion.ToString();
        }
    }

    private void DeleteDirectories(IReadOnlyCollection<string> directories)
    {
        foreach (var directory in directories)
        {
            DeleteDirectory(directory);
        }
    }

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            DeleteDirectories(GlobDirectories(TestsDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetLoggers("trx")
                .SetResultsDirectory(ArtifactsDirectory / "TestResults")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .SetOutputDirectory(ArtifactsDirectory / "NuGet")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Publish => _ => _
        .DependsOn(Test)
        .Requires(() => PublishRuntime)
        .Requires(() => PublishFramework)
        .Requires(() => PublishProject)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetProject(Solution.GetProject(PublishProject))
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .SetFramework(PublishFramework)
                .SetRuntime(PublishRuntime)
                .SetSelfContained(PublishSelfContained)
                .SetOutput(ArtifactsDirectory / "Publish" / PublishProject + "-" + PublishFramework + "-" + PublishRuntime));
        });
}
