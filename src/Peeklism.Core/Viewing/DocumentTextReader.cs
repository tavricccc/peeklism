using System.Text;

namespace Peeklism.Core.Viewing;

public static class DocumentTextReader
{
    public const long MaximumBytes = 64L * 1024 * 1024;

    public static async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default,
        long maximumBytes = MaximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        void CheckLimit()
        {
            // Recheck length as well as position: a live log may grow after opening.
            if (input.Length > maximumBytes || input.Position > maximumBytes)
                throw new IOException($"文字檔超過讀取上限（{maximumBytes:N0} bytes）。請使用大型文字檔工具；查看器不會截斷內容。");
        }
        CheckLimit();
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = new StringBuilder((int)Math.Min(input.Length, 1024 * 1024));
        var buffer = new char[32768];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            CheckLimit();
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }
}
