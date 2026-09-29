namespace HNice.ViewModel;

/// <summary>
/// A sidebar entry. The ViewModel is rendered through the DataTemplates declared in MainWindow,
/// so each tool keeps its own view and view-model.
/// </summary>
public sealed class ToolItem
{
    public string Title { get; }
    public string Group { get; }

    /// <summary>Segoe Fluent / MDL2 icon code point.</summary>
    public string Glyph { get; }

    /// <summary>One line shown under the tool title: what the tool really does.</summary>
    public string Description { get; }

    /// <summary>True when the tool only changes what your own client shows.</summary>
    public bool IsClientSideOnly { get; }

    public object ViewModel { get; }

    public ToolItem(string title, string group, string glyph, string description, bool isClientSideOnly, object viewModel)
    {
        Title = title;
        Group = group;
        Glyph = glyph;
        Description = description;
        IsClientSideOnly = isClientSideOnly;
        ViewModel = viewModel;
    }
}
