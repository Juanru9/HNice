
namespace HNice.Model.Packets;

public class IncomingPacket : Packet<IncomingPacketMessage>
{
    public IncomingPacket(string packetRawData) : base(packetRawData)
    {
    }

    public IncomingPacket(IncomingPacketMessage header, IEnumerable<string> packetContent) :base(header, packetContent)
    {
        Header = header;
        PacketContent = packetContent;
    }

    public override string ToString()
    {
        return $"{nameof(IncomingPacket)} (decrypted) [{Header}] -> {string.Join(" | ", PacketContent)}{Environment.NewLine}*{this.SerializePacketData()}*";
    }

}
