using Archigen.Core;

namespace Archigen.Generator;

public sealed class CrudGenerationOptions
{
    public required string ProjectName { get; init; }
    public required string EntityName { get; init; }
    public string DbContextName { get; init; } = "BaseDbContext";
    public string IdType { get; init; } = "int";
    public IList<PropertyDefinition> Properties { get; init; } = new List<PropertyDefinition>();
    public bool EnableSecurity { get; init; } = false;
}
