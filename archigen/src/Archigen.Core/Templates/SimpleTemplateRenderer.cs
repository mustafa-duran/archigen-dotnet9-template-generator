namespace Archigen.Core.Templates;

public sealed class SimpleTemplateRenderer
{
    private readonly IReadOnlyDictionary<string, string> _tokens;

    public SimpleTemplateRenderer(IReadOnlyDictionary<string, string> tokens)
    {
        _tokens = tokens;
    }

    public string Render(string template)
    {
        string result = template;
        foreach ((string key, string value) in _tokens)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return result;
    }
}
