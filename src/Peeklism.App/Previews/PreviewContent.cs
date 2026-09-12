using Microsoft.UI.Xaml;

namespace Peeklism.App.Previews;

/// <param name="Element">The rendered preview.</param>
/// <param name="Title">File or folder name, shown in the header strip.</param>
/// <param name="Subtitle">Type, size and modification time.</param>
/// <param name="PreferredWidth">Requested width in device-independent pixels.</param>
/// <param name="PreferredHeight">Requested height in device-independent pixels.</param>
public sealed record PreviewContent(
    FrameworkElement Element,
    string Title,
    string Subtitle,
    int PreferredWidth,
    int PreferredHeight);
