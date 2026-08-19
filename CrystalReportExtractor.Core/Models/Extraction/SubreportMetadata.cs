// ============================================================================
// File: SubreportMetadata.cs
// Purpose:
//   Defines the recursive JSON representation of a Crystal subreport and its
//   links to the parent report.
//
// Role in the architecture:
//   Preserves nested report definitions so downstream analysis can follow the
//   complete parent report → subreport → nested subreport dependency chain.
//
// Key behaviour:
//   - Identifies the subreport and its placement in the parent report.
//   - Contains the recursively extracted subreport metadata.
//   - Records parent fields or parameters linked to subreport parameters.
//
// Dependencies:
//   - ReportMetadata
//   - Standard .NET collection types
//
// Important considerations:
//   - A report may contain multiple visual instances of the same subreport.
//   - Subreports can contain further subreports.
//   - Recursion depth must be limited to protect against malformed reports.
//   - Crystal subreport documents own native resources and require careful
//     disposal.
//
// Maintenance notes:
//   Keep link metadata separate from the nested report definition because links
//   describe the parent-child relationship rather than the subreport alone.
// ============================================================================

using System.Collections.Generic;

namespace CrystalReportExtractor.Core.Models.Extraction
{
    /// <summary>
    /// Represents a subreport embedded in a parent Crystal report.
    /// </summary>
    public class SubreportMetadata
    {
        /// <summary>
        /// Gets or sets the subreport name used by Crystal.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the parent section containing the subreport object.
        /// </summary>
        public string ParentSection { get; set; }

        /// <summary>
        /// Gets or sets the name of the visual subreport object.
        /// </summary>
        public string ReportObjectName { get; set; }

        /// <summary>
        /// Gets or sets the recursively extracted subreport definition.
        /// </summary>
        public ReportMetadata Definition { get; set; }

        /// <summary>
        /// Gets links supplying values from the parent to this subreport.
        /// </summary>
        public List<SubreportLinkMetadata> Links { get; set; }
            = new List<SubreportLinkMetadata>();
    }

    /// <summary>
    /// Represents a value passed from a parent report into a subreport.
    /// </summary>
    public class SubreportLinkMetadata
    {
        /// <summary>
        /// Gets or sets the parent field, formula or parameter expression.
        /// </summary>
        public string ParentValue { get; set; }

        /// <summary>
        /// Gets or sets the receiving parameter in the subreport.
        /// </summary>
        public string SubreportParameter { get; set; }
    }
}