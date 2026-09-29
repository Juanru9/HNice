using System.Diagnostics;
using System.IO;

namespace HNice.Util;

public static class HostEditor
{
    private static string _windowsHostFolder = "drivers/etc/hosts";
    public static void UpdateHostsFile(string localhost, string hotelAddress)
    {
        try
        {
            string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), _windowsHostFolder);
            string newLine = $"{localhost} {hotelAddress}";

            if (!File.Exists(hostsFilePath)) return;

            string[] lines = File.ReadAllLines(hostsFilePath);

            if (Array.Exists(lines, line => line.Equals(newLine))) return;

            using (var sw = File.AppendText(hostsFilePath))
            {
                sw.WriteLine(newLine);
            }
            Debug.WriteLine("Hosts file updated successfully.");

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating hosts file: {ex.Message}");
        }
    }
    /// <summary>True when an active (uncommented) hosts entry maps the hotel to the given address.</summary>
    public static bool IsRedirected(string localhost, string hotelAddress)
    {
        try
        {
            string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), _windowsHostFolder);
            if (!File.Exists(hostsFilePath)) return false;

            return File.ReadLines(hostsFilePath)
                .Select(line => line.Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Any(parts => parts.Length >= 2
                    && parts[0] == localhost
                    && parts.Skip(1).Any(host => host.Equals(hotelAddress, StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading hosts file: {ex.Message}");
            return false;
        }
    }

    public static void RestoreHostsFile(string localhost, string hotelAddress)
    {
        try
        {
            string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), _windowsHostFolder);
            string newLine = $"{localhost} {hotelAddress}";

            if (!File.Exists(hostsFilePath)) return;

            string[] lines = File.ReadAllLines(hostsFilePath);
            bool lineExists = lines.Any(line => line.Trim().Equals(newLine, StringComparison.OrdinalIgnoreCase));

            if (lineExists)
            {
                lines = lines.Where(line => !line.Trim().Equals(newLine, StringComparison.OrdinalIgnoreCase)).ToArray();
                File.WriteAllLines(hostsFilePath, lines);
                Debug.WriteLine("Line removed from hosts file.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating hosts file: {ex.Message}");
        }
    }
}
