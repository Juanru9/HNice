namespace HNice.Model.Encryption.ShockWaveBuffer;

/// <summary>
/// Accumulates raw TCP data until complete packets can be extracted. TCP does not preserve message
/// boundaries, so a read can contain half a packet or several of them.
/// </summary>
public abstract class PayloadBuffer
{
    private byte[] _buffer = new byte[8192];
    private int _start;
    private int _count;

    public bool IsEmpty => _count == 0;

    protected int Count => _count;

    protected ReadOnlySpan<byte> Data => _buffer.AsSpan(_start, _count);

    public void Push(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;

        if (_start + _count + data.Length > _buffer.Length)
        {
            if (_count + data.Length > _buffer.Length)
            {
                var newBuffer = new byte[Math.Max(_buffer.Length * 2, _count + data.Length)];
                Data.CopyTo(newBuffer);
                _buffer = newBuffer;
            }
            else
            {
                Data.CopyTo(_buffer);
            }
            _start = 0;
        }

        data.CopyTo(_buffer.AsSpan(_start + _count));
        _count += data.Length;
    }

    /// <summary>Removes and returns everything that has not been consumed as a packet yet.</summary>
    public byte[] TakeAll()
    {
        var remaining = Data.ToArray();
        Consume(_count);
        return remaining;
    }

    protected void Consume(int length)
    {
        _start += length;
        _count -= length;
        if (_count == 0)
        {
            _start = 0;
        }
    }
}
