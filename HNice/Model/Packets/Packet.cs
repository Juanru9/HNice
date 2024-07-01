
using HNice.Util.Extensions;

namespace HNice.Model.Packets;

public abstract class Packet<THeader> where THeader : struct, Enum
{
    public bool IsEncrypted { get; set; }
    private readonly string _packetRawData;
    public IEnumerable<string> PacketContent { get; set; }
    public THeader Header { get; set; }

    protected Packet(string packetRawData, bool isEncrypted = false)
    {
        _packetRawData = packetRawData;
        PacketContent = new List<string>();
        DeserializePacketData();
        IsEncrypted = isEncrypted;
    }

    public Packet(THeader header, IEnumerable<string> packetContent)
    {
        Header = header;
        PacketContent = packetContent;
    }

    private void DeserializePacketData()
    {
        if (string.IsNullOrEmpty(_packetRawData))
            throw new ArgumentNullException(nameof(_packetRawData), "Packet raw data cannot be null or empty.");

        // Extract the header, assuming header length is 2 characters
        if (_packetRawData.Length < 2)
        {
            throw new FormatException($"Invalid packet format: incorrect header: {_packetRawData}");
        }

        var headerString = _packetRawData.Substring(0, 2);
        var headerValue = headerString.Substring(1).DecodeB64();

        if (!Enum.IsDefined(typeof(THeader), headerValue))
        {
            throw new ArgumentException($"Invalid header value: {headerValue}/{headerString}");
        }
        Header = (THeader)(object)headerValue;

        // Remove the header from the original string
        string remainingData = _packetRawData.Substring(2);

        // Split the remaining data by PacketSplitter until PacketEnder is reached
        int packetEndIndex = remainingData.IndexOf(Constants.PACKET_ENDER);
        if (packetEndIndex >= 0)
        {
            remainingData = remainingData.Substring(0, packetEndIndex);
        }

        PacketContent = remainingData.Split(Constants.PACKET_SPLITTER).ToList();
    }

    public string SerializePacketData()
    {
        // Convert the header to a base64 string representation
        string headerString = Convert.ToInt32(Header).EncodeB64();

        // Concatenate header and packet content using the specified delimiters
        string packetContentString = string.Join(Constants.PACKET_SPLITTER.ToString(), PacketContent);

        // Return the final packet string, ending with the PACKET_ENDER character
        return $"{headerString}{packetContentString}{Constants.PACKET_ENDER}";
    }

    public override string ToString()
    {
        return $"{GetType().Name} {Header} -> | {string.Join(" | ", PacketContent)}";
    }
}
