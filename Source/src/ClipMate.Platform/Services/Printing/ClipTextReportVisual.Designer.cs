namespace ClipMate.Platform.Services.Printing
{
    partial class ClipTextReportVisual
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.xrPictureBoxImage = new DevExpress.XtraReports.UI.XRPictureBox();
            this.xrLabelText = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLineSeparator = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelTitle = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelTitleValue = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelCreator = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelCreatorValue = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelUrl = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelUrlValue = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelDate = new DevExpress.XtraReports.UI.XRLabel();
            this.xrLabelDateValue = new DevExpress.XtraReports.UI.XRLabel();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.xrLabelReportTitle = new DevExpress.XtraReports.UI.XRLabel();
            this.GroupHeader1 = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.PageFooter = new DevExpress.XtraReports.UI.PageFooterBand();
            this.xrPageInfoFooter = new DevExpress.XtraReports.UI.XRPageInfo();
            this.topMarginBand1 = new DevExpress.XtraReports.UI.TopMarginBand();
            this.bottomMarginBand1 = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.objectDataSource1 = new DevExpress.DataAccess.ObjectBinding.ObjectDataSource(this.components);
            this.PrintHeader = new DevExpress.XtraReports.Parameters.Parameter();
            this.HeaderText = new DevExpress.XtraReports.Parameters.Parameter();
            this.PrintFooter = new DevExpress.XtraReports.Parameters.Parameter();
            this.PrintDetails = new DevExpress.XtraReports.Parameters.Parameter();
            this.SingleItemPerPage = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.objectDataSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrPictureBoxImage,
            this.xrLabelText});
            this.Detail.HeightF = 2F;
            this.Detail.Name = "Detail";
            this.Detail.Padding = new DevExpress.XtraPrinting.PaddingInfo(0F, 0F, 0F, 21F, 100F);
            // 
            // xrPictureBoxImage
            // 
            this.xrPictureBoxImage.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrPictureBoxImage.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "[IsImage]"),
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "ImageSource", "[ImageData]")});
            this.xrPictureBoxImage.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.xrPictureBoxImage.Name = "xrPictureBoxImage";
            this.xrPictureBoxImage.SizeF = new System.Drawing.SizeF(641.6667F, 2F);
            this.xrPictureBoxImage.Sizing = DevExpress.XtraPrinting.ImageSizeMode.ZoomImage;
            this.xrPictureBoxImage.UseImageMetadata = true;
            this.xrPictureBoxImage.BeforePrint += new DevExpress.XtraReports.UI.BeforePrintEventHandler(this.xrPictureBoxImage_BeforePrint);
            // 
            // xrLabelText
            // 
            this.xrLabelText.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrLabelText.CanShrink = true;
            this.xrLabelText.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "Not [IsImage]"),
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Content]")});
            this.xrLabelText.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F);
            this.xrLabelText.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.xrLabelText.Multiline = true;
            this.xrLabelText.Name = "xrLabelText";
            this.xrLabelText.Padding = new DevExpress.XtraPrinting.PaddingInfo(0F, 10F, 0F, 10F, 100F);
            this.xrLabelText.SizeF = new System.Drawing.SizeF(641.67F, 2F);
            this.xrLabelText.StylePriority.UseFont = false;
            this.xrLabelText.StylePriority.UsePadding = false;
            // 
            // xrLineSeparator
            // 
            this.xrLineSeparator.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrLineSeparator.BorderColor = System.Drawing.Color.LightGray;
            this.xrLineSeparator.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.xrLineSeparator.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.xrLineSeparator.Name = "xrLineSeparator";
            this.xrLineSeparator.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLineSeparator.SizeF = new System.Drawing.SizeF(641.6667F, 2F);
            this.xrLineSeparator.StylePriority.UseBorderColor = false;
            this.xrLineSeparator.StylePriority.UseBorders = false;
            // 
            // xrLabelTitle
            // 
            this.xrLabelTitle.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.xrLabelTitle.LocationFloat = new DevExpress.Utils.PointFloat(0F, 10.00001F);
            this.xrLabelTitle.Name = "xrLabelTitle";
            this.xrLabelTitle.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelTitle.SizeF = new System.Drawing.SizeF(36.19792F, 18F);
            this.xrLabelTitle.StylePriority.UseFont = false;
            this.xrLabelTitle.StylePriority.UseTextAlignment = false;
            this.xrLabelTitle.Text = "Title:";
            this.xrLabelTitle.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // xrLabelTitleValue
            // 
            this.xrLabelTitleValue.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrLabelTitleValue.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Title]")});
            this.xrLabelTitleValue.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F);
            this.xrLabelTitleValue.LocationFloat = new DevExpress.Utils.PointFloat(39.99998F, 10.00001F);
            this.xrLabelTitleValue.Name = "xrLabelTitleValue";
            this.xrLabelTitleValue.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelTitleValue.SizeF = new System.Drawing.SizeF(371.9394F, 18F);
            this.xrLabelTitleValue.StylePriority.UseFont = false;
            this.xrLabelTitleValue.StylePriority.UseTextAlignment = false;
            this.xrLabelTitleValue.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // xrLabelCreator
            // 
            this.xrLabelCreator.AnchorHorizontal = DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right;
            this.xrLabelCreator.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.xrLabelCreator.LocationFloat = new DevExpress.Utils.PointFloat(411.9394F, 10.00001F);
            this.xrLabelCreator.Name = "xrLabelCreator";
            this.xrLabelCreator.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelCreator.SizeF = new System.Drawing.SizeF(60F, 18F);
            this.xrLabelCreator.StylePriority.UseFont = false;
            this.xrLabelCreator.StylePriority.UseTextAlignment = false;
            this.xrLabelCreator.Text = "Creator:";
            this.xrLabelCreator.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // xrLabelCreatorValue
            // 
            this.xrLabelCreatorValue.AnchorHorizontal = DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right;
            this.xrLabelCreatorValue.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Creator]")});
            this.xrLabelCreatorValue.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F);
            this.xrLabelCreatorValue.LocationFloat = new DevExpress.Utils.PointFloat(473.9394F, 10.00001F);
            this.xrLabelCreatorValue.Name = "xrLabelCreatorValue";
            this.xrLabelCreatorValue.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelCreatorValue.SizeF = new System.Drawing.SizeF(167.7273F, 18F);
            this.xrLabelCreatorValue.StylePriority.UseFont = false;
            this.xrLabelCreatorValue.StylePriority.UseTextAlignment = false;
            this.xrLabelCreatorValue.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // xrLabelUrl
            // 
            this.xrLabelUrl.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.xrLabelUrl.LocationFloat = new DevExpress.Utils.PointFloat(0F, 30.00002F);
            this.xrLabelUrl.Name = "xrLabelUrl";
            this.xrLabelUrl.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelUrl.SizeF = new System.Drawing.SizeF(36.19792F, 18F);
            this.xrLabelUrl.StylePriority.UseFont = false;
            this.xrLabelUrl.StylePriority.UseTextAlignment = false;
            this.xrLabelUrl.Text = "URL:";
            this.xrLabelUrl.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // xrLabelUrlValue
            // 
            this.xrLabelUrlValue.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrLabelUrlValue.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Url]")});
            this.xrLabelUrlValue.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F);
            this.xrLabelUrlValue.LocationFloat = new DevExpress.Utils.PointFloat(39.99998F, 30.00001F);
            this.xrLabelUrlValue.Name = "xrLabelUrlValue";
            this.xrLabelUrlValue.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelUrlValue.SizeF = new System.Drawing.SizeF(371.9394F, 18F);
            this.xrLabelUrlValue.StylePriority.UseFont = false;
            this.xrLabelUrlValue.StylePriority.UseTextAlignment = false;
            this.xrLabelUrlValue.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // xrLabelDate
            // 
            this.xrLabelDate.AnchorHorizontal = DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right;
            this.xrLabelDate.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.xrLabelDate.LocationFloat = new DevExpress.Utils.PointFloat(411.9394F, 30F);
            this.xrLabelDate.Name = "xrLabelDate";
            this.xrLabelDate.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelDate.SizeF = new System.Drawing.SizeF(60F, 18F);
            this.xrLabelDate.StylePriority.UseFont = false;
            this.xrLabelDate.StylePriority.UseTextAlignment = false;
            this.xrLabelDate.Text = "Date:";
            this.xrLabelDate.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // xrLabelDateValue
            // 
            this.xrLabelDateValue.AnchorHorizontal = DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right;
            this.xrLabelDateValue.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[Created]")});
            this.xrLabelDateValue.Font = new DevExpress.Drawing.DXFont("Segoe UI", 9F);
            this.xrLabelDateValue.LocationFloat = new DevExpress.Utils.PointFloat(473.9394F, 30.00002F);
            this.xrLabelDateValue.Name = "xrLabelDateValue";
            this.xrLabelDateValue.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelDateValue.SizeF = new System.Drawing.SizeF(167.7273F, 18F);
            this.xrLabelDateValue.StylePriority.UseFont = false;
            this.xrLabelDateValue.StylePriority.UseTextAlignment = false;
            this.xrLabelDateValue.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.xrLabelDateValue.TextFormatString = "{0:MM/dd/yyyy hh:mm tt}";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrLabelReportTitle});
            this.ReportHeader.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "?PrintHeader")});
            this.ReportHeader.HeightF = 40F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // xrLabelReportTitle
            // 
            this.xrLabelReportTitle.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrLabelReportTitle.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "?PrintHeader")});
            this.xrLabelReportTitle.Font = new DevExpress.Drawing.DXFont("Segoe UI", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.xrLabelReportTitle.LocationFloat = new DevExpress.Utils.PointFloat(0F, 10F);
            this.xrLabelReportTitle.Name = "xrLabelReportTitle";
            this.xrLabelReportTitle.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrLabelReportTitle.SizeF = new System.Drawing.SizeF(641.6667F, 25F);
            this.xrLabelReportTitle.StylePriority.UseFont = false;
            this.xrLabelReportTitle.StylePriority.UseTextAlignment = false;
            this.xrLabelReportTitle.Text = "ClipMate Report";
            this.xrLabelReportTitle.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // GroupHeader1
            // 
            this.GroupHeader1.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrLineSeparator,
            this.xrLabelTitle,
            this.xrLabelTitleValue,
            this.xrLabelUrl,
            this.xrLabelUrlValue,
            this.xrLabelCreator,
            this.xrLabelCreatorValue,
            this.xrLabelDate,
            this.xrLabelDateValue});
            this.GroupHeader1.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "?PrintDetails")});
            this.GroupHeader1.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("ClipId", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeader1.GroupUnion = DevExpress.XtraReports.UI.GroupUnion.WithFirstDetail;
            this.GroupHeader1.HeightF = 60F;
            this.GroupHeader1.KeepTogether = true;
            this.GroupHeader1.Name = "GroupHeader1";
            // 
            // PageFooter
            // 
            this.PageFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.xrPageInfoFooter});
            this.PageFooter.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Visible", "?PrintFooter")});
            this.PageFooter.HeightF = 30F;
            this.PageFooter.Name = "PageFooter";
            // 
            // xrPageInfoFooter
            // 
            this.xrPageInfoFooter.AnchorHorizontal = ((DevExpress.XtraReports.UI.HorizontalAnchorStyles)((DevExpress.XtraReports.UI.HorizontalAnchorStyles.Left | DevExpress.XtraReports.UI.HorizontalAnchorStyles.Right)));
            this.xrPageInfoFooter.Font = new DevExpress.Drawing.DXFont("Segoe UI", 8F);
            this.xrPageInfoFooter.LocationFloat = new DevExpress.Utils.PointFloat(0F, 5F);
            this.xrPageInfoFooter.Name = "xrPageInfoFooter";
            this.xrPageInfoFooter.Padding = new DevExpress.XtraPrinting.PaddingInfo(2F, 2F, 0F, 0F, 100F);
            this.xrPageInfoFooter.SizeF = new System.Drawing.SizeF(641.6667F, 20F);
            this.xrPageInfoFooter.StylePriority.UseFont = false;
            this.xrPageInfoFooter.TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopCenter;
            this.xrPageInfoFooter.TextFormatString = "Page {0} of {1}";
            // 
            // topMarginBand1
            // 
            this.topMarginBand1.HeightF = 104.1667F;
            this.topMarginBand1.Name = "topMarginBand1";
            // 
            // bottomMarginBand1
            // 
            this.bottomMarginBand1.HeightF = 104.1667F;
            this.bottomMarginBand1.Name = "bottomMarginBand1";
            // 
            // objectDataSource1
            // 
            this.objectDataSource1.DataSourceType = null;
            this.objectDataSource1.Name = "objectDataSource1";
            // 
            // PrintHeader
            // 
            this.PrintHeader.Description = "Print Header";
            this.PrintHeader.Name = "PrintHeader";
            this.PrintHeader.Type = typeof(bool);
            this.PrintHeader.ValueInfo = "False";
            this.PrintHeader.Visible = false;
            // 
            // HeaderText
            // 
            this.HeaderText.Description = "Header Text";
            this.HeaderText.Name = "HeaderText";
            this.HeaderText.ValueInfo = "ClipMate Report";
            this.HeaderText.Visible = false;
            // 
            // PrintFooter
            // 
            this.PrintFooter.Description = "Print Footer";
            this.PrintFooter.Name = "PrintFooter";
            this.PrintFooter.Type = typeof(bool);
            this.PrintFooter.ValueInfo = "False";
            this.PrintFooter.Visible = false;
            // 
            // PrintDetails
            // 
            this.PrintDetails.Description = "Print Details";
            this.PrintDetails.Name = "PrintDetails";
            this.PrintDetails.Type = typeof(bool);
            this.PrintDetails.ValueInfo = "False";
            this.PrintDetails.Visible = false;
            // 
            // SingleItemPerPage
            // 
            this.SingleItemPerPage.Description = "Single Item Per Page";
            this.SingleItemPerPage.Name = "SingleItemPerPage";
            this.SingleItemPerPage.Type = typeof(bool);
            this.SingleItemPerPage.ValueInfo = "False";
            this.SingleItemPerPage.Visible = false;
            // 
            // ClipTextReportVisual
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.Detail,
            this.ReportHeader,
            this.GroupHeader1,
            this.PageFooter,
            this.topMarginBand1,
            this.bottomMarginBand1});
            this.ComponentStorage.AddRange(new System.ComponentModel.IComponent[] {
            this.objectDataSource1});
            this.DataSource = this.objectDataSource1;
            this.Margins = new DevExpress.Drawing.DXMargins(104.1667F, 104.1667F, 104.1667F, 104.1667F);
            this.ParameterPanelLayoutItems.AddRange(new DevExpress.XtraReports.Parameters.ParameterPanelLayoutItem[] {
            new DevExpress.XtraReports.Parameters.ParameterLayoutItem(this.PrintHeader, DevExpress.XtraReports.Parameters.Orientation.Horizontal),
            new DevExpress.XtraReports.Parameters.ParameterLayoutItem(this.HeaderText, DevExpress.XtraReports.Parameters.Orientation.Horizontal),
            new DevExpress.XtraReports.Parameters.ParameterLayoutItem(this.PrintFooter, DevExpress.XtraReports.Parameters.Orientation.Horizontal),
            new DevExpress.XtraReports.Parameters.ParameterLayoutItem(this.PrintDetails, DevExpress.XtraReports.Parameters.Orientation.Horizontal),
            new DevExpress.XtraReports.Parameters.ParameterLayoutItem(this.SingleItemPerPage, DevExpress.XtraReports.Parameters.Orientation.Horizontal)});
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.PrintHeader,
            this.HeaderText,
            this.PrintFooter,
            this.PrintDetails,
            this.SingleItemPerPage});
            this.Version = "25.2";
            this.BeforePrint += new DevExpress.XtraReports.UI.BeforePrintEventHandler(this.ClipTextReportVisual_BeforePrint);
            ((System.ComponentModel.ISupportInitialize)(this.objectDataSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }

        #endregion

        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.ReportHeaderBand ReportHeader;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeader1;
        private DevExpress.XtraReports.UI.PageFooterBand PageFooter;
        private DevExpress.XtraReports.UI.XRLabel xrLabelReportTitle;
        private DevExpress.XtraReports.UI.XRLabel xrLabelTitle;
        private DevExpress.XtraReports.UI.XRLabel xrLabelTitleValue;
        private DevExpress.XtraReports.UI.XRLabel xrLabelUrl;
        private DevExpress.XtraReports.UI.XRLabel xrLabelUrlValue;
        private DevExpress.XtraReports.UI.XRLabel xrLabelCreator;
        private DevExpress.XtraReports.UI.XRLabel xrLabelCreatorValue;
        private DevExpress.XtraReports.UI.XRLabel xrLabelDate;
        private DevExpress.XtraReports.UI.XRLabel xrLabelDateValue;
        private DevExpress.XtraReports.UI.XRLabel xrLineSeparator;
        private DevExpress.XtraReports.UI.XRPageInfo xrPageInfoFooter;
        private DevExpress.XtraReports.UI.TopMarginBand topMarginBand1;
        private DevExpress.XtraReports.UI.BottomMarginBand bottomMarginBand1;
        private DevExpress.DataAccess.ObjectBinding.ObjectDataSource objectDataSource1;
        private DevExpress.XtraReports.Parameters.Parameter PrintHeader;
        private DevExpress.XtraReports.Parameters.Parameter HeaderText;
        private DevExpress.XtraReports.Parameters.Parameter PrintFooter;
        private DevExpress.XtraReports.Parameters.Parameter PrintDetails;
        private DevExpress.XtraReports.Parameters.Parameter SingleItemPerPage;
        private DevExpress.XtraReports.UI.XRPictureBox xrPictureBoxImage;
        private DevExpress.XtraReports.UI.XRLabel xrLabelText;
    }
}
