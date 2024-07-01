namespace HNice.Model.Packets;

public class OutcomingPacket : Packet<OutcomingPacketMessage>
{
    public OutcomingPacket(string packetRawData) : base(packetRawData)
    {
    }
    public OutcomingPacket(OutcomingPacketMessage header, IEnumerable<string> packetContent) : base(header, packetContent)
    {
        Header = header;
        PacketContent = packetContent;
    }
}
