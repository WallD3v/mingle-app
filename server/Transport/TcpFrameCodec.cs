using System.Buffers.Binary;

namespace Mingle.Server.Transport;

public static class TcpFrameCodec
{
    public const int MaxFrameSize = 1024 * 1024;

    public static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        if (payload.Length > MaxFrameSize)
        {
            throw new InvalidDataException("Frame is too large.");
        }

        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, payload.Length);
        await stream.WriteAsync(lengthBuffer, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        var prefixRead = await ReadExactlyOrEofAsync(stream, lengthBuffer, cancellationToken);

        if (prefixRead == 0)
        {
            return null;
        }

        if (prefixRead < 4)
        {
            throw new EndOfStreamException("Incomplete frame length prefix.");
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        if (length <= 0 || length > MaxFrameSize)
        {
            throw new InvalidDataException("Invalid frame size.");
        }

        var payload = new byte[length];
        var read = await ReadExactlyOrEofAsync(stream, payload, cancellationToken);
        if (read < length)
        {
            throw new EndOfStreamException("Incomplete frame payload.");
        }

        return payload;
    }

    private static async Task<int> ReadExactlyOrEofAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
