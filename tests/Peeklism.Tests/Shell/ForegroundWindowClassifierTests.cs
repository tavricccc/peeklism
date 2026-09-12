using Peeklism.Core.Shell;
using Xunit;

namespace Peeklism.Tests.Shell;

public sealed class ForegroundWindowClassifierTests
{
    private const nint Window = 0x1234;

    [Theory]
    [InlineData(WindowClassNames.ExplorerCabinet)]
    [InlineData(WindowClassNames.ExplorerLegacy)]
    public void ClassifiesExplorerWindowsFromTheClassNameAlone(string className)
    {
        var result = Classify(className, hasDescendants: false);

        Assert.Equal(FocusedWindowType.Explorer, result);
    }

    [Theory]
    [InlineData(WindowClassNames.DesktopProgman)]
    [InlineData(WindowClassNames.DesktopWorkerW)]
    public void ClassifiesTheDesktopOnlyWhenItHostsTheShellView(string className)
    {
        Assert.Equal(FocusedWindowType.Desktop, Classify(className, hasDescendants: true));
        Assert.Equal(FocusedWindowType.Invalid, Classify(className, hasDescendants: false));
    }

    [Fact]
    public void ClassifiesAFileDialogThatHostsAShellView()
    {
        var result = Classify(WindowClassNames.Dialog, hasDescendants: true);

        Assert.Equal(FocusedWindowType.Dialog, result);
    }

    [Fact]
    public void RejectsAMessageBoxWhichSharesTheDialogClassButHasNoShellView()
    {
        var result = Classify(WindowClassNames.Dialog, hasDescendants: false);

        Assert.Equal(FocusedWindowType.Invalid, result);
    }

    [Theory]
    [InlineData("Notepad")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("")]
    public void RejectsEveryOtherWindowSoTheKeyPressIsPassedThrough(string className)
    {
        var result = Classify(className, hasDescendants: true);

        Assert.Equal(FocusedWindowType.Invalid, result);
    }

    [Fact]
    public void RejectsAnEmptyWindowHandleWithoutCallingIntoTheShell()
    {
        var result = ForegroundWindowClassifier.Classify(
            0,
            _ => throw new InvalidOperationException("類別名稱不該被查詢。"),
            (_, _) => throw new InvalidOperationException("子視窗不該被列舉。"));

        Assert.Equal(FocusedWindowType.Invalid, result);
    }

    [Fact]
    public void MatchesTheClassNameCaseSensitivelyBecauseTheShellNeverVariesIt()
    {
        var result = Classify("cabinetwclass", hasDescendants: false);

        Assert.Equal(FocusedWindowType.Invalid, result);
    }

    private static FocusedWindowType Classify(string className, bool hasDescendants) =>
        ForegroundWindowClassifier.Classify(Window, _ => className, (_, _) => hasDescendants);
}
