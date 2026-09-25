namespace Peeklism.Core.Viewing;

/// <summary>File launches never join the background preview instance or install another hook.</summary>
public sealed record ViewerRequest(bool IsViewer, IReadOnlyList<string> Paths, bool OpenSettings = false)
{
    public static ViewerRequest Parse(IEnumerable<string> arguments)
    {
        var viewer = false;
        var openSettings = false;
        var paths = new List<string>();
        var literal = false;
        foreach (var argument in arguments)
        {
            if (!literal && argument == "--") { literal = true; continue; }
            if (!literal && argument.Equals("--viewer", StringComparison.OrdinalIgnoreCase))
            {
                viewer = true;
                continue;
            }
            if (!literal && argument.Equals("--background", StringComparison.OrdinalIgnoreCase)) continue;
            if (!literal && argument.Equals("--settings", StringComparison.OrdinalIgnoreCase))
            {
                openSettings = true;
                continue;
            }
            // Preserve Unicode, spaces, and even missing paths so the UI can explain errors.
            paths.Add(argument);
        }
        return new ViewerRequest(viewer || paths.Count > 0, paths, openSettings);
    }
}
