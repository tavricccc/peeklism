namespace Peeklism.Core.Shell;

/// <summary>
/// What kind of shell surface currently owns the foreground window. Anything other than
/// <see cref="Invalid"/> means the space bar belongs to Peeklism rather than to the app.
/// </summary>
public enum FocusedWindowType
{
    /// <summary>No shell surface. The key press must be passed through untouched.</summary>
    Invalid,

    /// <summary>The desktop itself, whose shell view lives under Progman or a WorkerW.</summary>
    Desktop,

    /// <summary>A File Explorer window.</summary>
    Explorer,

    /// <summary>A common file dialog hosted by some other process.</summary>
    Dialog,
}
