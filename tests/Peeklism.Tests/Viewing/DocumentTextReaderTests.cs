using System.Text;
using Peeklism.Core.Viewing;
using Xunit;

namespace Peeklism.Tests.Viewing;

public sealed class DocumentTextReaderTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public async Task ReadsBomEncodedDocumentsWithoutTruncation(string encoding)
    {
        var text = new string('字', 300_000) + "END";
        await File.WriteAllTextAsync(_file, text, Encoding.GetEncoding(encoding));
        Assert.Equal(text, await DocumentTextReader.ReadAsync(_file));
    }

    [Fact]
    public async Task EmptyDocumentIsValid() => Assert.Equal("", await DocumentTextReader.ReadAsync(_file));

    [Fact]
    public async Task RejectsOversizeInsteadOfTruncating()
    {
        await File.WriteAllTextAsync(_file, new string('x', 1025));
        await Assert.ThrowsAsync<IOException>(() => DocumentTextReader.ReadAsync(_file, maximumBytes: 1024));
    }

    [Fact]
    public async Task SupportsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DocumentTextReader.ReadAsync(_file, cancellation.Token));
    }

    public void Dispose() => File.Delete(_file);
}
