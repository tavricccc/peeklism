using System.Runtime.InteropServices;
using Windows.Management.Deployment;

namespace Peeklism.Bootstrap;

/// <summary>
/// Registers the shared Windows App SDK runtime before the installer's own interface starts.
/// </summary>
/// <remarks>
/// Peeklism's WinUI is framework-dependent: the Windows App SDK binaries come from one MSIX
/// framework package that Windows keeps in a single shared location, instead of a 145 MB copy
/// inside every installation. Flowlism and Downlism point at the same package, so the three of
/// them stop carrying three identical copies on disk and, whenever more than one is open,
/// three copies of the same code pages in memory.
///
/// The packages travel inside the installer rather than being downloaded. It makes the download
/// larger and the installation simpler: no network at the one moment the product cannot yet
/// run, no progress bar that stalls on someone's hotel wifi, no second thing that can fail.
/// What is shared is where they end up, not where they come from.
///
/// This runs here rather than in the setup interface because the setup interface is itself a
/// WinUI application — it cannot be the thing that registers what it needs in order to start.
/// The bootstrap launcher is plain .NET with its own copy of the runtime, so it starts on a
/// machine that has nothing.
///
/// Declining, or failing, is no longer the end of the install: the same installer carries a
/// layout that needs none of this, and the caller falls back to it.
///
/// No elevation is requested. Windows stages the packages and registers them for the user
/// running the installer, which is all a per-user installation needs; provisioning them for
/// every user on the machine is the only part that wants an administrator.
/// </remarks>
internal static class WindowsAppRuntime
{
    /// <summary>
    /// The framework package Peeklism's WinUI resolves at run time. The family name is fixed by
    /// Microsoft's publisher identity, so it can be written down rather than discovered.
    /// </summary>
    private const string FrameworkFamilyName = "Microsoft.WindowsAppRuntime.2_8wekyb3d8bbwe";

    /// <summary>
    /// Matches the Microsoft.WindowsAppSDK version the app is built against, and must be moved
    /// with it. A newer package satisfies the requirement; an older one does not, because the
    /// app may call APIs it does not have.
    /// </summary>
    private static readonly Version Minimum = new(2, 4, 0, 0);

    /// <summary>
    /// Registered in this order. The framework carries the binaries the other three refer to,
    /// and only x64 ships: the x86 packages exist to run x86 applications, and Peeklism is x64.
    /// </summary>
    private static readonly string[] PackageFiles =
    [
        "Microsoft.WindowsAppRuntime.2.msix",
        "Microsoft.WindowsAppRuntime.Main.2.msix",
        "Microsoft.WindowsAppRuntime.Singleton.2.msix",
        "Microsoft.WindowsAppRuntime.DDLM.2.msix",
    ];

    /// <summary>Whether a new enough framework package is already registered for this user.</summary>
    public static bool IsPresent()
    {
        try
        {
            foreach (var package in new PackageManager().FindPackagesForUser(string.Empty, FrameworkFamilyName))
            {
                var version = package.Id.Version;
                if (new Version(version.Major, version.Minor, version.Build, version.Revision) >= Minimum) return true;
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or COMException)
        {
            // The deployment API is unavailable or refused. Reported as absent so the caller
            // tries and produces a real error, rather than failing much later when the app
            // cannot find its own UI framework.
        }

        return false;
    }

    /// <summary>
    /// Registers every package that ships beside the installer, then confirms the result.
    /// </summary>
    /// <remarks>
    /// Individual failures are swallowed on purpose. A machine part-way through an earlier
    /// attempt already has some of these, and adding a package that exists is reported as a
    /// failure; what decides the outcome is whether the framework is usable afterwards, which is
    /// checked once at the end.
    /// </remarks>
    /// <param name="directory">Where the packages were unpacked to.</param>
    /// <param name="progress">The caller's dialog, which is already on screen by this point.</param>
    public static void Install(string directory, ProgressDialog progress)
    {
        if (!Directory.Exists(directory)) throw new IOException($"找不到執行環境套件資料夾：{directory}");

        var available = PackageFiles.Where(name => File.Exists(Path.Combine(directory, name))).ToArray();
        if (available.Length == 0) throw new IOException($"{directory} 裡沒有任何執行環境套件。");

        var manager = new PackageManager();
        Exception? firstFailure = null;

        // One unit of progress per package, each divided into its own hundred, so the bar
        // reflects the whole job rather than restarting four times.
        var total = available.Length * 100L;

        for (var index = 0; index < available.Length; index++)
        {
            var path = Path.Combine(directory, available[index]);
            var completed = index * 100L;

            try
            {
                var operation = manager.AddPackageAsync(new Uri(path), null, DeploymentOptions.None);
                operation.Progress = (_, state) => progress.Report(completed + state.percentage, total);

                if (operation.AsTask().GetAwaiter().GetResult().ExtendedErrorCode is { } error) firstFailure ??= error;
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }

            progress.Report(completed + 100, total);
        }

        if (IsPresent()) return;

        throw new IOException(
            "無法登錄 Windows App 執行環境。" + (firstFailure is null ? string.Empty : "\n" + firstFailure.Message));
    }
}
