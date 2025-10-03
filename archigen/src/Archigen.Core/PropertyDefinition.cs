namespace Archigen.Core;

public sealed class PropertyDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }

    public string PropertyName => Name.ToPascalCase();
    public string FieldName => Name.ToCamelCase();
    public bool IsNullable => Type.EndsWith('?');
    public string NonNullableType => IsNullable ? Type.TrimEnd('?') : Type;
}
