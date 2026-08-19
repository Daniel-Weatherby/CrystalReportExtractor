// ============================================================================
// File: SpecialisedFieldMetadata.cs
// Purpose:
//   Defines JSON models for Crystal running totals, group name fields and
//   built-in special fields.
//
// Role in the architecture:
//   Preserves calculated and contextual fields that are not ordinary database
//   or formula fields.
//
// Key behaviour:
//   - Describes running-total calculations and reset behaviour.
//   - Records generated group-name fields.
//   - Records built-in Crystal special fields.
//
// Dependencies:
//   - Standard .NET types only.
//
// Important considerations:
//   - Raw Crystal expressions and SDK classifications are preserved wherever
//     accessible.
//   - These field types have different migration implications and therefore
//     remain separate in the JSON schema.
//
// Maintenance notes:
//   Extend these models deliberately as additional SDK properties are proven
//   accessible through real report testing.
// ============================================================================

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents a running-total field defined in a Crystal report.
    /// </summary>
    public class RunningTotalMetadata
    {
        /// <summary>Gets or sets the running-total name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the field being accumulated.</summary>
        public string SummarizedField { get; set; }

        /// <summary>Gets or sets Crystal's summary operation.</summary>
        public string SummaryOperation { get; set; }

        /// <summary>Gets or sets the evaluation condition.</summary>
        public string EvaluateCondition { get; set; }

        /// <summary>
        /// Gets or sets the Crystal formula used when evaluation is formula
        /// based, or <c>null</c> when no formula is defined or exposed.
        /// </summary>
        public string EvaluateFormula { get; set; }

        /// <summary>Gets or sets the reset condition.</summary>
        public string ResetCondition { get; set; }

        /// <summary>
        /// Gets or sets the Crystal formula used when reset is formula based,
        /// or <c>null</c> when no formula is defined or exposed.
        /// </summary>
        public string ResetFormula { get; set; }
    }

    /// <summary>
    /// Represents a generated group-name field.
    /// </summary>
    public class GroupNameFieldMetadata
    {
        /// <summary>Gets or sets the field name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets Crystal's formula-form representation.</summary>
        public string Expression { get; set; }

        /// <summary>Gets or sets Crystal's value-type classification.</summary>
        public string ValueType { get; set; }
    }

    /// <summary>
    /// Represents a built-in Crystal special field.
    /// </summary>
    public class SpecialFieldMetadata
    {
        /// <summary>Gets or sets the special-field name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets Crystal's formula-form representation.</summary>
        public string Expression { get; set; }

        /// <summary>Gets or sets Crystal's value-type classification.</summary>
        public string ValueType { get; set; }
    }
}
