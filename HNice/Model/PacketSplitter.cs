using HNice.Model.Packets;
using HNice.Service;
using HNice.Util;
using Microsoft.Extensions.Logging;

namespace HNice.Model;
public class SplittedData
{
    public List<IncomingPacket> IncomingPackets { get; set; } = new List<IncomingPacket>();
    public List<OutcomingPacket> OutgoingPackets { get; set; } = new List<OutcomingPacket>();
    public SplittedData() { }
}

public interface IPacketSplitter
{
    SplittedData SplitData(string data, TrafficDirection trafficDirection);
}
public class PacketSplitter : IPacketSplitter
{
    private ILogger<PacketSplitter> _logger;

    public PacketSplitter(ILogger<PacketSplitter> logger) 
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SplittedData SplitData(string data, TrafficDirection trafficDirection)
    {
        var splittedData = new SplittedData();

        if (string.IsNullOrEmpty(data)) 
        {
            return splittedData;
        }

        if (HasMultiplePackets(data)) 
        {
            foreach (var splittedPacket in data.Split((char)1))
            {
                PacketSelector(splittedPacket, trafficDirection, ref splittedData);
            }
            return splittedData;
        }

        PacketSelector(data, trafficDirection, ref splittedData);
        return splittedData;
    }

    private void PacketSelector(string data, TrafficDirection trafficDirection, ref SplittedData splittedData) 
    {
        if (string.IsNullOrEmpty(data))
            return;

        try
        {
            if (trafficDirection == TrafficDirection.ClientToServer)
            {
                if (data.Length >= 4)
                {
                    //var packet = new OutcomingPacket(data);
                }
            }

            if (trafficDirection == TrafficDirection.ServerToClient)
            {
                var packet = new IncomingPacket(data);
                splittedData.IncomingPackets.Add(packet);
                _logger.LogInformation(packet.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Error in SplitData: " + ex.Message, ex);
        }
    }

    private bool HasMultiplePackets(string data)
    {
        return data.Count(c => c == '@') > 1;
    }
}
