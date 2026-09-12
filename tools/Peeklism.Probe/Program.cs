using Peeklism.Core.Shell;

// Manual harness for the riskiest part of Peeklism: proving that the foreground window type
// and its selection can be read reliably before any preview UI exists. Leave it running,
// click around File Explorer and the desktop, and watch the line change.
using var executor = new StaExecutor("Peeklism.Probe.Sta");
var provider = new ShellSelectionProvider(executor);

Console.WriteLine("Peeklism probe — 切換到檔案總管或桌面並選取檔案，Ctrl+C 結束。");
Console.WriteLine();

var previous = string.Empty;
while (true)
{
    var handle = NativeForegroundWindowClassifier.GetForegroundWindow();
    var windowType = NativeForegroundWindowClassifier.Classify(handle);
    var selection = provider.GetSelection(windowType, handle);
    var line = $"{windowType,-8} hwnd=0x{handle:X}  count={selection.Count}  path={selection.Path ?? "-"}";
    if (line != previous)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {line}");
        previous = line;
    }

    await Task.Delay(250);
}
