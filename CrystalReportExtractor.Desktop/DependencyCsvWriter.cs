// ============================================================================
// File: DependencyCsvWriter.cs
// Purpose:
//   Writes one row per extracted report/database dependency, including
//   dependencies contained in subreports.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrystalReportExtractor.Core.Models.Extraction;

namespace CrystalReportExtractor.Desktop
{
    internal static class DependencyCsvWriter
    {
        public static void Write(
            string outputPath,
            IEnumerable<BatchReportResult> results)
        {
            var csv = new StringBuilder();

            csv.AppendLine(
                "SourceRelativePath,ReportName,ReportContext,IsSubreport," +
                "DependencyKind,Name,Alias,Location,CrystalObjectType," +
                "ProviderObjectType,HasEmbeddedSql");

            foreach (BatchReportResult result in results)
            {
                if (result.Metadata == null)
                {
                    continue;
                }

                string reportName = GetReportName(result.Metadata);

                AppendReportDependencies(
                    csv,
                    result.SourceRelativePath,
                    reportName,
                    "Main report",
                    false,
                    result.Metadata);
            }

            WriteAtomically(outputPath, csv.ToString());
        }

        private static void AppendReportDependencies(
            StringBuilder csv,
            string sourceRelativePath,
            string reportName,
            string reportContext,
            bool isSubreport,
            ReportMetadata metadata)
        {
            var representedCommands =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (metadata.Tables != null)
            {
                foreach (TableMetadata table in metadata.Tables)
                {
                    if (table == null)
                    {
                        continue;
                    }

                    string dependencyKind = ClassifyTable(table);
                    bool isCommand = string.Equals(
                        dependencyKind,
                        "SqlCommand",
                        StringComparison.OrdinalIgnoreCase);

                    if (isCommand)
                    {
                        AddKey(representedCommands, table.Name);
                        AddKey(representedCommands, table.Alias);
                    }

                    AppendRow(
                        csv,
                        sourceRelativePath,
                        reportName,
                        reportContext,
                        isSubreport,
                        dependencyKind,
                        table.Name,
                        table.Alias,
                        table.Location,
                        table.ObjectType,
                        table.ProviderObjectType,
                        isCommand && HasMatchingSql(
                            metadata.SqlCommands,
                            table.Name,
                            table.Alias));
                }
            }

            if (metadata.SqlCommands != null)
            {
                foreach (SqlCommandMetadata command in metadata.SqlCommands)
                {
                    if (command == null ||
                        ContainsKey(
                            representedCommands,
                            command.Name,
                            command.Alias))
                    {
                        continue;
                    }

                    AppendRow(
                        csv,
                        sourceRelativePath,
                        reportName,
                        reportContext,
                        isSubreport,
                        "SqlCommand",
                        command.Name,
                        command.Alias,
                        null,
                        "Command",
                        null,
                        !string.IsNullOrWhiteSpace(command.Sql));
                }
            }

            if (metadata.Subreports == null)
            {
                return;
            }

            foreach (SubreportMetadata subreport in metadata.Subreports)
            {
                if (subreport == null || subreport.Definition == null)
                {
                    continue;
                }

                string subreportName = string.IsNullOrWhiteSpace(subreport.Name)
                    ? GetReportName(subreport.Definition)
                    : subreport.Name;

                string childContext =
                    reportContext + " > " + subreportName;

                AppendReportDependencies(
                    csv,
                    sourceRelativePath,
                    reportName,
                    childContext,
                    true,
                    subreport.Definition);
            }
        }

        private static bool HasMatchingSql(
            IEnumerable<SqlCommandMetadata> commands,
            string name,
            string alias)
        {
            if (commands == null)
            {
                return false;
            }

            foreach (SqlCommandMetadata command in commands)
            {
                if (command != null &&
                    (ValuesMatch(command.Name, name) ||
                     ValuesMatch(command.Name, alias) ||
                     ValuesMatch(command.Alias, name) ||
                     ValuesMatch(command.Alias, alias)))
                {
                    return !string.IsNullOrWhiteSpace(command.Sql);
                }
            }

            return false;
        }

        private static void AddKey(
            ISet<string> keys,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                keys.Add(value.Trim());
            }
        }

        private static bool ContainsKey(
            ISet<string> keys,
            string name,
            string alias)
        {
            return
                (!string.IsNullOrWhiteSpace(name) &&
                 keys.Contains(name.Trim())) ||
                (!string.IsNullOrWhiteSpace(alias) &&
                 keys.Contains(alias.Trim()));
        }

        private static bool ValuesMatch(string left, string right)
        {
            return
                !string.IsNullOrWhiteSpace(left) &&
                !string.IsNullOrWhiteSpace(right) &&
                string.Equals(
                    left.Trim(),
                    right.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string ClassifyTable(TableMetadata table)
        {
            string classification =
                (table.ObjectType ?? string.Empty) + " " +
                (table.ProviderObjectType ?? string.Empty);

            if (classification.IndexOf(
                "command",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "SqlCommand";
            }

            if (classification.IndexOf(
                    "procedure",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                classification.IndexOf(
                    "stored proc",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "StoredProcedure";
            }

            if (classification.IndexOf(
                "view",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "View";
            }

            if (classification.IndexOf(
                "table",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Table";
            }

            return "DatabaseObject";
        }

        private static string GetReportName(ReportMetadata metadata)
        {
            if (metadata.Report == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(metadata.Report.ReportName)
                ? metadata.Report.SourceFileName
                : metadata.Report.ReportName;
        }

        private static void AppendRow(
            StringBuilder csv,
            string sourceRelativePath,
            string reportName,
            string reportContext,
            bool isSubreport,
            string dependencyKind,
            string name,
            string alias,
            string location,
            string crystalObjectType,
            string providerObjectType,
            bool hasEmbeddedSql)
        {
            csv.Append(Escape(sourceRelativePath)).Append(',');
            csv.Append(Escape(reportName)).Append(',');
            csv.Append(Escape(reportContext)).Append(',');
            csv.Append(isSubreport ? "true" : "false").Append(',');
            csv.Append(Escape(dependencyKind)).Append(',');
            csv.Append(Escape(name)).Append(',');
            csv.Append(Escape(alias)).Append(',');
            csv.Append(Escape(location)).Append(',');
            csv.Append(Escape(crystalObjectType)).Append(',');
            csv.Append(Escape(providerObjectType)).Append(',');
            csv.Append(hasEmbeddedSql ? "true" : "false");
            csv.AppendLine();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void WriteAtomically(string outputPath, string content)
        {
            string temporaryPath = outputPath
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    content,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (File.Exists(outputPath))
                {
                    File.Replace(temporaryPath, outputPath, null);
                }
                else
                {
                    File.Move(temporaryPath, outputPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
