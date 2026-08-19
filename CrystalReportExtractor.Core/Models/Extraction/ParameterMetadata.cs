// ============================================================================
// File: ParameterMetadata.cs
// Purpose:
//   Defines the stable JSON representation of parameters declared by a Crystal
//   report.
//
// Role in the architecture:
//   Preserves report inputs that influence SQL, selection logic, formulas,
//   presentation and subreport behaviour.
//
// Key behaviour:
//   - Records parameter identity, prompt text and Crystal value type.
//   - Records whether multiple values and null values are permitted.
//   - Preserves accessible default discrete and range values as text.
//
// Dependencies:
//   - Standard .NET collection types only.
//
// Important considerations:
//   - Parameter values are represented as invariant text to keep the JSON schema
//     stable across Crystal data types.
//   - Runtime user-supplied values are not extracted.
//   - Stored defaults may contain operationally sensitive values and should be
//     handled according to the same access controls as the source report.
//
// Maintenance notes:
//   Parent-to-subreport parameter links will be represented separately when
//   recursive subreport extraction is implemented.
// ============================================================================

using System.Collections.Generic;

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents a parameter declared by a Crystal report.
    /// </summary>
    public class ParameterMetadata
    {
        /// <summary>
        /// Gets or sets the parameter name used by formulas and selection logic.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the user-facing prompt text where available.
        /// </summary>
        public string PromptText { get; set; }

        /// <summary>
        /// Gets or sets Crystal's parameter value-type classification.
        /// </summary>
        public string ValueType { get; set; }

        /// <summary>
        /// Gets or sets whether the parameter accepts multiple values.
        /// </summary>
        public bool AllowsMultipleValues { get; set; }

        /// <summary>
        /// Gets or sets whether the parameter accepts a null value.
        /// </summary>
        public bool AllowsNullValue { get; set; }

        /// <summary>
        /// Gets accessible default values stored in the report.
        /// </summary>
        public List<ParameterValueMetadata> DefaultValues { get; set; }
            = new List<ParameterValueMetadata>();
    }

    /// <summary>
    /// Represents either a discrete parameter value or a bounded range.
    /// </summary>
    public class ParameterValueMetadata
    {
        /// <summary>
        /// Gets or sets either <c>Discrete</c> or <c>Range</c>.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets the invariant textual discrete value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the invariant textual lower range boundary.
        /// </summary>
        public string StartValue { get; set; }

        /// <summary>
        /// Gets or sets the invariant textual upper range boundary.
        /// </summary>
        public string EndValue { get; set; }

        /// <summary>
        /// Gets or sets Crystal's lower-bound inclusion classification.
        /// </summary>
        public string LowerBoundType { get; set; }

        /// <summary>
        /// Gets or sets Crystal's upper-bound inclusion classification.
        /// </summary>
        public string UpperBoundType { get; set; }
    }
}