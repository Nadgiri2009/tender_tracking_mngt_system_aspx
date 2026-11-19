using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace DepartmentModule.Helpers
{
    public static class TenderLifecycleFiles
    {
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
        public static bool IsAllowedTenderDocumentType(string documentType)
        {
            if (String.IsNullOrWhiteSpace(documentType)) return false;
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.DocumentTypes WHERE TypeName=@TypeName AND IsActive=1;", connection))
            {
                ModuleDb.AddParameter(command, "@TypeName", SqlDbType.NVarChar, documentType.Trim(), 80);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        public static void ConvertDatesToIndia(DataTable data, params string[] columns)
        {
            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            foreach (string column in columns)
            {
                if (!data.Columns.Contains(column)) continue;
                foreach (DataRow row in data.Rows)
                {
                    if (row[column] != DBNull.Value)
                    {
                        row[column] = TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.SpecifyKind(Convert.ToDateTime(row[column], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                            indiaTimeZone);
                    }
                }
            }
        }

        public static DateTime ParseIndiaDateTime(string date, string time, string label)
        {
            DateTime local;
            if (!DateTime.TryParseExact(
                    (date ?? String.Empty).Trim() + " " + (time ?? String.Empty).Trim(),
                    "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                throw new InvalidOperationException("Enter a valid " + label + " date and time.");
            }

            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), indiaTimeZone);
        }

        public static string FormatIndiaDateTime(object utcValue)
        {
            if (utcValue == null || utcValue == DBNull.Value) return String.Empty;
            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime utc = DateTime.SpecifyKind(Convert.ToDateTime(utcValue, CultureInfo.InvariantCulture), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, indiaTimeZone).ToString("dd MMM yyyy HH:mm", CultureInfo.CurrentCulture);
        }

        public static string SaveUpload(Page page, FileUpload upload, int tenderId, string uploadKind)
        {
            return SaveUpload(page, upload, tenderId, uploadKind, "tender-lifecycle");
        }

        public static string SaveTenderBidUpload(Page page, FileUpload upload, int tenderId)
        {
            return SaveUpload(page, upload, tenderId, "bid", "tender-bids");
        }

        public static string SaveTenderApplicationUpload(Page page, HttpPostedFile file, int tenderId,
            int contractorId, int maxFileSizeBytes, string allowedExtensions)
        {
            if (page == null || file == null || file.ContentLength <= 0 ||
                maxFileSizeBytes < 1 || maxFileSizeBytes > 10 * 1024 * 1024 ||
                file.ContentLength > maxFileSizeBytes)
            {
                throw new InvalidOperationException("Choose a non-empty document within the requirement's file-size limit.");
            }

            string fileName = SafeFileName(file.FileName);
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            HashSet<string> allowed = ParseAllowedExtensions(allowedExtensions);
            if (fileName.Length == 0 || fileName.Length > 260 || !allowed.Contains(extension) ||
                !HasAllowedContentSignature(file.InputStream, extension))
            {
                throw new InvalidOperationException("The selected document has an invalid name, file type, or content.");
            }

            string storedName = Guid.NewGuid().ToString("N") + extension;
            string relativeFolder = "tender-applications/" +
                tenderId.ToString(CultureInfo.InvariantCulture) + "/" +
                contractorId.ToString(CultureInfo.InvariantCulture);
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = page.Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            file.SaveAs(Path.Combine(folder, storedName));
            return "Uploads/" + relativeFolder + "/" + storedName;
        }

        public static string SaveTenderFinancialUpload(Page page, HttpPostedFile file, int tenderId,
            int contractorId, int maxFileSizeBytes)
        {
            if (page == null || file == null || file.ContentLength <= 0 ||
                maxFileSizeBytes < 1 || maxFileSizeBytes > 10 * 1024 * 1024 ||
                file.ContentLength > maxFileSizeBytes)
                throw new InvalidOperationException("Choose a non-empty supporting document within the 10 MB limit.");

            string fileName = SafeFileName(file.FileName);
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (fileName.Length == 0 || fileName.Length > 260 ||
                Array.IndexOf(AllowedExtensions, extension) < 0 ||
                !HasAllowedContentSignature(file.InputStream, extension))
                throw new InvalidOperationException("The selected supporting document has an invalid name, type, or content.");

            return SavePrivateFile(page, "tender-financial/" +
                tenderId.ToString(CultureInfo.InvariantCulture) + "/" +
                contractorId.ToString(CultureInfo.InvariantCulture),
                Guid.NewGuid().ToString("N") + extension, file.InputStream, file.ContentLength);
        }

        public static string SaveGeneratedTenderDocument(Page page, int tenderId,
            string fileName, byte[] content)
        {
            if (page == null || tenderId <= 0 || content == null || content.Length == 0 ||
                content.Length > 20 * 1024 * 1024)
                throw new InvalidOperationException("The generated document is empty or exceeds the storage limit.");
            string safeName = SafeFileName(fileName);
            if (safeName.Length == 0 || safeName.Length > 260 ||
                !String.Equals(Path.GetExtension(safeName), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The generated file name is invalid.");

            string relativeFolder = "tender-generated/" + tenderId.ToString(CultureInfo.InvariantCulture);
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = page.Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            string storedName = Guid.NewGuid().ToString("N") + ".pdf";
            File.WriteAllBytes(Path.Combine(folder, storedName), content);
            return "Uploads/" + relativeFolder + "/" + storedName;
        }

        private static string SaveUpload(Page page, FileUpload upload, int tenderId, string uploadKind,
            string relativeFolderName)
        {
            if (upload == null || !upload.HasFile)
                throw new InvalidOperationException("Select the required " + uploadKind + " document.");

            string extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
            if (upload.PostedFile.ContentLength <= 0 ||
                upload.PostedFile.ContentLength > 20 * 1024 * 1024 ||
                Array.IndexOf(AllowedExtensions, extension) < 0 ||
                !HasAllowedContentSignature(upload.PostedFile.InputStream, extension))
            {
                throw new InvalidOperationException("Choose a valid PDF, Office document, JPEG or PNG no larger than 20 MB.");
            }

            string storedName = Guid.NewGuid().ToString("N") + extension;
            string relativeFolder = relativeFolderName + "/" + tenderId.ToString(CultureInfo.InvariantCulture);
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = page.Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            upload.SaveAs(Path.Combine(folder, storedName));
            return "Uploads/" + relativeFolder + "/" + storedName;
        }

        public static void DeleteStoredUpload(Page page, string relativePath)
        {
            DeleteStoredUpload(page, relativePath, "tender-lifecycle");
        }

        public static void DeleteStoredBidUpload(Page page, string relativePath)
        {
            DeleteStoredUpload(page, relativePath, "tender-bids");
        }

        public static void DeleteStoredApplicationUpload(Page page, string relativePath)
        {
            DeleteStoredUpload(page, relativePath, "tender-applications");
        }

        public static void DeleteStoredFinancialUpload(Page page, string relativePath)
        {
            DeleteStoredUpload(page, relativePath, "tender-financial");
        }

        public static void DeleteStoredGeneratedTenderDocument(Page page, string relativePath)
        {
            DeleteStoredUpload(page, relativePath, "tender-generated");
        }

        private static string SavePrivateFile(Page page, string relativeFolder, string storedName,
            Stream source, int contentLength)
        {
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = page.Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, storedName);
            using (FileStream destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                int remaining = contentLength;
                while (remaining > 0 && (read = source.Read(buffer, 0, Math.Min(buffer.Length, remaining))) > 0)
                {
                    destination.Write(buffer, 0, read);
                    remaining -= read;
                }
                if (remaining != 0)
                    throw new IOException("The uploaded document could not be stored completely.");
            }
            return "Uploads/" + relativeFolder + "/" + storedName;
        }

        private static void DeleteStoredUpload(Page page, string relativePath, string relativeFolderName)
        {
            if (String.IsNullOrWhiteSpace(relativePath)) return;
            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            string expectedPrefix = "Uploads/" + relativeFolderName + "/";
            if (!normalized.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("..") || Path.IsPathRooted(normalized)) return;

            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string root = Path.GetFullPath(page.Server.MapPath(uploadRoot));
            string path = Path.GetFullPath(Path.Combine(
                root, normalized.Substring("Uploads/".Length).Replace('/', Path.DirectorySeparatorChar)));
            if (path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static string NormalizeAllowedApplicationExtensions(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            HashSet<string> extensions = ParseAllowedExtensions(value);
            if (extensions.Count == 0)
                throw new InvalidOperationException("Select at least one supported file extension.");
            return String.Join(",", Array.ConvertAll(AllowedExtensions,
                extension => extensions.Contains(extension) ? extension : null)
                .Where(extension => extension != null).ToArray());
        }

        private static HashSet<string> ParseAllowedExtensions(string value)
        {
            HashSet<string> supported = new HashSet<string>(AllowedExtensions, StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrWhiteSpace(value)) return supported;

            HashSet<string> parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] requested = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in requested)
            {
                string extension = item.Trim();
                if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;
                if (!supported.Contains(extension))
                    throw new InvalidOperationException("Allowed extensions must be selected from PDF, Office, JPEG, or PNG formats.");
                parsed.Add(extension);
            }
            if (parsed.Count == 0)
                throw new InvalidOperationException("Select at least one supported file extension.");
            return parsed;
        }

        private static bool HasAllowedContentSignature(Stream stream, string extension)
        {
            byte[] header = new byte[8];
            long originalPosition = stream.CanSeek ? stream.Position : 0;
            int count = stream.Read(header, 0, header.Length);
            if (stream.CanSeek) stream.Position = originalPosition;

            if (extension == ".pdf")
                return count >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 &&
                    header[3] == 0x46 && header[4] == 0x2D;
            if (extension == ".jpg" || extension == ".jpeg")
                return count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            if (extension == ".png")
                return count == 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
                    header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A &&
                    header[6] == 0x1A && header[7] == 0x0A;
            if (extension == ".doc" || extension == ".xls")
                return count >= 8 && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 &&
                    header[3] == 0xE0 && header[4] == 0xA1 && header[5] == 0xB1 &&
                    header[6] == 0x1A && header[7] == 0xE1;
            return (extension == ".docx" || extension == ".xlsx") && count >= 4 &&
                header[0] == 0x50 && header[1] == 0x4B &&
                (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07) &&
                (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08);
        }

        public static string SafeFileName(string fileName)
        {
            return Path.GetFileName(fileName ?? String.Empty);
        }
    }
}
