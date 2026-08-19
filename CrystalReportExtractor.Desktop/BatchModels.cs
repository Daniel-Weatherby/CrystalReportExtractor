// ============================================================================
// File: BatchModels.cs
// Purpose:
//   Defines desktop batch options, progress messages and the machine-readable
//   summary produced after each extraction run.
// ============================================================================

using System;
using System.Collections.Generic;

namespace CrystalReportExtractor.Desktop
{
    internal sealed class BatchOptions
    {
        public string InputDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public bool IncludeSubdirectories { get; set; }
        public bool OverwriteExisting { get; set; }
    }

    internal sealed class BatchProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
        public string RelativeSourcePath { get; set; }
        public string Status { get; set; }
    }

    internal sealed class BatchRunSummary
    {
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public string InputDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public int ReportsFound { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public bool Cancelled { get; set; }
        public List<BatchReportResult> Results { get; set; }
            = new List<BatchReportResult>();
    }

    internal sealed class BatchReportResult
    {
        public string SourceRelativePath { get; set; }
        public string OutputRelativePath { get; set; }
        public string Status { get; set; }
        public int WarningCount { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string ErrorType { get; set; }
        public string ErrorMessage { get; set; }
    }
}
