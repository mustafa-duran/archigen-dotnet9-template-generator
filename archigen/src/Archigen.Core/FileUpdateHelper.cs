namespace Archigen.Core;

public static class FileUpdateHelper
{
    public static void AddUsingIfMissing(string filePath, string usingStatement)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Any(line => line.Trim() == usingStatement))
            return;

        int insertIndex = 0;
        while (insertIndex < lines.Length && lines[insertIndex].StartsWith("using ", StringComparison.Ordinal))
            insertIndex++;

        var updated = lines.Take(insertIndex)
            .Append(usingStatement)
            .Concat(lines.Skip(insertIndex))
            .ToArray();

        File.WriteAllLines(filePath, updated);
    }

    public static void InsertAfter(string filePath, string marker, string[] contentLines)
    {
        string[] lines = File.ReadAllLines(filePath);
        int index = Array.FindIndex(lines, line => line.Contains(marker, StringComparison.Ordinal));
        if (index == -1)
            throw new InvalidOperationException($"Marker '{marker}' not found in {filePath}.");

        if (ContainsSequence(lines, contentLines))
            return;

        var updated = lines.Take(index + 1)
            .Concat(contentLines)
            .Concat(lines.Skip(index + 1))
            .ToArray();
        File.WriteAllLines(filePath, updated);
    }

    public static void InsertBefore(string filePath, string marker, string line)
    {
        string[] lines = File.ReadAllLines(filePath);
        int index = Array.FindIndex(lines, l => l.Contains(marker, StringComparison.Ordinal));
        if (index == -1)
            throw new InvalidOperationException($"Marker '{marker}' not found in {filePath}.");

        if (lines.Contains(line))
            return;

        var updated = lines.Take(index)
            .Append(line)
            .Concat(lines.Skip(index))
            .ToArray();
        File.WriteAllLines(filePath, updated);
    }

    private static bool ContainsSequence(string[] lines, string[] sequence)
    {
        for (int i = 0; i <= lines.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (lines[i + j].Trim() != sequence[j].Trim())
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return true;
        }

        return false;
    }
}
