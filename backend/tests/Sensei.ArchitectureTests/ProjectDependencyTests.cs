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
