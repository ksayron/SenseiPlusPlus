using System.Xml.Linq;

namespace Sensei.ArchitectureTests;

public sealed class ProjectDependencyTests
{
    private static readonly string BackendRoot = FindBackendRoot();

    [Fact]
    public void Domain_projects_have_no_project_dependencies()
    {
        foreach (var project in ModuleProjects("Domain"))
        {
            Assert.Empty(ProjectReferences(project));
        }
    }

    [Fact]
    public void Application_projects_do_not_reference_infrastructure_or_api()
    {
        foreach (var project in ModuleProjects("Application"))
        {
            Assert.DoesNotContain(ProjectReferences(project), reference =>
                reference.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains(".Api", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Modules_do_not_reference_other_modules()
    {
        foreach (var project in Directory.EnumerateFiles(Path.Combine(BackendRoot, "src", "Modules"), "*.csproj", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Path.Combine(BackendRoot, "src", "Modules"), project);
            var module = relative.Split(Path.DirectorySeparatorChar)[0];
            Assert.DoesNotContain(ProjectReferences(project), reference =>
                reference.Contains($"{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !reference.Contains($"{Path.DirectorySeparatorChar}{module}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Local_agent_has_no_backend_module_database_or_ai_dependencies()
    {
        var repository = Directory.GetParent(BackendRoot)!.FullName;
        var tools = Path.Combine(repository, "agent-spp", "src");
        var core = Path.Combine(tools, "Sensei.LocalAgent.Core", "Sensei.LocalAgent.Core.csproj");
        var windows = Path.Combine(tools, "Sensei.LocalAgent.Windows", "Sensei.LocalAgent.Windows.csproj");
        var cli = Path.Combine(tools, "Sensei.LocalAgent.Cli", "Sensei.LocalAgent.Cli.csproj");
        var contracts = Path.Combine(repository, "shared", "Sensei.Client.Contracts", "Sensei.Client.Contracts.csproj");
        var sync = Path.Combine(tools, "Sensei.LocalAgent.Sync", "Sensei.LocalAgent.Sync.csproj");
        Assert.Empty(ProjectReferences(contracts));
        Assert.Empty(XDocument.Load(contracts).Descendants("PackageReference"));
        Assert.Equal(new[] { core, contracts }.Order(StringComparer.Ordinal), ProjectReferences(sync).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(XDocument.Load(sync).Descendants("PackageReference"), p =>
            new[] { "Npgsql", "EntityFramework", "Sensei.Modules", "Gateway" }.Any(x => p.Attribute("Include")!.Value.Contains(x, StringComparison.OrdinalIgnoreCase)));
        Assert.Empty(ProjectReferences(core));
        Assert.Equal(new[] { core }, ProjectReferences(windows));
        Assert.Equal(new[] { core, windows, Path.Combine(tools, "Sensei.LocalAgent.Sync", "Sensei.LocalAgent.Sync.csproj") }.Order(StringComparer.Ordinal), ProjectReferences(cli).Order(StringComparer.Ordinal));
        foreach (var project in new[] { core, windows, cli })
            Assert.Empty(XDocument.Load(project).Descendants("PackageReference"));
    }

    private static IEnumerable<string> ModuleProjects(string layer) =>
        Directory.EnumerateFiles(Path.Combine(BackendRoot, "src", "Modules"), $"*.{layer}.csproj", SearchOption.AllDirectories);

    private static IReadOnlyCollection<string> ProjectReferences(string project)
    {
        var document = XDocument.Load(project);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, element.Attribute("Include")!.Value)))
            .ToArray();
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sensei.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate backend/Sensei.sln.");
    }
}
