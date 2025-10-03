namespace Archigen.Core;

public sealed class ProjectLayout
{
    public string SolutionRoot { get; }
    public string ProjectName { get; }

    public string CoreRoot => Path.Combine(SolutionRoot, "core");
    public string ProjectRoot => Path.Combine(SolutionRoot, "project");

    public string ApplicationPath => Path.Combine(ProjectRoot, $"{ProjectName}.Application");
    public string DomainPath => Path.Combine(ProjectRoot, $"{ProjectName}.Domain");
    public string InfrastructurePath => Path.Combine(ProjectRoot, $"{ProjectName}.Infrastructure");
    public string PersistencePath => Path.Combine(ProjectRoot, $"{ProjectName}.Persistence");
    public string WebApiPath => Path.Combine(ProjectRoot, $"{ProjectName}.WebAPI");

    private ProjectLayout(string solutionRoot, string projectName)
    {
        SolutionRoot = Path.GetFullPath(solutionRoot);
        ProjectName = projectName.ToPascalCase();
    }

    public static ProjectLayout Create(string solutionRoot, string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        if (!Directory.Exists(solutionRoot))
            throw new DirectoryNotFoundException($"Solution root '{solutionRoot}' was not found.");

        return new ProjectLayout(solutionRoot, projectName);
    }

    public bool LayerExists(string layerName)
    {
        return Directory.Exists(GetLayerPath(layerName));
    }

    public string GetLayerPath(string layerName) => layerName switch
    {
        "Application" => ApplicationPath,
        "Domain" => DomainPath,
        "Infrastructure" => InfrastructurePath,
        "Persistence" => PersistencePath,
        "WebAPI" => WebApiPath,
        _ => throw new ArgumentException($"Unknown layer '{layerName}'", nameof(layerName))
    };
}
