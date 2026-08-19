// ============================================================================
// File: ExtendedMetadata.cs
// Purpose:
//   Defines stable JSON models for data sources, relationships, field usage,
//   summaries, sections, report objects and conditional formatting formulas.
//
// Role in the architecture:
//   Carries the remaining report-definition information from the Crystal SDK
//   extraction layer into the downstream analysis contract.
//
// Key behaviour:
//   - Describes connections without usernames, passwords or connection strings.
//   - Preserves database lineage and report field usage.
//   - Describes report structure and conditional behaviour without serialising
//     Crystal SDK objects directly.
//
// Important considerations:
//   Layout measurements use Crystal's native twip-like integer units. Text and
//   expressions may contain sensitive business information and must be handled
//   with the same controls as the source report.
// ============================================================================

using System.Collections.Generic;

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>Represents a sanitised database endpoint used by a report.</summary>
    public class DataSourceMetadata
    {
        /// <summary>Gets or sets the server or service name.</summary>
        public string ServerName { get; set; }

        /// <summary>Gets or sets the database or catalogue name.</summary>
        public string DatabaseName { get; set; }

        /// <summary>Gets or sets Crystal's connection or provider classification.</summary>
        public string ConnectionType { get; set; }
    }

    /// <summary>Represents a join or link between two report data objects.</summary>
    public class RelationshipMetadata
    {
        /// <summary>Gets or sets the source field expression.</summary>
        public string SourceField { get; set; }

        /// <summary>Gets or sets the destination field expression.</summary>
        public string DestinationField { get; set; }

        /// <summary>Gets or sets Crystal's join-type classification.</summary>
        public string JoinType { get; set; }

        /// <summary>Gets or sets Crystal's link-enforcement classification.</summary>
        public string Enforcement { get; set; }
    }

    /// <summary>Represents a field used by the report definition or layout.</summary>
    public class FieldUsageMetadata
    {
        /// <summary>Gets or sets the field name or Crystal formula form.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets Crystal's field-kind classification.</summary>
        public string FieldKind { get; set; }

        /// <summary>Gets or sets Crystal's value-type classification.</summary>
        public string ValueType { get; set; }

        /// <summary>Gets the locations or SDK collections in which it is used.</summary>
        public List<string> UsageContexts { get; set; }
            = new List<string>();
    }

    /// <summary>Represents a non-running summary field.</summary>
    public class SummaryMetadata
    {
        /// <summary>Gets or sets the summary field name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the field being summarised.</summary>
        public string SummarizedField { get; set; }

        /// <summary>Gets or sets Crystal's summary-operation classification.</summary>
        public string SummaryOperation { get; set; }

        /// <summary>
        /// Gets whether this summary is calculated as a percentage.
        /// </summary>
        public bool? IsPercentage { get; set; }

        /// <summary>Gets or sets the group on which the summary is scoped.</summary>
        public string Group { get; set; }
    }

    /// <summary>Represents a Crystal section and its contained report objects.</summary>
    public class SectionMetadata
    {
        /// <summary>Gets or sets the internal section name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the section-kind classification.</summary>
        public string Kind { get; set; }

        /// <summary>Gets or sets the section height in Crystal layout units.</summary>
        public int? Height { get; set; }

        /// <summary>Gets or sets whether the section is unconditionally suppressed.</summary>
        public bool? Suppressed { get; set; }

        /// <summary>Gets or sets whether Crystal attempts to keep the section together.</summary>
        public bool? KeepTogether { get; set; }

        /// <summary>Gets or sets whether a new page is forced before the section.</summary>
        public bool? NewPageBefore { get; set; }

        /// <summary>Gets or sets whether a new page is forced after the section.</summary>
        public bool? NewPageAfter { get; set; }

        /// <summary>Gets conditional section-format formulas.</summary>
        public List<ConditionalFormulaMetadata> ConditionalFormulas { get; set; }
            = new List<ConditionalFormulaMetadata>();

        /// <summary>Gets report objects placed in the section.</summary>
        public List<ReportObjectMetadata> ReportObjects { get; set; }
            = new List<ReportObjectMetadata>();
    }

    /// <summary>Represents a visual or data-bound object placed on a report.</summary>
    public class ReportObjectMetadata
    {
        /// <summary>Gets or sets the object name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets Crystal's report-object classification.</summary>
        public string Kind { get; set; }

        /// <summary>Gets or sets the top coordinate in Crystal layout units.</summary>
        public int? Top { get; set; }

        /// <summary>Gets or sets the left coordinate in Crystal layout units.</summary>
        public int? Left { get; set; }

        /// <summary>Gets or sets the object width in Crystal layout units.</summary>
        public int? Width { get; set; }

        /// <summary>Gets or sets the object height in Crystal layout units.</summary>
        public int? Height { get; set; }

        /// <summary>Gets or sets whether the object is unconditionally suppressed.</summary>
        public bool? Suppressed { get; set; }

        /// <summary>Gets or sets the bound field, formula or subreport name.</summary>
        public string DataSource { get; set; }

        /// <summary>Gets or sets literal text stored in a text object.</summary>
        public string Text { get; set; }

        /// <summary>Gets conditional object-format formulas.</summary>
        public List<ConditionalFormulaMetadata> ConditionalFormulas { get; set; }
            = new List<ConditionalFormulaMetadata>();
    }

    /// <summary>Represents one conditional formatting formula.</summary>
    public class ConditionalFormulaMetadata
    {
        /// <summary>Gets or sets the formatting property controlled by the formula.</summary>
        public string Property { get; set; }

        /// <summary>Gets or sets the raw Crystal expression.</summary>
        public string Expression { get; set; }
    }
}
