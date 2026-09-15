// ============================================================================
// File: CsvInventoryWriter.cs
// Purpose:
//   Writes one flattened portfolio row per analysed Crystal report.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrystalReportExtractor.Core.Models.Analysis;
using CrystalReportExtractor.Core.Models.Extraction;

namespace CrystalReportExtractor.Desktop
{
    internal static class CsvInventoryWriter
    {
        public static void Write(
            string outputPath,
            IEnumerable<BatchReportResult> results)
        {
            var csv = new StringBuilder();

            csv.AppendLine(
                "SourceRelativePath,ReportName,FormulaCount,ParameterCount," +
                "TableCount,RelationshipCount,SqlCommandCount," +
                "RunningTotalCount,SummaryCount,GroupCount,SortCount," +
                "SelectionFormulaCount,ConditionalFormulaCount,SectionCount," +
                "ReportObjectCount,SubreportCount,ExtractionWarningCount," +
                "ComplexityScore,ComplexityBand,ScoringModelVersion");

            foreach (BatchReportResult result in results)
            {
                ReportMetadata metadata = result.Metadata;
                ReportAnalysis analysis = metadata == null
                    ? null
                    : metadata.Analysis;

                if (analysis == null)
                {
                    continue;
                }

                ReportMetricCounts metrics = analysis.TotalMetrics;

                csv.Append(Escape(result.SourceRelativePath)).Append(',');
                csv.Append(Escape(
                    metadata.Report == null
                        ? null
                        : metadata.Report.ReportName)).Append(',');
                csv.Append(metrics.FormulaCount).Append(',');
                csv.Append(metrics.ParameterCount).Append(',');
                csv.Append(metrics.TableCount).Append(',');
                csv.Append(metrics.RelationshipCount).Append(',');
                csv.Append(metrics.SqlCommandCount).Append(',');
                csv.Append(metrics.RunningTotalCount).Append(',');
                csv.Append(metrics.SummaryCount).Append(',');
                csv.Append(metrics.GroupCount).Append(',');
                csv.Append(metrics.SortCount).Append(',');
                csv.Append(metrics.SelectionFormulaCount).Append(',');
                csv.Append(metrics.ConditionalFormulaCount).Append(',');
                csv.Append(metrics.SectionCount).Append(',');
                csv.Append(metrics.ReportObjectCount).Append(',');
                csv.Append(metrics.SubreportCount).Append(',');
                csv.Append(metrics.ExtractionWarningCount).Append(',');
                csv.Append(analysis.ComplexityScore).Append(',');
                csv.Append(Escape(analysis.ComplexityBand)).Append(',');
                csv.Append(Escape(analysis.ScoringModelVersion));
                csv.AppendLine();
            }

            WriteAtomically(outputPath, csv.ToString());
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
