using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.DotnetPackaging;
using Nuke.Common.Utilities.Collections;
using Serilog;
using System;
using System.Linq;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;

[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(Compile) })]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    AbsolutePath OutputDirectory => RootDirectory / "output";
    GitHubActions GitHubActions => GitHubActions.Instance;

    Target Print => _ => _
        .Executes(() =>
        {
            Log.Information("Branch = {Branch}", GitHubActions?.Ref);
            Log.Information("Commit = {Commit}", GitHubActions?.Sha);
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(s => s
                    .SetProjectFile("skipper-paste.csproj")
                    .SetRuntime("linux-x64"));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(Print)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile("skipper-paste.csproj")
                .SetConfiguration(Configuration)
                .SetPublishSingleFile(true)
                .SetPublishTrimmed(true)
                .SetSelfContained(true)
                .SetRuntime("linux-x64")
                .SetOutputDirectory(OutputDirectory / "linux-x64")
                .EnableNoRestore());
        });

}
