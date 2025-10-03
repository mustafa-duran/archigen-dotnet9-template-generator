namespace Archigen.Core;

public sealed class EntityDefinition
{
    public required string Name { get; init; }
    public required string IdType { get; init; }
    public IReadOnlyList<PropertyDefinition> Properties { get; init; } = Array.Empty<PropertyDefinition>();

    public string NamePascal => Name.ToPascalCase();
    public string NameCamel => Name.ToCamelCase();
    public string PluralPascal => NamePascal.ToPlural();
    public string PluralCamel => NameCamel.ToPlural();
}
