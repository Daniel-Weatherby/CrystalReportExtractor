// ============================================================================
// File: BatchProcessor.cs
// Purpose:
//   Processes every Crystal report in an approved local input folder and writes
//   one metadata JSON file per report into a corresponding output structure.
//
// Key behaviour:
//   - Preserves input subdirectory structure.
//   - Continues when an individual report fails.
//   - Writes JSON atomically through a same-directory temporary file.
//   - Produces a run summary suitable for audit and retry planning.
//   - Never includes detailed SDK exception text in output files.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CrystalReportExtractor.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CrystalReportExtractor.Desktop
{
    internal sealed class BatchProcessor
    {
        private readonly ICrystalReportExtractor _extractor;
        private readonly JsonSerializerSettings _jsonSettings;

        public BatchProcessor()
        {
            _extractor = new CrystalReportExtractor.Core.Services
                .CrystalReportExtractor();

            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public BatchRunSummary Run(
            BatchOptions options,
            IProgress<BatchProgress> progress,
            CancellationToken cancellationToken)
        {
            ValidateOptions(options);

            string inputRoot = NormaliseDirectory(options.InputDirectory);
            string outputRoot = Path.GetFullPath(options.OutputDirectory);
            Directory.CreateDirectory(outputRoot);

            SearchOption searchOption = options.IncludeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            List<string> reportPaths = Directory
                .EnumerateFiles(inputRoot, "*.rpt", searchOption)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var summary = new BatchRunSummary
            {
                StartedAtUtc = DateTime.UtcNow,
                InputDirectory = inputRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                OutputDirectory = outputRoot,
                ReportsFound = reportPaths.Count
            };

            for (int index = 0; index < reportPaths.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    summary.Cancelled = true;
                    break;
                }

                string reportPath = reportPaths[index];
                string relativeSourcePath = reportPath.Substring(inputRoot.Length);
                string relativeOutputPath =
                    Path.ChangeExtension(relativeSourcePath, ".metadata.json");
                string outputPath = Path.Combine(outputRoot, relativeOutputPath);

                BatchReportResult result = ProcessReport(
                    reportPath,
                    relativeSourcePath,
                    outputPath,
                    relativeOutputPath,
                    options.OverwriteExisting);

                summary.Results.Add(result);

                switch (result.Status)
                {
                    case "Succeeded": summary.Succeeded++; break;
                    case "Skipped": summary.Skipped++; break;
                    default: summary.Failed++; break;
                }

                progress?.Report(
                    new BatchProgress
                    {
                        Completed = index + 1,
                        Total = reportPaths.Count,
                        RelativeSourcePath = relativeSourcePath,
                        Status = result.Status
                    });
            }

            summary.CompletedAtUtc = DateTime.UtcNow;
            WriteJsonAtomically(
                Path.Combine(outputRoot, "extraction-run-summary.json"),
                JsonConvert.SerializeObject(summary, _jsonSettings),
                overwriteExisting: true);

            return summary;
        }

        private BatchReportResult ProcessReport(
            string reportPath,
            string relativeSourcePath,
            string outputPath,
            string relativeOutputPath,
            bool overwriteExisting)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new BatchReportResult
            {
                SourceRelativePath = relativeSourcePath,
                OutputRelativePath = relativeOutputPath
            };

            try
            {
                if (File.Exists(outputPath) && !overwriteExisting)
                {
                    result.Status = "Skipped";
                    return result;
                }

                var fileInfo = new FileInfo(reportPath);
                var metadata = _extractor.Extract(
                    reportPath,
                    fileInfo.Name,
                    fileInfo.Length);

                string json = JsonConvert.SerializeObject(
                    metadata,
                    _jsonSettings);

                WriteJsonAtomically(outputPath, json, overwriteExisting);

                result.Status = "Succeeded";
                result.WarningCount = metadata.ExtractionWarnings.Count;
            }
            catch (Exception exception)
            {
                Trace.TraceError(
                    "Batch extraction failed for '{0}': {1}",
                    relativeSourcePath,
                    exception);

                result.Status = "Failed";
                result.ErrorType = exception.GetType().Name;
                result.ErrorMessage =
                    "The report could not be read by the Crystal Reports runtime.";
            }
            finally
            {
                stopwatch.Stop();
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        private static void WriteJsonAtomically(
            string outputPath,
            string json,
            bool overwriteExisting)
        {
            string outputDirectory = Path.GetDirectoryName(outputPath);
            Directory.CreateDirectory(outputDirectory);

            string temporaryPath = outputPath
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (File.Exists(outputPath))
                {
                    if (!overwriteExisting)
                    {
                        return;
                    }

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

        private static string NormaliseDirectory(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }

        private static void ValidateOptions(BatchOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.InputDirectory) ||
                !Directory.Exists(options.InputDirectory))
            {
                throw new DirectoryNotFoundException(
                    "Select an existing input directory.");
            }

            if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            {
                throw new ArgumentException(
                    "Select an output directory.",
                    nameof(options));
            }

            string input = Path.GetFullPath(options.InputDirectory)
                .TrimEnd(Path.DirectorySeparatorChar);
            string output = Path.GetFullPath(options.OutputDirectory)
                .TrimEnd(Path.DirectorySeparatorChar);

            if (string.Equals(
                input,
                output,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The input and output directories must be different.");
            }
        }
    }
}
