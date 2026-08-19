// ============================================================================
// File: SqlCommandMetadata.cs
// Purpose:
//   Defines the stable JSON representation of a SQL Command object embedded
//   inside a Crystal report.
//
// Role in the architecture:
//   Preserves custom SQL used by reports so downstream lineage and migration
//   analysis can identify database dependencies and hidden query logic.
//
// Key behaviour:
//   - Records the Command object's name and report alias.
//   - Preserves its original SQL statement without interpretation.
//
// Dependencies:
//   - Standard .NET types only.
//
// Important considerations:
//   - SQL text may reveal database structure but must never include credentials.
//   - The extractor preserves SQL exactly as exposed by Crystal.
//   - SQL is not executed by the extractor.
//
// Maintenance notes:
//   Command parameters will be linked to the report parameter model when that
//   extraction capability is added.
// ============================================================================

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents custom SQL stored in a Crystal Reports Command object.
    /// </summary>
    public class SqlCommandMetadata
    {
        /// <summary>
        /// Gets or sets the Command object name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the alias used to reference the Command in the report.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Gets or sets the original SQL statement stored in the report.
        /// </summary>
        public string Sql { get; set; }
    }
}