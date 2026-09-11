using System.Net.Http;

namespace Cardscape.Sdk;

internal sealed class HttpResponseStream(Stream content, HttpResponseMessage response) : Stream
{
    private bool _disposed;

    public override bool CanRead => content.CanRead;
    public override bool CanSeek => content.CanSeek;
    public override bool CanWrite => content.CanWrite;
    public override long Length => content.Length;

    public override long Position
    {
        get => content.Position;
        set => content.Position = value;
    }

    public override void Flush() => content.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        content.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        content.Read(buffer, offset, count);

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        content.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        content.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => content.Seek(offset, origin);

    public override void SetLength(long value) => content.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        content.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        content.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        content.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            content.Dispose();
            response.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
