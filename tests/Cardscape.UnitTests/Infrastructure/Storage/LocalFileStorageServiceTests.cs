using Cardscape.Infrastructure.Storage;

namespace Cardscape.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_PublishesFinalFileOnlyAfterCopyCompletes()
    {
        using var directory = new TemporaryDirectory();
        const string key = "cards/card-id/document.txt";
        string finalPath = Path.Combine(directory.Path, "cards", "card-id", "document.txt");
        var content = new ControlledCopyStream(
            [1, 2, 3, 4],
            duringCopy: () => File.Exists(finalPath).Should().BeFalse());
        var sut = new LocalFileStorageService(directory.Path);

        string savedKey = await sut.SaveAsync(
            key, content, "text/plain", TestContext.Current.CancellationToken);

        savedKey.Should().Be(key);
        File.Exists(finalPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(finalPath, TestContext.Current.CancellationToken))
            .Should().Equal(1, 2, 3, 4);
        Directory.GetFiles(directory.Path, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_WhenCopyFailsOrIsCancelled_LeavesNoFinalOrTemporaryFile(bool cancel)
    {
        using var directory = new TemporaryDirectory();
        const string key = "cards/card-id/interrupted.bin";
        string finalPath = Path.Combine(directory.Path, "cards", "card-id", "interrupted.bin");
        var content = new ControlledCopyStream([9, 8], failAfterWrite: true, cancel: cancel);
        var sut = new LocalFileStorageService(directory.Path);

        Func<Task> act = () => sut.SaveAsync(key, content, "application/octet-stream");

        if (cancel)
        {
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            await act.Should().ThrowAsync<IOException>().WithMessage("copy interrupted");
        }

        File.Exists(finalPath).Should().BeFalse();
        Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_KeyEscapingIntoASiblingDirectoryWithTheSamePrefix_IsRejected()
    {
        using var directory = new TemporaryDirectory();
        string root = Path.Combine(directory.Path, "storage");
        var sut = new LocalFileStorageService(root);

        Func<Task> act = () => sut.SaveAsync(
            "../storage2/escaped.txt", new MemoryStream([1]), "text/plain");

        await act.Should().ThrowAsync<InvalidOperationException>();
        File.Exists(Path.Combine(directory.Path, "storage2", "escaped.txt")).Should().BeFalse();
    }

    private sealed class ControlledCopyStream(
        byte[] bytes,
        Action? duringCopy = null,
        bool failAfterWrite = false,
        bool cancel = false) : MemoryStream(bytes)
    {
        private readonly byte[] _bytes = bytes;
        private readonly Action? _duringCopy = duringCopy;
        private readonly bool _failAfterWrite = failAfterWrite;
        private readonly bool _cancel = cancel;

        public override async Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            _duringCopy?.Invoke();
            await destination.WriteAsync(_bytes, cancellationToken);
            _duringCopy?.Invoke();
            if (_failAfterWrite)
            {
                if (_cancel)
                {
                    throw new OperationCanceledException();
                }

                throw new IOException("copy interrupted");
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"cardscape-storage-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
