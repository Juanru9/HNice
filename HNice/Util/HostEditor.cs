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
