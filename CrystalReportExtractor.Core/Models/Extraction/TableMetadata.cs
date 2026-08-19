// ============================================================================
// File: TableMetadata.cs
// Purpose:
//   Defines the stable JSON representation of a database table, view, stored
//   procedure or SQL Command object referenced by a Crystal report.
//
// Role in the architecture:
//   Carries data-dependency information from the Crystal SDK extraction layer
//   into the JSON output used for lineage and migration analysis.
//
// Key behaviour:
//   - Records the Crystal table name, alias and source location.
//   - Records the SDK table classification where available.
//   - Does not expose database credentials or connection properties.
//
// Dependencies:
//   - Standard .NET types only.
//
// Important considerations:
//   - Crystal represents tables, views, stored procedures and Command objects
//     through related SDK table abstractions.
//   - The SDK classification may not reliably distinguish every database object
//     type; uncertain classifications will be recorded conservatively.
//   - Connection credentials must never be added to this model.
//
// Maintenance notes:
//   SQL Command text and sanitised connection metadata will use deliberate
//   dedicated models rather than being placed into generic properties here.
// ============================================================================

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents a data object referenced by a Crystal report.
    /// </summary>
    public class TableMetadata
    {
        /// <summary>
        /// Gets or sets the object name exposed by Crystal.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the alias used inside the report.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Gets or sets the database-qualified source location where accessible.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets Crystal's textual classification of the object.
        /// </summary>
        public string ObjectType { get; set; }

        /// <summary>
        /// Gets or sets a provider-specific table/view/procedure classification
        /// when the installed Crystal data driver exposes one.
        /// </summary>
        public string ProviderObjectType { get; set; }
    }
}
