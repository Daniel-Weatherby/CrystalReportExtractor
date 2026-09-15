// ============================================================================
// File: ReportAnalysis.cs
// Purpose:
//   Defines deterministic structural analysis appended to extracted report
//   metadata. Raw extraction remains separate from derived analysis.
// ============================================================================

namespace CrystalReportExtractor.Core.Models.Analysis
{
    /// <summary>
    /// Contains objective counts and the derived complexity assessment for one
    /// report. TotalMetrics includes all recursively extracted subreports.
    /// </summary>
    public class ReportAnalysis
    {
        public ReportMetricCounts DirectMetrics { get; set; }
            = new ReportMetricCounts();

        public ReportMetricCounts TotalMetrics { get; set; }
            = new ReportMetricCounts();

        public int ComplexityScore { get; set; }

        public string ComplexityBand { get; set; }

        public string ScoringModelVersion { get; set; } = "1.0";
    }

    /// <summary>
    /// Objective structural counts. These remain available independently of
    /// the weighting model so the complexity conclusion can be challenged or
    /// recalculated later.
    /// </summary>
    public class ReportMetricCounts
    {
        public int FormulaCount { get; set; }
        public int ParameterCount { get; set; }
        public int TableCount { get; set; }
        public int RelationshipCount { get; set; }
        public int SqlCommandCount { get; set; }
        public int RunningTotalCount { get; set; }
        public int SummaryCount { get; set; }
        public int GroupCount { get; set; }
        public int SortCount { get; set; }
        public int SelectionFormulaCount { get; set; }
        public int ConditionalFormulaCount { get; set; }
        public int SectionCount { get; set; }
        public int ReportObjectCount { get; set; }
        public int SubreportCount { get; set; }
        public int ExtractionWarningCount { get; set; }
    }
}
