using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HNice.Util;

/// <summary>
/// Resolves a hostname's A record by querying a public DNS server directly over UDP.
/// The normal OS resolver can't be used: HNice adds "127.0.0.1 &lt;hotel&gt;" to the hosts file to hijack the
/// connection, so a system lookup of the hotel would return 127.0.0.1 (the proxy itself).
/// </summary>
public static class PublicDnsResolver
{
    // Cloudflare and Google public resolvers, queried by IP so no name lookup is needed.
    private static readonly IPEndPoint[] Resolvers =
    {
        new(IPAddress.Parse("1.1.1.1"), 53),
        new(IPAddress.Parse("8.8.8.8"), 53),
    };

    public static async Task<string?> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return literal.ToString();
        }

        var query = BuildQuery(host);

        foreach (var resolver in Resolvers)
        {
            try
            {
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Connect(resolver);
                await udp.SendAsync(query, cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

                var receive = await udp.ReceiveAsync(cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                var address = ParseFirstA(receive.Buffer);
                if (address is not null)
                {
                    return address;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Try the next resolver.
            }
        }

        return null;
    }

    private static byte[] BuildQuery(string host)
    {
        using var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[0..], (ushort)Random.Shared.Next(ushort.MaxValue)); // transaction id
        BinaryPrimitives.WriteUInt16BigEndian(header[2..], 0x0100); // standard query, recursion desired
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], 1);      // one question
        stream.Write(header);

        foreach (var label in host.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }
        stream.WriteByte(0); // end of name

        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(tail[0..], 1); // QTYPE A
        BinaryPrimitives.WriteUInt16BigEndian(tail[2..], 1); // QCLASS IN
        stream.Write(tail);

        return stream.ToArray();
    }

    private static string? ParseFirstA(byte[] response)
    {
        if (response.Length < 12) return null;

        int questions = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4));
        int answers = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6));

        var pos = 12;
        for (var i = 0; i < questions; i++)
        {
            pos = SkipName(response, pos);
            pos += 4; // QTYPE + QCLASS
        }

        for (var i = 0; i < answers && pos + 10 <= response.Length; i++)
        {
            pos = SkipName(response, pos);
            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(pos));
            var rdLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(pos + 8));
            pos += 10;

            if (type == 1 && rdLength == 4 && pos + 4 <= response.Length) // A record
            {
                return new IPAddress(response.AsSpan(pos, 4).ToArray()).ToString();
            }
            pos += rdLength;
        }

        return null;
    }

    // Names use length-prefixed labels and may end in a compression pointer (top two bits set).
    private static int SkipName(byte[] data, int pos)
    {
        while (pos < data.Length)
        {
            var length = data[pos];
            if (length == 0)
            {
                return pos + 1;
            }
            if ((length & 0xC0) == 0xC0)
            {
                return pos + 2; // compression pointer, name ends here
            }
            pos += length + 1;
        }
        return pos;
    }
}
