namespace HNice.Model.Encryption.ShockWaveBuffer;

/// <summary>
/// Server to client plaintext stream: every packet is terminated by chr(1).
/// The returned packets do not include the terminator.
/// </summary>
public sealed class ShockwaveBuffer : PayloadBuffer
{
    public bool TryReceive(out byte[] packet)
    {
        var end = Data.IndexOf(Constants.PACKET_ENDER_BYTE);
        if (end < 0)
        {
            packet = Array.Empty<byte>();
            return false;
        }

        packet = Data[..end].ToArray();
        Consume(end + 1);
        return true;
    }

    public byte[][] Receive()
    {
        var packets = new List<byte[]>();
        while (TryReceive(out var packet))
        {
            packets.Add(packet);
        }
        return packets.ToArray();
    }
}
