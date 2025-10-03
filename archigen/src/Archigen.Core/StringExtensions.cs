namespace Archigen.Core;

public static class StringExtensions
{
    public static string ToPascalCase(this string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        // If the string doesn't contain separators, treat it as a single segment but preserve existing casing for mixed case strings
        if (!value.Any(c => c == ' ' || c == '-' || c == '_' || c == '.'))
        {
            // If it's already mixed case (has both upper and lower), preserve it
            if (value.Any(char.IsUpper) && value.Any(char.IsLower))
                return value;

            // Otherwise, capitalize first letter and make rest lowercase
            return value.Length switch
            {
                0 => string.Empty,
                1 => char.ToUpperInvariant(value[0]).ToString(),
                _ => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant()
            };
        }

        string[] segments = value
            .Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Length switch
            {
                0 => string.Empty,
                1 => char.ToUpperInvariant(segment[0]).ToString(),
                _ => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant()
            })
            .ToArray();

        return string.Concat(segments);
    }

    public static string ToCamelCase(this string value)
    {
        string pascal = value.ToPascalCase();
        return pascal.Length switch
        {
            0 => pascal,
            1 => pascal.ToLowerInvariant(),
            _ => char.ToLowerInvariant(pascal[0]) + pascal[1..]
        };
    }

    public static string ToPlural(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.EndsWith("y", StringComparison.OrdinalIgnoreCase) && value.Length > 1)
            return value[..^1] + "ies";

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return value + "es";

        return value + "s";
    }
}
