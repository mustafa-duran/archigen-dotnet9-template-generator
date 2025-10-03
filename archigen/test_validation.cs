using System;

public class ValidationTest
{
    public static void Main()
    {
        try
        {
            ValidatePropertyName("isValid", "VisitType");
            Console.WriteLine("❌ Validation failed - should have thrown exception!");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"✅ Validation works: {ex.Message}");
        }

        try
        {
            ValidatePropertyName("IsValid", "VisitType");
            Console.WriteLine("✅ Valid property name passed validation");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"❌ Valid property failed: {ex.Message}");
        }
    }

    static void ValidatePropertyName(string propertyName, string? entityName = null)
    {
        // 1. Property name cannot be null or empty
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name cannot be empty.");
        }

        // 2. Property name must follow PascalCase convention (start with uppercase letter)
        if (propertyName.Length > 0 && !char.IsUpper(propertyName[0]))
        {
            throw new ArgumentException($"Property name '{propertyName}' must follow PascalCase convention and start with an uppercase letter. Did you mean '{char.ToUpper(propertyName[0])}{propertyName.Substring(1)}'?");
        }
    }
}
