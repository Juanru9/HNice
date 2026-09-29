using System.Reflection;

namespace HNice.ViewModel;

/// <summary>Static information for the About panel.</summary>
public sealed class AboutViewModel
{
    public string Version { get; } = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1");
    public string Author { get; } = "github.com/Juanru9";
}
