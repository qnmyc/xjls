using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace KTXGame.ExportTables
{
    public static class ExportTablesToolCore
    {
        private const int DescriptionRowIndex = 0;
        private const int TypeRowIndex = 1;
        private const int FieldNameRowIndex = 2;
        private const int FirstDataRowIndex = 3;
        private const string ManifestFileName = "tables_manifest.json";

        public static ExportResult Export(ExportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            string excelRootPath = NormalizeDirectoryPath(request.ExcelRootPath);
            string outputRootPath = NormalizeDirectoryPath(request.OutputRootPath);
            string generatedCodeRootPath = NormalizeDirectoryPath(request.GeneratedCodeRootPath);

            if (!Directory.Exists(excelRootPath))
            {
                throw new DirectoryNotFoundException("Excel root directory does not exist: " + excelRootPath);
            }

            if (!Directory.Exists(outputRootPath))
            {
                Directory.CreateDirectory(outputRootPath);
            }

            if (!Directory.Exists(generatedCodeRootPath))
            {
                Directory.CreateDirectory(generatedCodeRootPath);
            }

            ScopeTarget scope = ResolveScopeTarget(excelRootPath, request.ScopePath);
            List<string> excelFiles = CollectExcelFiles(scope);
            ManifestData previousManifest = ManifestData.Load(outputRootPath);

            var generatedEntries = new List<ManifestEntry>();
            var writtenFiles = new List<string>();
            var skippedSheets = new List<string>();
            var deletedFiles = new List<string>();
            var logs = new List<string>();

            DeletePreviousOutputsForScope(previousManifest, outputRootPath, scope, deletedFiles);

            foreach (string excelFilePath in excelFiles)
            {
                WorkbookData workbook = XlsxReader.ReadWorkbook(excelFilePath);
                string sourceRelativePath = GetRelativePathSafe(excelRootPath, excelFilePath);
                string outputDirectoryForFile = ResolveOutputDirectory(outputRootPath, sourceRelativePath);

                if (!Directory.Exists(outputDirectoryForFile))
                {
                    Directory.CreateDirectory(outputDirectoryForFile);
                }

                foreach (SheetData sheet in workbook.Sheets)
                {
                    TableData table = TableParser.TryParse(sourceRelativePath, workbook.FileName, sheet, logs, skippedSheets);
                    if (table == null)
                    {
                        continue;
                    }

                    string jsonFileName = BuildJsonFileName(workbook.FileNameWithoutExtension, table.TableName);
                    string jsonFilePath = Path.Combine(outputDirectoryForFile, jsonFileName);
                    string jsonContent = JsonWriter.WriteTable(table);
                    File.WriteAllText(jsonFilePath, jsonContent, new UTF8Encoding(false));

                    string outputRelativePath = GetRelativePathSafe(outputRootPath, jsonFilePath);
                    writtenFiles.Add(jsonFilePath);
                    generatedEntries.Add(new ManifestEntry
                    {
                        TableName = table.TableName,
                        SourceFile = workbook.FileName,
                        SourceRelativePath = sourceRelativePath,
                        SheetName = table.SheetName,
                        OutputRelativePath = outputRelativePath.Replace('\\', '/'),
                        RowCount = table.RowCount
                    });
                }
            }

            ValidateGeneratedEntries(generatedEntries);
            ManifestData mergedManifest = MergeManifest(previousManifest, generatedEntries, scope);

            foreach (ManifestEntry staleEntry in FindStaleEntries(previousManifest, generatedEntries, scope))
            {
                string staleFilePath = Path.Combine(outputRootPath, staleEntry.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(staleFilePath))
                {
                    File.Delete(staleFilePath);
                    deletedFiles.Add(staleFilePath);
                }

            }

            DeleteEmptyDirectories(outputRootPath);

            string manifestPath = Path.Combine(outputRootPath, ManifestFileName);
            File.WriteAllText(manifestPath, JsonWriter.WriteManifest(mergedManifest), new UTF8Encoding(false));
            WriteGeneratedCodeFile(outputRootPath, generatedCodeRootPath, mergedManifest);

            logs.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Export completed. Scope='{0}', ExcelFiles={1}, Tables={2}, Deleted={3}.",
                string.IsNullOrEmpty(scope.RelativePath) ? "." : scope.RelativePath,
                excelFiles.Count,
                generatedEntries.Count,
                deletedFiles.Count));

            return new ExportResult
            {
                ScopeRelativePath = string.IsNullOrEmpty(scope.RelativePath) ? "." : scope.RelativePath,
                ManifestPath = manifestPath,
                ExportedTables = generatedEntries,
                WrittenFiles = writtenFiles,
                DeletedFiles = deletedFiles,
                SkippedSheets = skippedSheets,
                Logs = logs
            };
        }

        private static ScopeTarget ResolveScopeTarget(string excelRootPath, string scopePath)
        {
            string absolutePath = string.IsNullOrWhiteSpace(scopePath)
                ? excelRootPath
                : NormalizePath(scopePath);

            bool isFile = File.Exists(absolutePath);
            bool isDirectory = Directory.Exists(absolutePath);
            if (!isFile && !isDirectory)
            {
                throw new FileNotFoundException("Scope path does not exist: " + absolutePath);
            }

            string normalizedRoot = AppendDirectorySeparator(excelRootPath);
            string normalizedScope = isDirectory
                ? AppendDirectorySeparator(absolutePath)
                : absolutePath;

            bool insideRoot = isDirectory
                ? normalizedScope.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(excelRootPath, absolutePath, StringComparison.OrdinalIgnoreCase)
                : absolutePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            if (!insideRoot)
            {
                throw new InvalidOperationException("Scope path must be inside the Excel root directory.");
            }

            if (isFile && !string.Equals(Path.GetExtension(absolutePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only .xlsx files are supported for file export.");
            }

            return new ScopeTarget
            {
                AbsolutePath = absolutePath,
                RelativePath = GetRelativePathSafe(excelRootPath, absolutePath),
                IsFile = isFile
            };
        }

        private static string ResolveOutputDirectory(string outputRootPath, string sourceRelativePath)
        {
            string sourceDirectory = Path.GetDirectoryName(sourceRelativePath) ?? string.Empty;
            if (string.IsNullOrEmpty(sourceDirectory))
            {
                return outputRootPath;
            }

            return Path.Combine(outputRootPath, sourceDirectory);
        }

        private static List<string> CollectExcelFiles(ScopeTarget scope)
        {
            if (scope.IsFile)
            {
                return new List<string> { scope.AbsolutePath };
            }

            return Directory.GetFiles(scope.AbsolutePath, "*.xlsx", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<ManifestEntry> FindStaleEntries(ManifestData previousManifest, List<ManifestEntry> generatedEntries, ScopeTarget scope)
        {
            var staleEntries = new List<ManifestEntry>();
            if (previousManifest == null)
            {
                return staleEntries;
            }

            var activeOutputPaths = new HashSet<string>(
                generatedEntries.Select(entry => entry.OutputRelativePath),
                StringComparer.OrdinalIgnoreCase);

            foreach (ManifestEntry previousEntry in previousManifest.Tables)
            {
                if (!IsWithinScope(previousEntry.SourceRelativePath, scope))
                {
                    continue;
                }

                if (!activeOutputPaths.Contains(previousEntry.OutputRelativePath))
                {
                    staleEntries.Add(previousEntry);
                }
            }

            return staleEntries;
        }

        private static ManifestData MergeManifest(ManifestData previousManifest, List<ManifestEntry> generatedEntries, ScopeTarget scope)
        {
            var mergedEntries = new List<ManifestEntry>();

            if (previousManifest != null)
            {
                foreach (ManifestEntry entry in previousManifest.Tables)
                {
                    if (!IsWithinScope(entry.SourceRelativePath, scope))
                    {
                        mergedEntries.Add(entry);
                        continue;
                    }

                    // Everything in the selected scope is regenerated from scratch.
                }
            }

            mergedEntries.AddRange(generatedEntries);
            mergedEntries = mergedEntries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.OutputRelativePath))
                .GroupBy(entry => entry.OutputRelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .OrderBy(entry => entry.OutputRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ManifestData
            {
                GeneratedAtUtc = DateTime.UtcNow,
                Tables = mergedEntries
            };
        }

        private static void DeletePreviousOutputsForScope(
            ManifestData previousManifest,
            string outputRootPath,
            ScopeTarget scope,
            List<string> deletedFiles)
        {
            if (previousManifest == null || previousManifest.Tables == null)
            {
                return;
            }

            foreach (ManifestEntry entry in previousManifest.Tables)
            {
                if (entry == null
                    || !IsWithinScope(entry.SourceRelativePath, scope)
                    || string.IsNullOrWhiteSpace(entry.OutputRelativePath))
                {
                    continue;
                }

                string outputPath = Path.Combine(outputRootPath, entry.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(outputPath))
                {
                    continue;
                }

                File.Delete(outputPath);
                deletedFiles.Add(outputPath);
            }
        }

        private static void ValidateGeneratedEntries(List<ManifestEntry> generatedEntries)
        {
            foreach (IGrouping<string, ManifestEntry> tableGroup in generatedEntries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.TableName))
                .GroupBy(entry => entry.TableName, StringComparer.OrdinalIgnoreCase))
            {
                if (tableGroup.Count() <= 1)
                {
                    continue;
                }

                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Duplicate exported tableName '{0}'. Sheet names using schema prefixes must still resolve to unique table names.",
                    tableGroup.Key));
            }
        }

        private static bool IsWithinScope(string sourceRelativePath, ScopeTarget scope)
        {
            string normalizedSource = NormalizeRelativePath(sourceRelativePath);
            string normalizedScope = NormalizeRelativePath(scope.RelativePath);
            if (string.IsNullOrEmpty(normalizedScope))
            {
                return true;
            }

            if (scope.IsFile)
            {
                return string.Equals(normalizedSource, normalizedScope, StringComparison.OrdinalIgnoreCase);
            }

            normalizedScope = normalizedScope.TrimEnd('/') + "/";
            return normalizedSource.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildJsonFileName(string workbookNameWithoutExtension, string sheetName)
        {
            return SanitizeFileName(workbookNameWithoutExtension) + "_" + SanitizeFileName(sheetName) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                builder.Append(invalidChars.Contains(character) ? '_' : character);
            }

            return builder.ToString().Trim();
        }

        private static void DeleteEmptyDirectories(string rootPath)
        {
            foreach (string directory in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length))
            {
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
        }

        private static void WriteGeneratedCodeFile(string outputRootPath, string generatedCodeRootPath, ManifestData manifest)
        {
            string generatedCodeFilePath = Path.Combine(generatedCodeRootPath, CodeGenerator.GeneratedCodeFileName);
            List<TableData> tables = LoadTablesForCodeGeneration(outputRootPath, manifest);

            if (tables.Count == 0)
            {
                if (File.Exists(generatedCodeFilePath))
                {
                    File.Delete(generatedCodeFilePath);
                }

                return;
            }

            string codeContent = CodeGenerator.WriteAllTableClasses(tables);
            File.WriteAllText(generatedCodeFilePath, codeContent, new UTF8Encoding(false));
        }

        private static List<TableData> LoadTablesForCodeGeneration(string outputRootPath, ManifestData manifest)
        {
            var tables = new List<TableData>();
            if (manifest == null || manifest.Tables == null)
            {
                return tables;
            }

            foreach (ManifestEntry entry in manifest.Tables.OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.OutputRelativePath))
                {
                    continue;
                }

                string jsonFilePath = Path.Combine(outputRootPath, entry.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(jsonFilePath))
                {
                    continue;
                }

                TableData table = ExportedTableReader.ReadTableSchema(jsonFilePath);
                if (table != null)
                {
                    tables.Add(table);
                }
            }

            return tables
                .Where(table => table != null && !string.IsNullOrWhiteSpace(table.SchemaName))
                .GroupBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
                .Select(ValidateAndSelectSchemaTable)
                .OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static TableData ValidateAndSelectSchemaTable(IGrouping<string, TableData> schemaGroup)
        {
            TableData selected = schemaGroup.First();
            foreach (TableData table in schemaGroup.Skip(1))
            {
                if (!HasSameColumns(selected, table))
                {
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Schema '{0}' is used by multiple sheets with different column definitions. Check sheet '{1}' and sheet '{2}'.",
                        schemaGroup.Key,
                        selected.SheetName,
                        table.SheetName));
                }
            }

            return selected;
        }

        private static bool HasSameColumns(TableData left, TableData right)
        {
            if (left.Columns.Count != right.Columns.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Columns.Count; i++)
            {
                TableColumn leftColumn = left.Columns[i];
                TableColumn rightColumn = right.Columns[i];
                if (!string.Equals(leftColumn.Name, rightColumn.Name, StringComparison.Ordinal)
                    || !string.Equals(leftColumn.Type, rightColumn.Type, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string GetRelativePathSafe(string rootPath, string targetPath)
        {
            string relativePath = GetRelativePath(rootPath, targetPath);
            if (relativePath == ".")
            {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/');
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            string normalized = (relativePath ?? string.Empty).Replace('\\', '/').Trim('/');
            return normalized;
        }

        private static string GetRelativePath(string rootPath, string targetPath)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(rootPath));
            Uri targetUri = new Uri(targetPath);
            string relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(targetUri).ToString());
            return string.IsNullOrEmpty(relativePath) ? "." : relativePath;
        }

        private sealed class ScopeTarget
        {
            public string AbsolutePath;
            public string RelativePath;
            public bool IsFile;
        }
    }

    [Serializable]
    public sealed class ExportRequest
    {
        public string ExcelRootPath;
        public string OutputRootPath;
        public string GeneratedCodeRootPath;
        public string ScopePath;
    }

    public sealed class ExportResult
    {
        public string ScopeRelativePath;
        public string ManifestPath;
        public List<ManifestEntry> ExportedTables = new List<ManifestEntry>();
        public List<string> WrittenFiles = new List<string>();
        public List<string> DeletedFiles = new List<string>();
        public List<string> SkippedSheets = new List<string>();
        public List<string> Logs = new List<string>();
    }

    public sealed class ManifestData
    {
        public DateTime GeneratedAtUtc;
        public List<ManifestEntry> Tables = new List<ManifestEntry>();

        public static ManifestData Load(string outputRootPath)
        {
            string manifestPath = Path.Combine(outputRootPath, "tables_manifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            return ManifestParser.Parse(json);
        }
    }

    public sealed class ManifestEntry
    {
        public string TableName;
        public string SourceFile;
        public string SourceRelativePath;
        public string SheetName;
        public string OutputRelativePath;
        public int RowCount;
    }

    internal sealed class WorkbookData
    {
        public string FileName;
        public string FileNameWithoutExtension;
        public List<SheetData> Sheets = new List<SheetData>();
    }

    internal sealed class SheetData
    {
        public string Name;
        public List<List<string>> Rows = new List<List<string>>();
    }

    internal sealed class TableData
    {
        public string TableName;
        public string SchemaName;
        public string SourceFile;
        public string SourceRelativePath;
        public string SheetName;
        public int RowCount;
        public List<TableColumn> Columns = new List<TableColumn>();
        public List<Dictionary<string, object>> Records = new List<Dictionary<string, object>>();
    }

    internal sealed class TableColumn
    {
        public string Description;
        public string Type;
        public string Name;
    }

    internal static class TableParser
    {
        public static TableData TryParse(
            string sourceRelativePath,
            string sourceFile,
            SheetData sheet,
            List<string> logs,
            List<string> skippedSheets)
        {
            if (sheet.Rows.Count <= ExportTablesToolCorePrivate.FirstDataRowIndex)
            {
                skippedSheets.Add(sourceRelativePath + ":" + sheet.Name + " skipped: sheet has fewer than 4 rows.");
                return null;
            }

            List<string> descriptions = NormalizeRow(sheet.Rows[ExportTablesToolCorePrivate.DescriptionRowIndex]);
            List<string> types = NormalizeRow(sheet.Rows[ExportTablesToolCorePrivate.TypeRowIndex]);
            List<string> names = NormalizeRow(sheet.Rows[ExportTablesToolCorePrivate.FieldNameRowIndex]);
            ParsedSheetName parsedSheetName = ParseSheetName(sheet.Name);

            int columnCount = Math.Max(descriptions.Count, Math.Max(types.Count, names.Count));
            if (columnCount == 0)
            {
                skippedSheets.Add(sourceRelativePath + ":" + sheet.Name + " skipped: no header columns.");
                return null;
            }

            var columns = new List<TableColumn>();
            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                string description = GetCell(descriptions, columnIndex);
                string type = GetCell(types, columnIndex);
                string name = GetCell(names, columnIndex);

                if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(type) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
                {
                    var missingParts = new List<string>();
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        missingParts.Add("description");
                    }

                    if (string.IsNullOrWhiteSpace(type))
                    {
                        missingParts.Add("type");
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        missingParts.Add("field name");
                    }

                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid header in {0}:{1}, column {2}. Missing {3}.",
                        sourceRelativePath,
                        sheet.Name,
                        columnIndex + 1,
                        string.Join(", ", missingParts.ToArray())));
                }

                columns.Add(new TableColumn
                {
                    Description = description,
                    Type = NormalizeType(type, sourceRelativePath, sheet.Name, columnIndex + 1),
                    Name = name
                });
            }

            if (columns.Count == 0)
            {
                skippedSheets.Add(sourceRelativePath + ":" + sheet.Name + " skipped: no valid columns.");
                return null;
            }

            var records = new List<Dictionary<string, object>>();
            for (int rowIndex = ExportTablesToolCorePrivate.FirstDataRowIndex; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                List<string> row = NormalizeRow(sheet.Rows[rowIndex]);
                if (IsEmptyRow(row))
                {
                    continue;
                }

                var record = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    TableColumn column = columns[columnIndex];
                    string rawValue = GetCell(row, columnIndex);
                    record[column.Name] = ConvertCellValue(rawValue, column.Type, sourceRelativePath, sheet.Name, rowIndex + 1, columnIndex + 1);
                }

                records.Add(record);
            }

            if (records.Count == 0)
            {
                skippedSheets.Add(sourceRelativePath + ":" + sheet.Name + " skipped: no data rows.");
                return null;
            }

            logs.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Parsed {0}:{1} rows={2}.",
                sourceRelativePath,
                sheet.Name,
                records.Count));

            return new TableData
            {
                TableName = parsedSheetName.TableName,
                SchemaName = parsedSheetName.SchemaName,
                SourceFile = sourceFile,
                SourceRelativePath = sourceRelativePath,
                SheetName = sheet.Name,
                RowCount = records.Count,
                Columns = columns,
                Records = records
            };
        }

        private static ParsedSheetName ParseSheetName(string sheetName)
        {
            string normalized = (sheetName ?? string.Empty).Trim();
            int separatorIndex = normalized.IndexOf('.');
            if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
            {
                return new ParsedSheetName
                {
                    TableName = normalized,
                    SchemaName = normalized
                };
            }

            return new ParsedSheetName
            {
                SchemaName = normalized.Substring(0, separatorIndex),
                TableName = normalized.Substring(separatorIndex + 1)
            };
        }

        private static List<string> NormalizeRow(List<string> row)
        {
            return row == null ? new List<string>() : row.Select(cell => cell == null ? string.Empty : cell.Trim()).ToList();
        }

        private static string GetCell(List<string> row, int index)
        {
            return index >= 0 && index < row.Count ? row[index] : string.Empty;
        }

        private static bool IsEmptyRow(List<string> row)
        {
            return row.All(string.IsNullOrWhiteSpace);
        }

        private static string NormalizeType(string type, string sourceRelativePath, string sheetName, int columnNumber)
        {
            string normalized = type.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "int":
                case "int32":
                    return "int";
                case "long":
                case "int64":
                    return "long";
                case "float":
                case "single":
                    return "float";
                case "double":
                    return "double";
                case "bool":
                case "boolean":
                    return "bool";
                case "string":
                    return "string";
                default:
                    throw new InvalidDataException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Unsupported field type '{0}' in {1}:{2}, header row {3}, column {4}.",
                        type,
                        sourceRelativePath,
                        sheetName,
                        ExportTablesToolCorePrivate.TypeRowIndex + 1,
                        columnNumber));
            }
        }

        private static object ConvertCellValue(string rawValue, string type, string sourceRelativePath, string sheetName, int rowNumber, int columnNumber)
        {
            string value = rawValue == null ? string.Empty : rawValue.Trim();
            if (string.IsNullOrEmpty(value))
            {
                if (type == "string")
                {
                    return string.Empty;
                }

                return null;
            }

            try
            {
                switch (type)
                {
                    case "int":
                        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case "long":
                        return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case "float":
                        return float.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    case "double":
                        return double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                    case "bool":
                        return ParseBoolean(value);
                    case "string":
                        return value;
                    default:
                        throw new InvalidDataException("Unsupported field type: " + type);
                }
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Failed to convert cell in {0}:{1} at row {2}, column {3}, value '{4}', targetType '{5}'.",
                        sourceRelativePath,
                        sheetName,
                        rowNumber,
                        columnNumber,
                        value,
                        type),
                    exception);
            }
        }

        private static bool ParseBoolean(string value)
        {
            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return bool.Parse(value);
        }

        private sealed class ParsedSheetName
        {
            public string TableName;
            public string SchemaName;
        }
    }

    internal static class XlsxReader
    {
        private static readonly XNamespace SpreadsheetMl = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static WorkbookData ReadWorkbook(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                var sharedStrings = ReadSharedStrings(archive);
                var sheetTargets = ReadSheetTargets(archive);

                var workbook = new WorkbookData
                {
                    FileName = Path.GetFileName(filePath),
                    FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath)
                };

                foreach (var sheetTarget in sheetTargets)
                {
                    workbook.Sheets.Add(ReadSheet(archive, sheetTarget.Name, sheetTarget.Target, sharedStrings));
                }

                return workbook;
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            XDocument document = LoadXml(entry);
            return document.Root
                .Elements(SpreadsheetMl + "si")
                .Select(ReadSharedStringItem)
                .ToList();
        }

        private static string ReadSharedStringItem(XElement item)
        {
            IEnumerable<XElement> textNodes = item.Descendants(SpreadsheetMl + "t");
            var builder = new StringBuilder();
            foreach (XElement textNode in textNodes)
            {
                builder.Append(textNode.Value);
            }

            return builder.ToString();
        }

        private static List<SheetTarget> ReadSheetTargets(ZipArchive archive)
        {
            XDocument workbookDocument = LoadXml(RequireEntry(archive, "xl/workbook.xml"));
            XDocument relationsDocument = LoadXml(RequireEntry(archive, "xl/_rels/workbook.xml.rels"));

            var relationMap = relationsDocument.Root
                .Elements(PackageRelationshipNs + "Relationship")
                .ToDictionary(
                    element => (string)element.Attribute("Id"),
                    element => NormalizeZipPath("xl/" + (string)element.Attribute("Target")),
                    StringComparer.Ordinal);

            return workbookDocument.Root
                .Element(SpreadsheetMl + "sheets")
                .Elements(SpreadsheetMl + "sheet")
                .Select(sheet => new SheetTarget
                {
                    Name = (string)sheet.Attribute("name"),
                    Target = relationMap[(string)sheet.Attribute(RelationshipNs + "id")]
                })
                .ToList();
        }

        private static SheetData ReadSheet(ZipArchive archive, string sheetName, string entryPath, List<string> sharedStrings)
        {
            XDocument document = LoadXml(RequireEntry(archive, entryPath));
            var rowMap = new SortedDictionary<int, List<string>>();

            XElement sheetData = document.Root.Element(SpreadsheetMl + "sheetData");
            if (sheetData != null)
            {
                foreach (XElement rowElement in sheetData.Elements(SpreadsheetMl + "row"))
                {
                    int rowIndex = ParseRowIndex(rowElement);
                    var rowValues = new List<string>();

                    foreach (XElement cellElement in rowElement.Elements(SpreadsheetMl + "c"))
                    {
                        string cellReference = (string)cellElement.Attribute("r");
                        int columnIndex = ParseColumnIndex(cellReference);
                        EnsureSize(rowValues, columnIndex + 1);
                        rowValues[columnIndex] = ReadCellValue(cellElement, sharedStrings);
                    }

                    rowMap[rowIndex] = rowValues;
                }
            }

            int maxRowIndex = rowMap.Count == 0 ? -1 : rowMap.Keys.Max();
            var rows = new List<List<string>>();
            for (int rowIndex = 0; rowIndex <= maxRowIndex; rowIndex++)
            {
                List<string> rowValues;
                if (rowMap.TryGetValue(rowIndex, out rowValues))
                {
                    rows.Add(rowValues);
                }
                else
                {
                    rows.Add(new List<string>());
                }
            }

            return new SheetData
            {
                Name = sheetName,
                Rows = rows
            };
        }

        private static string ReadCellValue(XElement cellElement, List<string> sharedStrings)
        {
            string dataType = (string)cellElement.Attribute("t");

            if (string.Equals(dataType, "inlineStr", StringComparison.Ordinal))
            {
                IEnumerable<XElement> texts = cellElement.Descendants(SpreadsheetMl + "t");
                return string.Concat(texts.Select(element => element.Value));
            }

            XElement valueElement = cellElement.Element(SpreadsheetMl + "v");
            string rawValue = valueElement == null ? string.Empty : valueElement.Value;

            if (string.Equals(dataType, "s", StringComparison.Ordinal))
            {
                int sharedStringIndex;
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedStringIndex)
                    && sharedStringIndex >= 0
                    && sharedStringIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedStringIndex];
                }

                return string.Empty;
            }

            if (string.Equals(dataType, "b", StringComparison.Ordinal))
            {
                return rawValue == "1" ? "true" : "false";
            }

            return rawValue;
        }

        private static int ParseRowIndex(XElement rowElement)
        {
            XAttribute rowAttribute = rowElement.Attribute("r");
            if (rowAttribute == null)
            {
                return 0;
            }

            return int.Parse(rowAttribute.Value, CultureInfo.InvariantCulture) - 1;
        }

        private static int ParseColumnIndex(string cellReference)
        {
            if (string.IsNullOrEmpty(cellReference))
            {
                return 0;
            }

            int column = 0;
            foreach (char character in cellReference)
            {
                if (character < 'A' || character > 'Z')
                {
                    break;
                }

                column = (column * 26) + (character - 'A' + 1);
            }

            return Math.Max(0, column - 1);
        }

        private static void EnsureSize(List<string> rowValues, int size)
        {
            while (rowValues.Count < size)
            {
                rowValues.Add(string.Empty);
            }
        }

        private static ZipArchiveEntry RequireEntry(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);
            if (entry == null)
            {
                throw new InvalidDataException("Missing xlsx entry: " + path);
            }

            return entry;
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static string NormalizeZipPath(string path)
        {
            var segments = new List<string>();
            foreach (string segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment == "." || string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                    continue;
                }

                segments.Add(segment);
            }

            return string.Join("/", segments.ToArray());
        }

        private sealed class SheetTarget
        {
            public string Name;
            public string Target;
        }
    }

    internal static class JsonWriter
    {
        public static string WriteTable(TableData table)
        {
            var root = new Dictionary<string, object>
            {
                { "tableName", table.TableName },
                { "schemaName", table.SchemaName },
                { "sourceFile", table.SourceFile },
                { "sourceRelativePath", table.SourceRelativePath },
                { "sheetName", table.SheetName },
                { "rowCount", table.RowCount },
                {
                    "columns",
                    table.Columns.Select(column => new Dictionary<string, object>
                    {
                        { "name", column.Name },
                        { "type", column.Type },
                        { "description", column.Description }
                    }).ToList()
                },
                { "data", table.Records }
            };

            return Serialize(root);
        }

        public static string WriteManifest(ManifestData manifest)
        {
            var root = new Dictionary<string, object>
            {
                { "generatedAtUtc", manifest.GeneratedAtUtc.ToString("o", CultureInfo.InvariantCulture) },
                {
                    "tables",
                    manifest.Tables.Select(entry => new Dictionary<string, object>
                    {
                        { "tableName", entry.TableName },
                        { "sourceFile", entry.SourceFile },
                        { "sourceRelativePath", entry.SourceRelativePath },
                        { "sheetName", entry.SheetName },
                        { "outputRelativePath", entry.OutputRelativePath },
                        { "rowCount", entry.RowCount }
                    }).ToList()
                }
            };

            return Serialize(root);
        }

        private static string Serialize(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, 0);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value, int indent)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string)
            {
                WriteString(builder, (string)value);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is float || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                WriteObject(builder, dictionary, indent);
                return;
            }

            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                WriteArray(builder, enumerable, indent);
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary<string, object> dictionary, int indent)
        {
            builder.Append('{');
            if (dictionary.Count == 0)
            {
                builder.Append('}');
                return;
            }

            builder.AppendLine();
            int index = 0;
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                builder.Append(' ', (indent + 1) * 2);
                WriteString(builder, pair.Key);
                builder.Append(": ");
                WriteValue(builder, pair.Value, indent + 1);
                index++;
                if (index < dictionary.Count)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            builder.Append(' ', indent * 2);
            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, System.Collections.IEnumerable values, int indent)
        {
            var items = values.Cast<object>().ToList();
            builder.Append('[');
            if (items.Count == 0)
            {
                builder.Append(']');
                return;
            }

            builder.AppendLine();
            for (int index = 0; index < items.Count; index++)
            {
                builder.Append(' ', (indent + 1) * 2);
                WriteValue(builder, items[index], indent + 1);
                if (index < items.Count - 1)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            builder.Append(' ', indent * 2);
            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
        }
    }

    internal static class ExportedTableReader
    {
        public static TableData ReadTableSchema(string jsonFilePath)
        {
            string json = File.ReadAllText(jsonFilePath, Encoding.UTF8);
            var parser = new JsonParser(json);
            Dictionary<string, object> root = parser.ParseObject();
            if (root == null)
            {
                return null;
            }

            var table = new TableData
            {
                TableName = ReadString(root, "tableName"),
                SchemaName = ReadString(root, "schemaName"),
                SourceFile = ReadString(root, "sourceFile"),
                SourceRelativePath = ReadString(root, "sourceRelativePath"),
                SheetName = ReadString(root, "sheetName"),
                RowCount = ReadInt(root, "rowCount")
            };
            if (string.IsNullOrWhiteSpace(table.SchemaName))
            {
                table.SchemaName = table.TableName;
            }

            object columnsValue;
            if (!root.TryGetValue("columns", out columnsValue))
            {
                return table;
            }

            var columns = columnsValue as List<object>;
            if (columns == null)
            {
                return table;
            }

            foreach (object columnValue in columns)
            {
                var columnObject = columnValue as Dictionary<string, object>;
                if (columnObject == null)
                {
                    continue;
                }

                table.Columns.Add(new TableColumn
                {
                    Name = ReadString(columnObject, "name"),
                    Type = ReadString(columnObject, "type"),
                    Description = ReadString(columnObject, "description")
                });
            }

            return table;
        }

        private static string ReadString(Dictionary<string, object> objectValue, string key)
        {
            object value;
            return objectValue.TryGetValue(key, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private static int ReadInt(Dictionary<string, object> objectValue, string key)
        {
            object value;
            if (!objectValue.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is long)
            {
                return (int)(long)value;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    internal static class CodeGenerator
    {
        public const string GeneratedCodeFileName = "GameDataTables.cs";

        public static string WriteAllTableClasses(List<TableData> tables)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            for (int i = 0; i < tables.Count; i++)
            {
                TableData table = tables[i];
                string rowClassName = BuildRowClassName(table.SchemaName);
                string tableClassName = BuildTableClassName(table.SchemaName);

                builder.AppendLine();
                builder.AppendLine("[Serializable]");
                builder.AppendLine("public class " + rowClassName);
                builder.AppendLine("{");
                foreach (TableColumn column in table.Columns)
                {
                    builder.AppendLine("    public " + MapCSharpType(column.Type) + " " + SanitizeFieldIdentifier(column.Name) + ";");
                }
                builder.AppendLine("}");
                builder.AppendLine();
                builder.AppendLine("[Serializable]");
                builder.AppendLine("public class " + tableClassName);
                builder.AppendLine("{");
                builder.AppendLine("    public string tableName;");
                builder.AppendLine("    public string sourceFile;");
                builder.AppendLine("    public string sourceRelativePath;");
                builder.AppendLine("    public string sheetName;");
                builder.AppendLine("    public int rowCount;");
                builder.AppendLine("    public List<" + rowClassName + "> data;");
                builder.AppendLine("}");
            }

            return builder.ToString();
        }

        private static string BuildRowClassName(string tableName)
        {
            return BuildBaseIdentifier(tableName) + "Record";
        }

        private static string BuildTableClassName(string tableName)
        {
            return BuildBaseIdentifier(tableName) + "Table";
        }

        private static string BuildBaseIdentifier(string value)
        {
            string sanitized = SanitizeTypeIdentifier(value);
            return string.IsNullOrEmpty(sanitized) ? "GeneratedTable" : sanitized;
        }

        private static string SanitizeTypeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool capitalizeNext = true;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character))
                {
                    if (builder.Length == 0 && char.IsDigit(character))
                    {
                        builder.Append('_');
                    }

                    builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            string result = builder.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return string.Empty;
            }

            if (CSharpKeywords.Contains(result))
            {
                return result + "Value";
            }

            return result;
        }

        private static string SanitizeFieldIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "fieldValue";
            }

            var builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    if (builder.Length == 0 && char.IsDigit(character))
                    {
                        builder.Append('_');
                    }

                    builder.Append(character);
                }
            }

            string result = builder.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return "fieldValue";
            }

            if (CSharpKeywords.Contains(result))
            {
                return "@" + result;
            }

            return result;
        }

        private static string MapCSharpType(string tableType)
        {
            switch (tableType)
            {
                case "int":
                    return "int";
                case "long":
                    return "long";
                case "float":
                    return "float";
                case "double":
                    return "double";
                case "bool":
                    return "bool";
                case "string":
                    return "string";
                default:
                    return "string";
            }
        }

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };
    }

    internal static class ManifestParser
    {
        public static ManifestData Parse(string json)
        {
            var parser = new JsonParser(json);
            var root = parser.ParseObject();
            var manifest = new ManifestData();

            object generatedAtValue;
            if (root.TryGetValue("generatedAtUtc", out generatedAtValue))
            {
                DateTime parsedTime;
                if (DateTime.TryParse(Convert.ToString(generatedAtValue, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsedTime))
                {
                    manifest.GeneratedAtUtc = parsedTime;
                }
            }

            object tablesValue;
            if (root.TryGetValue("tables", out tablesValue))
            {
                var tables = tablesValue as List<object>;
                if (tables != null)
                {
                    foreach (object tableValue in tables)
                    {
                        var entryObject = tableValue as Dictionary<string, object>;
                        if (entryObject == null)
                        {
                            continue;
                        }

                        manifest.Tables.Add(new ManifestEntry
                        {
                            TableName = ReadString(entryObject, "tableName"),
                            SourceFile = ReadString(entryObject, "sourceFile"),
                            SourceRelativePath = ReadString(entryObject, "sourceRelativePath"),
                            SheetName = ReadString(entryObject, "sheetName"),
                            OutputRelativePath = ReadString(entryObject, "outputRelativePath"),
                            RowCount = ReadInt(entryObject, "rowCount")
                        });
                    }
                }
            }

            return manifest;
        }

        private static string ReadString(Dictionary<string, object> objectValue, string key)
        {
            object value;
            return objectValue.TryGetValue(key, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private static int ReadInt(Dictionary<string, object> objectValue, string key)
        {
            object value;
            if (!objectValue.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            if (value is int)
            {
                return (int)value;
            }

            if (value is long)
            {
                return (int)(long)value;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class JsonParser
    {
        private readonly string text;
        private int index;

        public JsonParser(string text)
        {
            this.text = text ?? string.Empty;
        }

        public Dictionary<string, object> ParseObject()
        {
            SkipWhitespace();
            return ReadObject();
        }

        private object ReadValue()
        {
            SkipWhitespace();
            if (index >= text.Length)
            {
                throw new InvalidDataException("Unexpected end of JSON.");
            }

            char current = text[index];
            switch (current)
            {
                case '{':
                    return ReadObject();
                case '[':
                    return ReadArray();
                case '"':
                    return ReadString();
                case 't':
                    ReadLiteral("true");
                    return true;
                case 'f':
                    ReadLiteral("false");
                    return false;
                case 'n':
                    ReadLiteral("null");
                    return null;
                default:
                    return ReadNumber();
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            Expect('{');
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            SkipWhitespace();

            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                SkipWhitespace();
                Expect(':');
                object value = ReadValue();
                result[key] = value;
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object> ReadArray()
        {
            Expect('[');
            var result = new List<object>();
            SkipWhitespace();

            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ReadValue());
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ReadString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (index < text.Length)
            {
                char current = text[index++];
                if (current == '"')
                {
                    return builder.ToString();
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (index >= text.Length)
                {
                    break;
                }

                char escaped = text[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        string hex = text.Substring(index, 4);
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new InvalidDataException("Invalid JSON escape: \\" + escaped);
                }
            }

            throw new InvalidDataException("Unterminated JSON string.");
        }

        private object ReadNumber()
        {
            int start = index;
            while (index < text.Length)
            {
                char current = text[index];
                if ((current >= '0' && current <= '9') || current == '-' || current == '+' || current == '.' || current == 'e' || current == 'E')
                {
                    index++;
                    continue;
                }

                break;
            }

            string numberText = text.Substring(start, index - start);
            if (numberText.IndexOf('.') >= 0 || numberText.IndexOf('e') >= 0 || numberText.IndexOf('E') >= 0)
            {
                return double.Parse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            long integerValue;
            if (long.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
            {
                if (integerValue >= int.MinValue && integerValue <= int.MaxValue)
                {
                    return (int)integerValue;
                }

                return integerValue;
            }

            throw new InvalidDataException("Invalid JSON number: " + numberText);
        }

        private void ReadLiteral(string value)
        {
            for (int literalIndex = 0; literalIndex < value.Length; literalIndex++)
            {
                if (index >= text.Length || text[index] != value[literalIndex])
                {
                    throw new InvalidDataException("Invalid JSON literal: " + value);
                }

                index++;
            }
        }

        private void SkipWhitespace()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        private void Expect(char value)
        {
            SkipWhitespace();
            if (index >= text.Length || text[index] != value)
            {
                throw new InvalidDataException("Expected JSON token: " + value);
            }

            index++;
        }

        private bool TryConsume(char value)
        {
            SkipWhitespace();
            if (index < text.Length && text[index] == value)
            {
                index++;
                return true;
            }

            return false;
        }
    }

    internal static class ExportTablesToolCorePrivate
    {
        public const int DescriptionRowIndex = 0;
        public const int TypeRowIndex = 1;
        public const int FieldNameRowIndex = 2;
        public const int FirstDataRowIndex = 3;
    }

#if UNITY_EDITOR
    public static class ExportTablesToolPaths
    {
        public const string ExcelRootRelativePath = "Assets/Resources/Docs/Tables";
        public const string OutputRelativePath = "Assets/Resources/GameData/ExportedTables";
        public const string GeneratedCodeRelativePath = "Assets/Scripts/GameData";

        public static string ProjectRootPath
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        public static string ExcelRootAbsolutePath
        {
            get { return Path.GetFullPath(Path.Combine(ProjectRootPath, ExcelRootRelativePath)); }
        }

        public static string OutputAbsolutePath
        {
            get { return Path.GetFullPath(Path.Combine(ProjectRootPath, OutputRelativePath)); }
        }

        public static string GeneratedCodeAbsolutePath
        {
            get { return Path.GetFullPath(Path.Combine(ProjectRootPath, GeneratedCodeRelativePath)); }
        }

        public static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return ExcelRootAbsolutePath;
            }

            return Path.GetFullPath(Path.Combine(ProjectRootPath, assetPath));
        }

        public static string ToAssetPath(string absolutePath)
        {
            string normalizedProjectRoot = ProjectRootPath.Replace('\\', '/').TrimEnd('/') + "/";
            return Path.GetFullPath(absolutePath).Replace('\\', '/').Replace(normalizedProjectRoot, string.Empty);
        }
    }

    public static class ExportTablesToolMenu
    {
        [MenuItem("Tools/Export Tables")]
        private static void OpenWindow()
        {
            ExportTablesToolWindow.OpenWindow();
        }
    }

    public sealed class ExportTablesToolWindow : EditorWindow
    {
        private const string SelectedPathKey = "KTXGame.ExportTables.SelectedPath";
        private const string ExpandedPathsKey = "KTXGame.ExportTables.ExpandedPaths";
        private const float TreeIndent = 18f;

        private Vector2 scrollPosition;
        private string selectedAssetPath = string.Empty;
        private HashSet<string> expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string lastMessage = string.Empty;
        private MessageType lastMessageType = MessageType.Info;

        public static void OpenWindow()
        {
            var window = GetWindow<ExportTablesToolWindow>("导出表格");
            window.minSize = new Vector2(540f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadState();
            SyncSelectionFromProject(false);
            EnsureValidSelection();
        }

        private void OnFocus()
        {
            SyncSelectionFromProject(false);
            EnsureValidSelection();
        }

        private void OnSelectionChange()
        {
            if (SyncSelectionFromProject(false))
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Excel 导出工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("仅支持 Assets/Resources/Docs 下的文件夹和 .xlsx 文件，导出目录固定为 Assets/Resources/GameData/ExportedTables，并会生成总数据类文件到 Assets/Scripts/GameData/GameDataTables.cs。", MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("当前选择", selectedAssetPath);
                EditorGUILayout.LabelField("类型", GetSelectionTypeText());
                EditorGUILayout.LabelField("将扫描的 Excel 数量", CountSelectedExcelFiles().ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(lastMessage))
                {
                    EditorGUILayout.HelpBox(lastMessage, lastMessageType);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("选择导出目标", EditorStyles.boldLabel);
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollScope.scrollPosition;
                DrawNode(ExportTablesToolPaths.ExcelRootAbsolutePath, 0);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("导出", GUILayout.Width(140f), GUILayout.Height(32f)))
                {
                    ExecuteExport();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawNode(string absolutePath, int depth)
        {
            string assetPath = NormalizeAssetPath(ExportTablesToolPaths.ToAssetPath(absolutePath));
            bool isDirectory = Directory.Exists(absolutePath);
            bool isFile = File.Exists(absolutePath) && string.Equals(Path.GetExtension(absolutePath), ".xlsx", StringComparison.OrdinalIgnoreCase);
            if (!isDirectory && !isFile)
            {
                return;
            }

            if (IsOutputFolder(assetPath))
            {
                return;
            }

            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            rowRect.xMin += depth * TreeIndent;

            if (isDirectory)
            {
                bool isExpanded = expandedPaths.Contains(assetPath);
                Rect foldoutRect = new Rect(rowRect.x, rowRect.y, 16f, rowRect.height);
                bool nextExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none);
                if (nextExpanded != isExpanded)
                {
                    SetExpanded(assetPath, nextExpanded);
                }

                Rect labelRect = new Rect(rowRect.x + 16f, rowRect.y, rowRect.width - 16f, rowRect.height);
                DrawSelectableLabel(labelRect, assetPath, "文件夹  " + Path.GetFileName(absolutePath));

                if (expandedPaths.Contains(assetPath))
                {
                    foreach (string childDirectory in Directory.GetDirectories(absolutePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        DrawNode(childDirectory, depth + 1);
                    }

                    foreach (string childFile in Directory.GetFiles(absolutePath, "*.xlsx").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        DrawNode(childFile, depth + 1);
                    }
                }
            }
            else
            {
                DrawSelectableLabel(rowRect, assetPath, "Excel  " + Path.GetFileName(absolutePath));
            }
        }

        private void DrawSelectableLabel(Rect rect, string assetPath, string label)
        {
            bool isSelected = string.Equals(selectedAssetPath, assetPath, StringComparison.OrdinalIgnoreCase);
            if (Event.current.type == EventType.Repaint && isSelected)
            {
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.90f, 0.28f));
            }

            if (GUI.Button(rect, label, EditorStyles.label))
            {
                SelectPath(assetPath);
            }
        }

        private void ExecuteExport()
        {
            EnsureValidSelection();

            try
            {
                ExportResult result = ExportTablesToolCore.Export(new ExportRequest
                {
                    ExcelRootPath = ExportTablesToolPaths.ExcelRootAbsolutePath,
                    OutputRootPath = ExportTablesToolPaths.OutputAbsolutePath,
                    GeneratedCodeRootPath = ExportTablesToolPaths.GeneratedCodeAbsolutePath,
                    ScopePath = ExportTablesToolPaths.ToAbsolutePath(selectedAssetPath)
                });

                AssetDatabase.Refresh();
                lastMessageType = MessageType.Info;
                lastMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "导出完成。写入 {0} 个文件，删除 {1} 个文件，跳过 {2} 个 Sheet。",
                    result.WrittenFiles.Count,
                    result.DeletedFiles.Count,
                    result.SkippedSheets.Count);
                Debug.Log(string.Join(Environment.NewLine, result.Logs.ToArray()));
            }
            catch (Exception exception)
            {
                lastMessageType = MessageType.Error;
                lastMessage = BuildExportErrorMessage(exception);
                Debug.LogError(exception);
            }
        }

        private static string BuildExportErrorMessage(Exception exception)
        {
            if (exception == null)
            {
                return "导出失败：发生未知错误。";
            }

            var messages = new List<string>();
            Exception current = exception;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) &&
                    (messages.Count == 0 || !string.Equals(messages[messages.Count - 1], current.Message, StringComparison.Ordinal)))
                {
                    messages.Add(current.Message.Trim());
                }

                current = current.InnerException;
            }

            return messages.Count == 0
                ? "导出失败：发生未知错误。"
                : "导出失败：" + string.Join(" | ", messages.ToArray());
        }

        private bool SyncSelectionFromProject(bool showFallbackMessage)
        {
            string projectSelection = NormalizeAssetPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            if (!IsSelectableAssetPath(projectSelection))
            {
                if (showFallbackMessage)
                {
                    lastMessageType = MessageType.Warning;
                    lastMessage = "当前选中内容不在 Assets/Resources/Docs 下，已回退到上一次导出目标。";
                }

                return false;
            }

            SelectPath(projectSelection);
            return true;
        }

        private void EnsureValidSelection()
        {
            if (IsSelectableAssetPath(selectedAssetPath))
            {
                return;
            }

            string savedPath = NormalizeAssetPath(EditorPrefs.GetString(SelectedPathKey, string.Empty));
            if (IsSelectableAssetPath(savedPath))
            {
                selectedAssetPath = savedPath;
                ExpandToSelection(savedPath);
                return;
            }

            selectedAssetPath = ExportTablesToolPaths.ExcelRootRelativePath;
            ExpandToSelection(selectedAssetPath);
            SaveSelectedPath();
        }

        private bool IsSelectableAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string normalizedPath = NormalizeAssetPath(assetPath);
            if (!normalizedPath.StartsWith(ExportTablesToolPaths.ExcelRootRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsOutputFolder(normalizedPath))
            {
                return false;
            }

            string absolutePath = ExportTablesToolPaths.ToAbsolutePath(normalizedPath);
            return Directory.Exists(absolutePath)
                || (File.Exists(absolutePath) && string.Equals(Path.GetExtension(absolutePath), ".xlsx", StringComparison.OrdinalIgnoreCase));
        }

        private void SelectPath(string assetPath)
        {
            selectedAssetPath = NormalizeAssetPath(assetPath);
            ExpandToSelection(selectedAssetPath);
            SaveSelectedPath();
            lastMessage = string.Empty;
        }

        private void ExpandToSelection(string assetPath)
        {
            string currentPath = NormalizeAssetPath(assetPath);
            while (!string.IsNullOrEmpty(currentPath))
            {
                string absolutePath = ExportTablesToolPaths.ToAbsolutePath(currentPath);
                string directoryPath = File.Exists(absolutePath) ? NormalizeAssetPath(Path.GetDirectoryName(currentPath)) : currentPath;
                if (string.IsNullOrEmpty(directoryPath))
                {
                    break;
                }

                expandedPaths.Add(directoryPath);
                if (string.Equals(directoryPath, ExportTablesToolPaths.ExcelRootRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                currentPath = NormalizeAssetPath(Path.GetDirectoryName(directoryPath));
            }

            SaveExpandedPaths();
        }

        private int CountSelectedExcelFiles()
        {
            string absolutePath = ExportTablesToolPaths.ToAbsolutePath(selectedAssetPath);
            if (File.Exists(absolutePath))
            {
                return 1;
            }

            if (!Directory.Exists(absolutePath))
            {
                return 0;
            }

            return Directory.GetFiles(absolutePath, "*.xlsx", SearchOption.AllDirectories).Length;
        }

        private string GetSelectionTypeText()
        {
            string absolutePath = ExportTablesToolPaths.ToAbsolutePath(selectedAssetPath);
            return File.Exists(absolutePath) ? "单个 Excel 文件" : "文件夹";
        }

        private void LoadState()
        {
            selectedAssetPath = NormalizeAssetPath(EditorPrefs.GetString(SelectedPathKey, ExportTablesToolPaths.ExcelRootRelativePath));
            string expanded = EditorPrefs.GetString(ExpandedPathsKey, string.Empty);
            expandedPaths = new HashSet<string>(
                expanded.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(NormalizeAssetPath),
                StringComparer.OrdinalIgnoreCase);
        }

        private void SaveSelectedPath()
        {
            EditorPrefs.SetString(SelectedPathKey, selectedAssetPath);
        }

        private void SaveExpandedPaths()
        {
            EditorPrefs.SetString(ExpandedPathsKey, string.Join("|", expandedPaths.ToArray()));
        }

        private void SetExpanded(string assetPath, bool expanded)
        {
            if (expanded)
            {
                expandedPaths.Add(assetPath);
            }
            else
            {
                expandedPaths.Remove(assetPath);
            }

            SaveExpandedPaths();
        }

        private static bool IsOutputFolder(string assetPath)
        {
            return string.Equals(NormalizeAssetPath(assetPath), NormalizeAssetPath(ExportTablesToolPaths.OutputRelativePath), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return (assetPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }
    }
#endif

}
