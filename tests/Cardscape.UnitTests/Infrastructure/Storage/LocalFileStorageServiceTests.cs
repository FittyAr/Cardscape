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

    private sealed class ControlledCopyStream : MemoryStream
    {
        private readonly byte[] bytes;
        private readonly Action? duringCopy;
        private readonly bool failAfterWrite;
        private readonly bool cancel;

        public ControlledCopyStream(
            byte[] bytes,
            Action? duringCopy = null,
            bool failAfterWrite = false,
            bool cancel = false)
            : base(bytes)
        {
            this.bytes = bytes;
            this.duringCopy = duringCopy;
            this.failAfterWrite = failAfterWrite;
            this.cancel = cancel;
        }

        public override async Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            duringCopy?.Invoke();
            await destination.WriteAsync(bytes, cancellationToken);
            duringCopy?.Invoke();
            if (failAfterWrite)
            {
                if (cancel)
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
