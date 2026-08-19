// ============================================================================
// File: ReportMetadata.cs
// Purpose:
//   Defines the stable, serialisable metadata produced from a Crystal Reports
//   .rpt file.
//
// Role in the architecture:
//   Represents the JSON boundary between Crystal SDK extraction and downstream
//   consumers such as SharePoint and future AI-based analysis.
//
// Key behaviour:
//   - Stores report identity and extraction information.
//   - Preserves Crystal formula names and their original expressions.
//   - Provides an explicit location for warnings and SDK limitations.
//   - Includes data sources, parameters, selection logic, grouping, sorting,
//     specialised fields and subreports.
//   - Records the report's referenced database objects and aliases.
//   - Preserves custom SQL from embedded Crystal Command objects.
//   - Records report parameters and accessible stored default values.
//   - Contains extracted first-level subreports and parent-child links.
//
// Dependencies:
//   - Standard .NET collection and date/time types only.
//
// Important considerations:
//   - Crystal SDK objects must never be exposed directly through this model.
//   - Database credentials and other secrets must never be included.
//   - Property names form part of the downstream JSON contract.
//   - Formula expressions are preserved verbatim and are not interpreted or
//     translated into plain English by this application.
//
// Maintenance notes:
//   Add deliberate domain properties as extraction capabilities grow. Avoid
//   generic object dictionaries that would make the JSON schema unstable.
// ============================================================================

using System;
using System.Collections.Generic;

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents the complete metadata extraction result for one Crystal
    /// Reports file.
    /// </summary>
    public class ReportMetadata
    {
        /// <summary>
        /// Gets or sets information identifying the source report.
        /// </summary>
        public ReportIdentityMetadata Report { get; set; }
            = new ReportIdentityMetadata();

        /// <summary>
        /// Gets or sets the UTC time at which extraction began.
        /// </summary>
        public DateTime ExtractedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the version of the Crystal Reports SDK assembly used
        /// by the extractor.
        /// </summary>
        public string CrystalSdkVersion { get; set; }

        /// <summary>
        /// Gets the database objects referenced by the report.
        /// </summary>
        public List<TableMetadata> Tables { get; set; }
            = new List<TableMetadata>();

        /// <summary>Gets sanitised database endpoints used by the report.</summary>
        public List<DataSourceMetadata> DataSources { get; set; }
            = new List<DataSourceMetadata>();

        /// <summary>Gets links or joins between report data objects.</summary>
        public List<RelationshipMetadata> Relationships { get; set; }
            = new List<RelationshipMetadata>();

        /// <summary>Gets fields referenced by definitions or report objects.</summary>
        public List<FieldUsageMetadata> Fields { get; set; }
            = new List<FieldUsageMetadata>();

        /// <summary>
        /// Gets formula fields defined in the report.
        /// </summary>
        public List<FormulaMetadata> Formulas { get; set; }
            = new List<FormulaMetadata>();

        /// <summary>
        /// Gets the custom SQL Command objects embedded in the report.
        /// </summary>
        public List<SqlCommandMetadata> SqlCommands { get; set; }
            = new List<SqlCommandMetadata>();

        /// <summary>
        /// Gets the parameters declared by the report.
        /// </summary>
        public List<ParameterMetadata> Parameters { get; set; }
            = new List<ParameterMetadata>();

        /// <summary>
        /// Gets running-total fields defined in the report.
        /// </summary>
        public List<RunningTotalMetadata> RunningTotals { get; set; }
            = new List<RunningTotalMetadata>();

        /// <summary>Gets ordinary summary fields defined by the report.</summary>
        public List<SummaryMetadata> Summaries { get; set; }
            = new List<SummaryMetadata>();

        /// <summary>
        /// Gets generated group-name fields used by the report.
        /// </summary>
        public List<GroupNameFieldMetadata> GroupNameFields { get; set; }
            = new List<GroupNameFieldMetadata>();

        /// <summary>
        /// Gets built-in Crystal special fields used by the report.
        /// </summary>
        public List<SpecialFieldMetadata> SpecialFields { get; set; }
            = new List<SpecialFieldMetadata>();

        /// <summary>
        /// Gets the record-level and group-level report selection formulas.
        /// </summary>
        public SelectionLogicMetadata SelectionLogic { get; set; }
            = new SelectionLogicMetadata();

        /// <summary>
        /// Gets the explicit grouping levels declared by the report.
        /// </summary>
        public List<GroupMetadata> Groups { get; set; }
            = new List<GroupMetadata>();

        /// <summary>
        /// Gets record-sort and group-sort definitions in priority order.
        /// </summary>
        public List<SortMetadata> Sorts { get; set; }
            = new List<SortMetadata>();

        /// <summary>Gets sections, report objects and formatting behaviour.</summary>
        public List<SectionMetadata> Sections { get; set; }
            = new List<SectionMetadata>();
        
        /// <summary>
        /// Gets first-level subreports extracted from this report.
        /// </summary>
        public List<SubreportMetadata> Subreports { get; set; }
            = new List<SubreportMetadata>();

        /// <summary>
        /// Gets warnings describing inaccessible metadata, partial extraction
        /// or other limitations that downstream consumers should understand.
        /// </summary>
        public List<string> ExtractionWarnings { get; set; }
            = new List<string>();
    }

    /// <summary>
    /// Contains safe general information identifying the extracted report.
    /// </summary>
    public class ReportIdentityMetadata
    {
        /// <summary>
        /// Gets or sets the sanitised name of the uploaded source file.
        /// </summary>
        public string SourceFileName { get; set; }

        /// <summary>
        /// Gets or sets the report title exposed by the Crystal SDK, falling
        /// back to the source filename when no title is defined.
        /// </summary>
        public string ReportName { get; set; }

        /// <summary>
        /// Gets or sets the size of the uploaded report file in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>Gets or sets the report author from Summary Info.</summary>
        public string Author { get; set; }

        /// <summary>Gets or sets the report subject from Summary Info.</summary>
        public string Subject { get; set; }

        /// <summary>Gets or sets report comments from Summary Info.</summary>
        public string Comments { get; set; }

        /// <summary>Gets or sets report keywords from Summary Info.</summary>
        public string Keywords { get; set; }

        /// <summary>Gets or sets whether saved report data is present.</summary>
        public bool? HasSavedData { get; set; }
    }

    /// <summary>
    /// Represents a Crystal Reports formula field and its original expression.
    /// </summary>
    public class FormulaMetadata
    {
        /// <summary>
        /// Gets or sets the formula name as defined in Crystal Reports.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the original Crystal formula expression without
        /// interpretation or conversion.
        /// </summary>
        public string Expression { get; set; }
    }
}
