// ============================================================================
// File: StructuralMetadata.cs
// Purpose:
//   Defines stable JSON models for report selection logic, explicit grouping
//   and sort definitions.
//
// Role in the architecture:
//   Extends the metadata contract with report structure that materially affects
//   which records are included and how the output is organised.
//
// Important considerations:
//   Crystal expressions and SDK classifications are preserved without
//   interpretation. These values can disclose business logic and database
//   structure and must be protected like the source report.
// ============================================================================

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Contains the report's record-level and group-level selection formulas.
    /// </summary>
    public class SelectionLogicMetadata
    {
        /// <summary>Gets or sets the record selection formula.</summary>
        public string RecordSelectionFormula { get; set; }

        /// <summary>Gets or sets the group selection formula.</summary>
        public string GroupSelectionFormula { get; set; }
    }

    /// <summary>
    /// Represents one group level declared in a Crystal report.
    /// </summary>
    public class GroupMetadata
    {
        /// <summary>Gets or sets the zero-based group level.</summary>
        public int Level { get; set; }

        /// <summary>Gets or sets the field or formula used to form the group.</summary>
        public string ConditionField { get; set; }

        /// <summary>Gets or sets the Crystal field-kind classification.</summary>
        public string FieldKind { get; set; }

        /// <summary>Gets or sets the Crystal value-type classification.</summary>
        public string ValueType { get; set; }
    }

    /// <summary>
    /// Represents one record-sort or group-sort definition.
    /// </summary>
    public class SortMetadata
    {
        /// <summary>Gets or sets the zero-based sort priority.</summary>
        public int Priority { get; set; }

        /// <summary>Gets or sets the field or formula being sorted.</summary>
        public string Field { get; set; }

        /// <summary>Gets or sets Crystal's sort-field classification.</summary>
        public string SortType { get; set; }

        /// <summary>Gets or sets Crystal's sort-direction classification.</summary>
        public string Direction { get; set; }
    }
}
