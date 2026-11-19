using System;
using System.Collections.Generic;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace DepartmentModule.Helpers
{
    public static class TenderDocumentGenerator
    {
        public static byte[] Render(string title, string tenderNumber, string tenderTitle,
            IEnumerable<KeyValuePair<string, string>> fields)
        {
            if (String.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A document title is required.", "title");

            Document document = new Document();
            document.Info.Title = title;
            document.Info.Subject = tenderNumber ?? String.Empty;
            document.DefaultPageSetup.PageFormat = PageFormat.A4;
            document.DefaultPageSetup.LeftMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.RightMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.TopMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.BottomMargin = Unit.FromCentimeter(2);

            Section section = document.AddSection();
            Paragraph heading = section.AddParagraph(title);
            heading.Format.Font.Name = "Arial";
            heading.Format.Font.Size = 18;
            heading.Format.Font.Bold = true;
            heading.Format.SpaceAfter = Unit.FromCentimeter(0.35);

            Paragraph organization = section.AddParagraph("Solapur Municipal Corporation");
            organization.Format.Font.Name = "Arial";
            organization.Format.Font.Size = 11;
            organization.Format.Font.Bold = true;
            organization.Format.SpaceAfter = Unit.FromCentimeter(0.25);

            AddField(section, "Tender number", tenderNumber);
            AddField(section, "Tender title", tenderTitle);
            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.2);

            foreach (KeyValuePair<string, string> field in fields)
                AddField(section, field.Key, field.Value);

            Paragraph footer = section.Footers.Primary.AddParagraph(
                "Generated from the Tender Management System. Verify all details before signing.");
            footer.Format.Font.Name = "Arial";
            footer.Format.Font.Size = 8;
            footer.Format.Font.Italic = true;
            footer.Format.Alignment = ParagraphAlignment.Center;

            PdfDocumentRenderer renderer = new PdfDocumentRenderer();
            renderer.Document = document;
            renderer.RenderDocument();
            using (MemoryStream stream = new MemoryStream())
            {
                renderer.PdfDocument.Save(stream, false);
                return stream.ToArray();
            }
        }

        private static void AddField(Section section, string label, string value)
        {
            Paragraph paragraph = section.AddParagraph();
            paragraph.Format.Font.Name = "Arial";
            paragraph.Format.Font.Size = 10;
            paragraph.Format.SpaceAfter = Unit.FromPoint(8);
            paragraph.AddFormattedText((label ?? String.Empty) + ": ", TextFormat.Bold);
            paragraph.AddText(value ?? String.Empty);
        }
    }
}
