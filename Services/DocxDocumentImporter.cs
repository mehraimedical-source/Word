using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace MedicalWord.Web.Services
{
    public sealed class DocxDocumentImporter : IDocumentImporter
    {
        public bool CanImport(string extension)
        {
            return string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase);
        }

        public string ImportToHtml(Stream input)
        {
            using (var document = WordprocessingDocument.Open(input, false))
            {
                var main = document.MainDocumentPart;
                if (main?.Document?.Body == null)
                    throw new InvalidDataException("The DOCX document does not contain a readable body.");

                var sb = new StringBuilder();
                sb.Append("<div class=\"imported-docx\">");

                foreach (var child in main.Document.Body.ChildElements)
                    AppendBlock(sb, child, main);

                sb.Append("</div>");
                return sb.ToString();
            }
        }

        private static void AppendBlock(StringBuilder sb, OpenXmlElement element, MainDocumentPart main)
        {
            var paragraph = element as Paragraph;
            if (paragraph != null)
            {
                AppendParagraph(sb, paragraph, main);
                return;
            }

            var table = element as Table;
            if (table != null)
            {
                AppendTable(sb, table, main);
            }
        }

        private static void AppendParagraph(StringBuilder sb, Paragraph p, MainDocumentPart main)
        {
            var pPr = p.ParagraphProperties;
            var styles = new StringBuilder();

            if (pPr?.Justification?.Val != null)
            {
                var value = pPr.Justification.Val.Value;
                if (value == JustificationValues.Center) styles.Append("text-align:center;");
                else if (value == JustificationValues.Right) styles.Append("text-align:right;");
                else if (value == JustificationValues.Both) styles.Append("text-align:justify;");
                else if (value == JustificationValues.Left) styles.Append("text-align:left;");
            }

            var direction = pPr?.BiDi != null ? "rtl" : "auto";
            sb.Append("<p dir=\"").Append(direction).Append("\" style=\"")
              .Append(styles).Append("\">");

            foreach (var node in p.ChildElements)
            {
                if (node is Run run) AppendRun(sb, run, main);
                else if (node is Hyperlink hyperlink)
                {
                    foreach (var r in hyperlink.Elements<Run>())
                        AppendRun(sb, r, main);
                }
            }

            if (!p.Descendants<Text>().Any() && !p.Descendants<Drawing>().Any())
                sb.Append("<br>");

            sb.Append("</p>");
        }

        private static void AppendRun(StringBuilder sb, Run run, MainDocumentPart main)
        {
            var style = GetRunStyle(run.RunProperties);
            sb.Append("<span");
            if (!string.IsNullOrWhiteSpace(style))
                sb.Append(" style=\"").Append(style).Append("\"");
            sb.Append(">");

            foreach (var child in run.ChildElements)
            {
                if (child is Text text)
                    sb.Append(HttpUtility.HtmlEncode(text.Text));
                else if (child is TabChar)
                    sb.Append("&emsp;");
                else if (child is Break)
                    sb.Append("<br>");
                else if (child is Drawing drawing)
                    AppendDrawing(sb, drawing, main);
            }

            sb.Append("</span>");
        }

        private static string GetRunStyle(RunProperties p)
        {
            if (p == null) return string.Empty;
            var sb = new StringBuilder();

            if (p.Bold != null && p.Bold.Val?.Value != false) sb.Append("font-weight:700;");
            if (p.Italic != null && p.Italic.Val?.Value != false) sb.Append("font-style:italic;");
            if (p.Underline?.Val != null && p.Underline.Val.Value != UnderlineValues.None) sb.Append("text-decoration:underline;");

            var color = p.Color?.Val?.Value;
            if (!string.IsNullOrWhiteSpace(color) && !string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase))
                sb.Append("color:#").Append(color).Append(";");

            var shade = p.Shading?.Fill?.Value;
            if (!string.IsNullOrWhiteSpace(shade) && !string.Equals(shade, "auto", StringComparison.OrdinalIgnoreCase))
                sb.Append("background-color:#").Append(shade).Append(";");

            var halfPoints = p.FontSize?.Val?.Value;
            if (double.TryParse(halfPoints, NumberStyles.Any, CultureInfo.InvariantCulture, out var hp))
                sb.Append("font-size:").Append((hp / 2d).ToString("0.##", CultureInfo.InvariantCulture)).Append("pt;");

            var font = p.RunFonts?.Ascii?.Value ?? p.RunFonts?.HighAnsi?.Value ?? p.RunFonts?.ComplexScript?.Value;
            if (!string.IsNullOrWhiteSpace(font))
                sb.Append("font-family:'").Append(HttpUtility.HtmlAttributeEncode(font)).Append("';");

            return sb.ToString();
        }

        private static void AppendDrawing(StringBuilder sb, Drawing drawing, MainDocumentPart main)
        {
            var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
            var relId = blip?.Embed?.Value;
            if (string.IsNullOrWhiteSpace(relId)) return;

            ImagePart imagePart;
            try { imagePart = (ImagePart)main.GetPartById(relId); }
            catch { return; }

            using (var imageStream = imagePart.GetStream())
            using (var ms = new MemoryStream())
            {
                imageStream.CopyTo(ms);
                var data = Convert.ToBase64String(ms.ToArray());

                var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
                var sizeStyle = string.Empty;
                if (extent != null)
                {
                    const double emusPerPixel = 9525d;
                    var width = Math.Round(extent.Cx.Value / emusPerPixel);
                    var height = Math.Round(extent.Cy.Value / emusPerPixel);
                    sizeStyle = "width:" + width + "px;height:" + height + "px;max-width:100%;";
                }

                sb.Append("<img alt=\"\" style=\"").Append(sizeStyle)
                  .Append("\" src=\"data:").Append(imagePart.ContentType)
                  .Append(";base64,").Append(data).Append("\" />");
            }
        }

        private static void AppendTable(StringBuilder sb, Table table, MainDocumentPart main)
        {
            sb.Append("<table class=\"word-table\"><tbody>");
            foreach (var row in table.Elements<TableRow>())
            {
                sb.Append("<tr>");
                foreach (var cell in row.Elements<TableCell>())
                {
                    var span = cell.TableCellProperties?.GridSpan?.Val?.Value;
                    sb.Append("<td");
                    if (span.HasValue && span.Value > 1)
                        sb.Append(" colspan=\"").Append(span.Value).Append("\"");
                    sb.Append(">");

                    foreach (var child in cell.ChildElements)
                        AppendBlock(sb, child, main);

                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }
    }
}