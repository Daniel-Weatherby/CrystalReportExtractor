// ============================================================================
// File: ReportAnalyser.cs
// Purpose:
//   Calculates reproducible structural metrics and a transparent migration
//   complexity heuristic from the stable ReportMetadata model.
// ============================================================================

using System;
using CrystalReportExtractor.Core.Models.Analysis;
using CrystalReportExtractor.Core.Models.Extraction;

namespace CrystalReportExtractor.Core.Analysis
{
    public sealed class ReportAnalyser
    {
        public ReportAnalysis Analyse(ReportMetadata report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            ReportMetricCounts direct = CountDirect(report);
            ReportMetricCounts total = CountTree(report);

            int score =
                total.FormulaCount +
                total.ParameterCount +
                total.TableCount +
                total.RelationshipCount +
                (total.SqlCommandCount * 5) +
                (total.RunningTotalCount * 3) +
                (total.SummaryCount * 2) +
                (total.GroupCount * 2) +
                total.SortCount +
                (total.SelectionFormulaCount * 2) +
                (total.ConditionalFormulaCount * 2) +
                (total.SubreportCount * 5);

            return new ReportAnalysis
            {
                DirectMetrics = direct,
                TotalMetrics = total,
                ComplexityScore = score,
                ComplexityBand = GetComplexityBand(score),
                ScoringModelVersion = "1.0"
            };
        }

        private static ReportMetricCounts CountTree(ReportMetadata report)
        {
            ReportMetricCounts total = CountDirect(report);

            if (report.Subreports == null)
            {
                return total;
            }

            foreach (SubreportMetadata subreport in report.Subreports)
            {
                if (subreport != null && subreport.Definition != null)
                {
                    Add(total, CountTree(subreport.Definition));
                }
            }

            return total;
        }

        private static ReportMetricCounts CountDirect(ReportMetadata report)
        {
            int conditionalFormulaCount = 0;
            int reportObjectCount = 0;

            if (report.Sections != null)
            {
                foreach (SectionMetadata section in report.Sections)
                {
                    if (section == null)
                    {
                        continue;
                    }

                    conditionalFormulaCount += Count(section.ConditionalFormulas);
                    reportObjectCount += Count(section.ReportObjects);

                    if (section.ReportObjects == null)
                    {
                        continue;
                    }

                    foreach (ReportObjectMetadata reportObject in section.ReportObjects)
                    {
                        if (reportObject != null)
                        {
                            conditionalFormulaCount +=
                                Count(reportObject.ConditionalFormulas);
                        }
                    }
                }
            }

            int selectionFormulaCount = 0;
            if (report.SelectionLogic != null)
            {
                if (!string.IsNullOrWhiteSpace(
                    report.SelectionLogic.RecordSelectionFormula))
                {
                    selectionFormulaCount++;
                }

                if (!string.IsNullOrWhiteSpace(
                    report.SelectionLogic.GroupSelectionFormula))
                {
                    selectionFormulaCount++;
                }
            }

            return new ReportMetricCounts
            {
                FormulaCount = Count(report.Formulas),
                ParameterCount = Count(report.Parameters),
                TableCount = Count(report.Tables),
                RelationshipCount = Count(report.Relationships),
                SqlCommandCount = Count(report.SqlCommands),
                RunningTotalCount = Count(report.RunningTotals),
                SummaryCount = Count(report.Summaries),
                GroupCount = Count(report.Groups),
                SortCount = Count(report.Sorts),
                SelectionFormulaCount = selectionFormulaCount,
                ConditionalFormulaCount = conditionalFormulaCount,
                SectionCount = Count(report.Sections),
                ReportObjectCount = reportObjectCount,
                SubreportCount = Count(report.Subreports),
                ExtractionWarningCount = Count(report.ExtractionWarnings)
            };
        }

        private static int Count<T>(System.Collections.Generic.ICollection<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        private static void Add(
            ReportMetricCounts target,
            ReportMetricCounts source)
        {
            target.FormulaCount += source.FormulaCount;
            target.ParameterCount += source.ParameterCount;
            target.TableCount += source.TableCount;
            target.RelationshipCount += source.RelationshipCount;
            target.SqlCommandCount += source.SqlCommandCount;
            target.RunningTotalCount += source.RunningTotalCount;
            target.SummaryCount += source.SummaryCount;
            target.GroupCount += source.GroupCount;
            target.SortCount += source.SortCount;
            target.SelectionFormulaCount += source.SelectionFormulaCount;
            target.ConditionalFormulaCount += source.ConditionalFormulaCount;
            target.SectionCount += source.SectionCount;
            target.ReportObjectCount += source.ReportObjectCount;
            target.SubreportCount += source.SubreportCount;
            target.ExtractionWarningCount += source.ExtractionWarningCount;
        }

        private static string GetComplexityBand(int score)
        {
            if (score <= 10)
            {
                return "Low";
            }

            if (score <= 30)
            {
                return "Medium";
            }

            return "High";
        }
    }
}
