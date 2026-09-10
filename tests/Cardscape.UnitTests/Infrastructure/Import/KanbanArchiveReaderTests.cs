using System.Text;
using Cardscape.Infrastructure.Import;

namespace Cardscape.UnitTests.Infrastructure.Import;

public sealed class KanbanArchiveReaderTests
{
    [Fact]
    public async Task ReadAsync_WhenArchiveIsValid_ReturnsParsedBoard()
    {
        await using var source = JsonStream("""
            [{"name":"Roadmap","lists":[{"id":"todo","name":"Todo"}]}]
            """);

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Name.Should().Be("Roadmap");
        result.Value[0].Lists.Should().ContainSingle()
            .Which.Id.Should().Be("todo");
    }

    [Fact]
    public async Task ReadAsync_WhenJsonIsInvalid_ReturnsStableValidationError()
    {
        await using var source = JsonStream("not-json");

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("imports.invalid_json");
        result.Error.Message.Should().Be("Kanban export is not valid JSON.");
    }

    [Fact]
    public async Task ReadAsync_WhenArchiveIsEmpty_ReturnsStableValidationError()
    {
        await using var source = JsonStream("[]");

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("imports.empty_archive");
    }

    [Fact]
    public async Task ReadAsync_WhenSeekablePayloadExceedsLimit_RejectsWithoutReading()
    {
        await using var source = new MemoryStream(new byte[KanbanArchiveReader.MaxBytes + 1]);

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("imports.payload_too_large");
        source.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_WhenPayloadIsExactlyLimit_ReturnsArchive()
    {
        byte[] json = Encoding.UTF8.GetBytes("[{\"name\":\"Boundary\"}]");
        byte[] payload = new byte[KanbanArchiveReader.MaxBytes];
        json.CopyTo(payload, 0);
        payload.AsSpan(json.Length).Fill((byte)' ');
        await using var source = new MemoryStream(payload);

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Name.Should().Be("Boundary");
    }

    [Fact]
    public async Task ReadAsync_WhenNonSeekablePayloadExceedsLimit_StopsAtBoundary()
    {
        await using var source = new CountingNonSeekableStream(KanbanArchiveReader.MaxBytes * 2L);

        var result = await KanbanArchiveReader.ReadAsync(
            source,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("imports.payload_too_large");
        source.BytesRead.Should().BeLessThanOrEqualTo(KanbanArchiveReader.MaxBytes + 16 * 1024);
        source.BytesRead.Should().BeLessThan(source.TotalBytes);
    }

    private static MemoryStream JsonStream(string json) =>
        new(Encoding.UTF8.GetBytes(json));

    private sealed class CountingNonSeekableStream(long totalBytes) : Stream
    {
        public long BytesRead { get; private set; }
        public long TotalBytes { get; } = totalBytes;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = (int)Math.Min(count, TotalBytes - BytesRead);
            Array.Clear(buffer, offset, bytesToRead);
            BytesRead += bytesToRead;
            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesToRead = (int)Math.Min(buffer.Length, TotalBytes - BytesRead);
            buffer.Span[..bytesToRead].Clear();
            BytesRead += bytesToRead;
            return ValueTask.FromResult(bytesToRead);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
