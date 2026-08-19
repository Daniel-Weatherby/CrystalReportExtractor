// ============================================================================
// File: ICrystalReportExtractor.cs
// Purpose:
//   Defines the contract for converting a Crystal Reports .rpt file into the
//   application's stable metadata model.
//
// Role in the architecture:
//   Separates the MVC web layer from the Crystal SDK extraction implementation.
//
// Key behaviour:
//   - Accepts a trusted local temporary-file path.
//   - Returns serialisable report metadata.
//   - Allows the extraction implementation to evolve without coupling Crystal
//     SDK types to controllers or views.
//
// Dependencies:
//   - ReportMetadata domain model.
//
// Important considerations:
//   - The supplied path must refer to a file managed by the application.
//   - Implementations must close and dispose all Crystal report resources.
//   - Implementations must not expose database credentials in their results.
//
// Maintenance notes:
//   Keep Crystal SDK types out of this interface so callers remain independent
//   of SAP-specific implementation details.
// ============================================================================

using CrystalReportExtractor.Core.Models.Extraction;

namespace CrystalReportExtractor.Core.Services
{
    /// <summary>
    /// Extracts structured metadata from a local Crystal Reports file.
    /// </summary>
    public interface ICrystalReportExtractor
    {
        /// <summary>
        /// Loads a Crystal Reports file and extracts its available metadata.
        /// </summary>
        /// <param name="reportFilePath">
        /// Absolute path to the temporary server-side copy of the report.
        /// </param>
        /// <param name="sourceFileName">
        /// Sanitised original filename to record in the output.
        /// </param>
        /// <param name="fileSizeBytes">
        /// Size of the uploaded report file in bytes.
        /// </param>
        /// <returns>A stable metadata representation of the report.</returns>
        ReportMetadata Extract(
            string reportFilePath,
            string sourceFileName,
            long fileSizeBytes);
    }
}