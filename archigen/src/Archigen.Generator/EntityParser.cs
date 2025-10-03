using System.Text.RegularExpressions;
using Archigen.Core;

namespace Archigen.Generator;

public static class EntityParser
{
    private static readonly Regex ClassRegex = new(@"class\s+(?<name>\w+)\s*:\s*(?<base>[^\r\n{]+)");
    // Updated regex to support complex types like List<T>, Dictionary<K,V>, arrays, etc.
    private static readonly Regex PropertyRegex = new(@"public\s+(?<type>[\w\?\[\]<>,\s]+)\s+(?<name>\w+)\s*{\s*get;\s*set;\s*}");

    public static EntityDefinition Parse(string filePath)
    {
        string content = File.ReadAllText(filePath);

        Match classMatch = ClassRegex.Match(content);
        if (!classMatch.Success)
            throw new InvalidOperationException($"Could not locate class definition in '{filePath}'.");

        string name = classMatch.Groups["name"].Value;
        string basePart = classMatch.Groups["base"].Value.Trim();
        string idType = ExtractIdType(basePart);

        List<PropertyDefinition> properties = PropertyRegex
            .Matches(content)
            .Select(match => new PropertyDefinition
            {
                Name = match.Groups["name"].Value,
                Type = match.Groups["type"].Value
            })
            .Where(property => !IsBaseProperty(property.PropertyName))
            .ToList();

        return new EntityDefinition
        {
            Name = name,
            IdType = idType,
            Properties = properties
        };
    }

    private static string ExtractIdType(string basePart)
    {
        int start = basePart.IndexOf('<');
        int end = basePart.IndexOf('>');
        if (start >= 0 && end > start)
            return basePart[(start + 1)..end].Trim();

        return "int";
    }

    private static bool IsBaseProperty(string propertyName) => propertyName is "Id" or "CreatedDate" or "UpdatedDate" or "DeletedDate";
}
