using System.ComponentModel;
using DevExpress.Utils;
using DevExpress.XtraReports.UI;

namespace ClipMate.Platform.Services.Printing;

/// <summary>
/// Visual designer-based report for printing clips.
/// This report is designed using the DevExpress Report Designer and loads from ClipTextReportVisual.repx
/// </summary>
public partial class ClipTextReportVisual : XtraReport
{
    public ClipTextReportVisual()
    {
        InitializeComponent();

        xrPictureBoxImage.CanGrow = true;
    }

    private void xrPictureBoxImage_BeforePrint(object sender, CancelEventArgs e)
    {
        if (sender is not XRPictureBox pictureBox || pictureBox.Image == null)
            return;

        // Calculate the desired height based on a target width
        var targetWidth = WidthF = PageWidth - Margins.Left - Margins.Right;
        float imageWidth = pictureBox.Image.Width;
        float imageHeight = pictureBox.Image.Height;

        // Calculate new height to maintain aspect ratio
        var targetHeight = targetWidth / imageWidth * imageHeight;

        pictureBox.WidthF = targetWidth;
        pictureBox.HeightF = targetHeight;

        pictureBox.LocationF = new PointFloat(
            pictureBox.LocationF.X,
            (pictureBox.Parent.HeightF - targetHeight) / 2);
    }

    private void ClipTextReportVisual_BeforePrint(object sender, CancelEventArgs e)
    {
        if (Parameters["SingleItemPerPage"].Value is true)
            Detail.PageBreak = PageBreak.AfterBandExceptLastEntry;
    }
}
