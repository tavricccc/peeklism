namespace Peeklism.Core.Shell;

/// <summary>
/// What the shell surface currently has selected.
/// </summary>
/// <param name="Path">
/// Filesystem path of the first selected item, or <see langword="null"/> when nothing is
/// selected or the item has no path (virtual desktop entries such as This PC).
/// </param>
/// <param name="Count">How many items are selected, whether or not they have paths.</param>
public sealed record ShellSelection(string? Path, int Count)
{
    public static readonly ShellSelection Empty = new(null, 0);

    public bool HasPath => !string.IsNullOrEmpty(Path);
}
