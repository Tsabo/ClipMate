using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.Export.Html;

namespace ClipMate.App.Services;

/// <summary>
/// Converts RTF to HTML using the DevExpress non-visual rich edit document server.
/// </summary>
public class DevExpressRtfToHtmlConverter : IRtfToHtmlConverter
{
    /// <inheritdoc />
    public string ConvertToHtml(string rtf)
    {
        ArgumentNullException.ThrowIfNull(rtf);

        if (string.IsNullOrWhiteSpace(rtf))
            return string.Empty;

        using var server = new RichEditDocumentServer();
        server.RtfText = rtf;

        var htmlOptions = server.Options.Export.Html;
        // Inline styles let the Markdown converter turn bold/italic spans into tags
        htmlOptions.CssPropertiesExportType = CssPropertiesExportType.Inline;
        htmlOptions.ExportRootTag = ExportRootTag.Body;
        htmlOptions.EmbedImages = true;

        return server.HtmlText;
    }
}
