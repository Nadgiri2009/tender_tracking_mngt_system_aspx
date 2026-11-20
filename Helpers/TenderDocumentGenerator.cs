using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
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

        public static byte[] RenderBoq(string tenderNumber, string tenderTitle, DataTable items)
        {
            if (items == null || items.Rows.Count == 0)
                throw new ArgumentException("At least one BOQ item is required.", "items");

            Document document = new Document();
            document.Info.Title = "Bill of Quantities";
            document.Info.Subject = tenderNumber ?? String.Empty;
            document.DefaultPageSetup.PageFormat = PageFormat.A4;
            document.DefaultPageSetup.LeftMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.RightMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.TopMargin = Unit.FromCentimeter(2);
            document.DefaultPageSetup.BottomMargin = Unit.FromCentimeter(2);

            Section section = document.AddSection();
            Paragraph heading = section.AddParagraph("Bill of Quantities");
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

            Table table = section.AddTable();
            table.Borders.Width = Unit.FromPoint(0.5);
            table.Format.Font.Name = "Arial";
            table.Format.Font.Size = 9;
            table.AddColumn(Unit.FromCentimeter(1.1));
            table.AddColumn(Unit.FromCentimeter(5.1));
            table.AddColumn(Unit.FromCentimeter(1.8));
            table.AddColumn(Unit.FromCentimeter(2.0));
            table.AddColumn(Unit.FromCentimeter(3.0));
            table.AddColumn(Unit.FromCentimeter(3.0));

            Row header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Shading.Color = Colors.LightGray;
            string[] headings = { "No.", "Item / description", "Unit", "Quantity", "Rate", "Amount" };
            for (int i = 0; i < headings.Length; i++)
                header.Cells[i].AddParagraph(headings[i]);

            CultureInfo numberCulture = CultureInfo.GetCultureInfo("en-IN");
            decimal total = 0m;
            for (int i = 0; i < items.Rows.Count; i++)
            {
                DataRow item = items.Rows[i];
                Row row = table.AddRow();
                row.Cells[0].AddParagraph((i + 1).ToString(CultureInfo.InvariantCulture));
                row.Cells[1].AddParagraph(Convert.ToString(item["ItemName"]));
                string description = item["Description"] == DBNull.Value
                    ? String.Empty
                    : Convert.ToString(item["Description"]);
                if (!String.IsNullOrWhiteSpace(description))
                {
                    Paragraph detail = row.Cells[1].AddParagraph(description);
                    detail.Format.Font.Size = 8;
                }
                row.Cells[2].AddParagraph(Convert.ToString(item["Unit"]));
                row.Cells[3].AddParagraph(Convert.ToDecimal(item["Quantity"], CultureInfo.InvariantCulture)
                    .ToString("N3", numberCulture));
                row.Cells[4].AddParagraph(Convert.ToDecimal(item["Rate"], CultureInfo.InvariantCulture)
                    .ToString("N2", numberCulture));
                decimal amount = Convert.ToDecimal(item["Amount"], CultureInfo.InvariantCulture);
                total += amount;
                row.Cells[5].AddParagraph(amount.ToString("N2", numberCulture));
            }

            Row totalRow = table.AddRow();
            totalRow.Cells[0].MergeRight = 4;
            totalRow.Cells[0].AddParagraph("Total");
            totalRow.Cells[0].Format.Font.Bold = true;
            totalRow.Cells[5].AddParagraph(total.ToString("N2", numberCulture));
            totalRow.Cells[5].Format.Font.Bold = true;

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
