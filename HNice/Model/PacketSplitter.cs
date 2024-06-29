using HNice.Service;
using HNice.Util;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HNice.Model;
public class SplittedData
{
    public string Status { get; set; }
    public string DataToSend { get; set; }
    public string PublicKey { get; set; }
    public Coordinate Coordinates { get; set; }
    public SplittedData()
    {

    }
    public SplittedData(string status, string dataToSend, string publicKey)
    {
        Status = status;
        DataToSend = dataToSend;
        PublicKey = publicKey;
    }

    public bool HasDataToSend() => !string.IsNullOrEmpty(DataToSend) && !string.IsNullOrEmpty(Status);
}

public interface IPacketSplitter
{
    SplittedData SplitData(string data, TrafficDirection trafficDirection, ref bool packetSplitted);
}
public class PacketSplitter : IPacketSplitter
{
    private string tmpData = string.Empty;
    private ILogger<PacketSplitter> _logger;

    public PacketSplitter(ILogger<PacketSplitter> logger) 
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SplittedData SplitData(string data, TrafficDirection trafficDirection, ref bool packetSplitted)
    {
        var splittedData = new SplittedData();

        try
        {
            if (trafficDirection == TrafficDirection.ClientToServer)
            {
                string packetHeader2 = string.Empty;
                if (data.Length >= 4)
                {
                    packetHeader2 = data.Substring(1, 2);
                }
            }

            if (trafficDirection == TrafficDirection.ServerToClient)
            {
                string packetHeader = string.Empty;
                if (data.Length >= 2)
                {
                    packetHeader = data.Substring(0, 2);
                }

                if (packetSplitted)
                {
                    data = tmpData + data;
                    packetSplitted = false;
                }

                if (data.Length >= 1400)
                {
                    if (packetHeader == "@_")
                    {
                        tmpData = data;
                        packetSplitted = true;
                        return splittedData;
                    }
                }
                else
                {
                    packetSplitted = false;
                }

                if (packetHeader == "@E")
                {
                    string[] lines = data.Split(new[] { '\r' }, StringSplitOptions.None);
                    string habboName = string.Empty;
                    string habboFigure = string.Empty;
                    string habboSex = string.Empty;
                    string habboMission = string.Empty;
                    string regNumber = string.Empty;

                    foreach (var line in lines)
                    {
                        if (line.Contains("name="))
                        {
                            habboName = line.Substring(line.IndexOf('=') + 1);
                            _logger.LogInformation("Habbo name packet: " + habboName);
                        }
                        else if (line.StartsWith("figure="))
                        {
                            habboFigure = line.Substring(line.IndexOf('=') + 1);
                        }
                        else if (line.StartsWith("sex="))
                        {
                            habboSex = line.Substring(line.IndexOf('=') + 1);
                        }
                        else if (line.StartsWith("customData="))
                        {
                            habboMission = line.Substring(line.IndexOf('=') + 1);
                        }
                    }

                    regNumber = data.Substring(2, data.IndexOf('\r', 3) - 3);

                    splittedData.Status = "Status: Habbologin was successfully. Welcome " + habboName;
                    splittedData.DataToSend = "BK" + "Welcome to Habbo Nice [" + habboName + "] by Samus" + (char)1;
                }

                if (packetHeader == "@A")
                {
                    string[] j = data.Split(new[] { '\u0001' }, StringSplitOptions.None);
                    splittedData.PublicKey = data.Substring(2, data.Length - 2).Split(new[] { '\u0001' }, StringSplitOptions.None)[0];
                }

                // Walk packet
                if (packetHeader == "@b")
                {
                    var coordinates = PacketExtractor.ExtractCoordinates(data);
                    if (coordinates.AreValidCoords())
                    {
                        splittedData.Coordinates = coordinates;
                        _logger.LogInformation($"Walking to ({coordinates.X},{coordinates.Y}) coords.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Error in SplitData: " + ex.Message, ex);
        }
        return splittedData;
    }
}
