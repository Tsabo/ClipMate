namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Configuration settings for printing.
/// </summary>
public sealed class PrintConfiguration
{
    // Printer Selection
    /// <summary>
    /// Gets or sets the selected printer name. Null or empty string means use default printer.
    /// </summary>
    public string? SelectedPrinter { get; set; }

    // Header/Footer Options
    /// <summary>
    /// Gets or sets a value indicating whether to print the report header.
    /// </summary>
    public bool PrintHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom header text (e.g., "ClipMate Report (Text)").
    /// </summary>
    public string HeaderText { get; set; } = "ClipMate Report";

    /// <summary>
    /// Gets or sets a value indicating whether to print the page footer.
    /// </summary>
    public bool PrintFooter { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include clip metadata in prints.
    /// </summary>
    public bool PrintDetails { get; set; } = true;

    // Print Behavior
    /// <summary>
    /// Gets or sets a value indicating whether QuickPrint is enabled (bypasses preview dialog).
    /// </summary>
    public bool QuickPrintEnabled { get; set; }

    // Layout Options
    /// <summary>
    /// Gets or sets a value indicating whether to print one item per page (true) or multiple items per page (false).
    /// </summary>
    public bool SingleItemPerPage { get; set; } = false;

    // Margins
    /// <summary>
    /// Gets or sets the margin unit (Inches or Millimeters).
    /// </summary>
    public MarginUnit MarginUnit { get; set; } = MarginUnit.Inches;

    /// <summary>
    /// Gets or sets the left margin.
    /// </summary>
    public double LeftMargin { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the right margin.
    /// </summary>
    public double RightMargin { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the top margin.
    /// </summary>
    public double TopMargin { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the bottom margin.
    /// </summary>
    public double BottomMargin { get; set; } = 1.0;
}

/// <summary>
/// Margin unit enumeration.
/// </summary>
public enum MarginUnit
{
    /// <summary>
    /// Margins in inches.
    /// </summary>
    Inches,

    /// <summary>
    /// Margins in millimeters.
    /// </summary>
    Millimeters,
}
