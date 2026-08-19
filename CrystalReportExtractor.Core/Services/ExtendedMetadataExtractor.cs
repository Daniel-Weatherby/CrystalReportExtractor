// ============================================================================
// File: ExtendedMetadataExtractor.cs
// Purpose:
//   Extracts data lineage, field usage, summaries and layout/formatting metadata
//   that supplements the core CrystalReportExtractor traversal.
//
// Role in the architecture:
//   Operates inside the Crystal SDK boundary and maps version-sensitive SDK
//   properties into the stable ExtendedMetadata domain models.
//
// Key behaviour:
//   - Uses defensive reflection for properties that vary between Engine and RAS.
//   - Sanitises connection information by allow-listing non-secret properties.
//   - Inspects both main reports and opened subreports without assuming RAS is
//     available for a subreport wrapper.
//
// Dependencies:
//   - SAP Crystal Reports Engine
//   - ExtendedMetadata models
//
// Important considerations:
//   Crystal frequently throws NotSupportedException from valid subreport
//   properties. Each category is isolated so one unsupported property does not
//   prevent extraction of the rest of the report.
// ============================================================================

using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using CrystalReportExtractor.Core.Models.Extraction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CrystalReportExtractor.Core.Services
{
    /// <summary>
    /// Extracts extended metadata through a stable, version-tolerant boundary.
    /// </summary>
    internal static class ExtendedMetadataExtractor
    {
        private static readonly Regex CrystalReferencePattern =
            new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Populates the extended portions of a report metadata result.
        /// </summary>
        /// <param name="reportDocument">Loaded main report or opened subreport.</param>
        /// <param name="metadata">Stable metadata model to populate.</param>
        /// <param name="includeRasRelationships">
        /// Whether the parent report's RAS database model can be inspected.
        /// </param>
        public static void Extract(
            ReportDocument reportDocument,
            ReportMetadata metadata,
            bool includeRasRelationships)
        {
            TryCategory("general report", metadata, () =>
                ExtractGeneralMetadata(reportDocument, metadata));
            TryCategory("data source", metadata, () =>
                ExtractDataSources(reportDocument, metadata));
            TryCategory("relationship", metadata, () =>
                ExtractRelationships(reportDocument, metadata, includeRasRelationships));
            TryCategory("field usage", metadata, () =>
                ExtractFieldUsage(reportDocument, metadata));
            TryCategory("summary", metadata, () =>
                ExtractSummaries(reportDocument, metadata));
            TryCategory("section and report object", metadata, () =>
                ExtractSections(reportDocument, metadata));
        }

        private static void ExtractGeneralMetadata(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            object summaryInfo = GetProperty(reportDocument, "SummaryInfo");
            metadata.Report.Author = FirstString(summaryInfo, "ReportAuthor", "Author");
            metadata.Report.Subject = FirstString(summaryInfo, "ReportSubject", "Subject");
            metadata.Report.Comments = FirstString(summaryInfo, "ReportComments", "Comments");
            metadata.Report.Keywords = FirstString(summaryInfo, "ReportKeywords", "Keywords");
            metadata.Report.HasSavedData = GetNullableBool(reportDocument, "HasSavedData");
        }

        /// <summary>
        /// Extracts sanitised connection metadata from each Engine table.
        ///
        /// Crystal's table collection supports strongly typed traversal but can throw
        /// when accessed through its reflected IEnumerable implementation. Use the
        /// Engine collection directly and expose only allow-listed, non-secret values.
        /// </summary>
        private static void ExtractDataSources(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            CrystalDecisions.CrystalReports.Engine.Tables tables = reportDocument.Database.Tables;

            for (int tableIndex = 0;
                tableIndex < tables.Count;
                tableIndex++)
            {
                CrystalDecisions.CrystalReports.Engine.Table table =
                    tables[tableIndex];
                try
                {
                    object logOnInfo = table.LogOnInfo;
                    object connectionInfo =
                        GetProperty(logOnInfo, "ConnectionInfo");

                    if (connectionInfo == null)
                    {
                        continue;
                    }

                    string server =
                        GetString(connectionInfo, "ServerName");

                    string database =
                        GetString(connectionInfo, "DatabaseName");

                    string connectionType = FirstNonEmpty(
                        FirstString(
                            connectionInfo,
                            "Type",
                            "DatabaseDLL"),
                        connectionInfo.GetType().Name);

                    string key = string.Join(
                        "|",
                        server,
                        database,
                        connectionType);

                    if (seen.Add(key))
                    {
                        metadata.DataSources.Add(
                            new DataSourceMetadata
                            {
                                ServerName = server,
                                DatabaseName = database,
                                ConnectionType = connectionType
                            });
                    }
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        "Connection metadata for database object '"
                        + table.Name
                        + "' could not be extracted.");
                }
            }
        }

        private static void ExtractRelationships(
            ReportDocument reportDocument,
            ReportMetadata metadata,
            bool includeRasRelationships)
        {
            var candidates = new List<object>
            {
                GetProperty(reportDocument.Database, "Links"),
                GetProperty(reportDocument.Database, "TableLinks")
            };

            if (includeRasRelationships)
            {
                object clientDocument = GetProperty(reportDocument, "ReportClientDocument");
                object databaseController = GetProperty(clientDocument, "DatabaseController");
                object database = GetProperty(databaseController, "Database");
                candidates.Add(GetProperty(database, "TableLinks"));
                candidates.Add(GetProperty(database, "Links"));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object candidate in candidates.Where(item => item != null))
            {
                foreach (object link in Enumerate(candidate))
                {
                    string sourceTable = FirstString(link, "SourceTableAlias");

                    string sourceFields =
                        DescribeField(
                            FirstProperty(
                                link,
                                "SourceFieldNames",
                                "SourceField",
                                "SourceFields"));

                    string source = string.IsNullOrWhiteSpace(sourceTable)
                        ? sourceFields
                        : sourceTable + "." + sourceFields;

                    string targetTable =
                        FirstString(link, "TargetTableAlias");

                    string targetFields =
                        DescribeField(
                            FirstProperty(
                                link,
                                "TargetFieldNames",
                                "TargetField",
                                "TargetFields"));

                    string destination = string.IsNullOrWhiteSpace(targetTable)
                        ? targetFields
                        : targetTable + "." + targetFields;
                    string joinType = FirstString(link, "JoinType", "LinkType");
                    string enforcement = FirstString(
                        link, "EnforceJoin", "Enforcement", "LinkEnforcement");
                    string key = string.Join("|", source, destination, joinType, enforcement);

                    if (seen.Add(key))
                    {
                        metadata.Relationships.Add(new RelationshipMetadata
                        {
                            SourceField = source,
                            DestinationField = destination,
                            JoinType = joinType,
                            Enforcement = enforcement
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Identifies fields used by report definitions, expressions and placed field
        /// objects.
        ///
        /// Crystal field collections are accessed by index because some SDK collection
        /// enumerators throw even though they advertise IEnumerable support.
        /// </summary>
        private static void ExtractFieldUsage(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            var byKey = new Dictionary<string, FieldUsageMetadata>(
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                index < reportDocument.DataDefinition.FormulaFields.Count;
                index++)
            {
                FormulaFieldDefinition formula =
                    reportDocument.DataDefinition.FormulaFields[index];

                AddField(byKey, formula, "FormulaFields");

                AddExpressionReferences(
                    byKey,
                    formula.Text,
                    "FormulaExpression");
            }

            for (int index = 0;
                index < reportDocument.DataDefinition.ParameterFields.Count;
                index++)
            {
                AddField(
                    byKey,
                    reportDocument.DataDefinition.ParameterFields[index],
                    "ParameterFields");
            }

            for (int index = 0;
                index < reportDocument.DataDefinition.Groups.Count;
                index++)
            {
                AddField(
                    byKey,
                    reportDocument.DataDefinition.Groups[index].ConditionField,
                    "GroupCondition");
            }

            for (int index = 0;
                index < reportDocument.DataDefinition.SortFields.Count;
                index++)
            {
                AddField(
                    byKey,
                    reportDocument.DataDefinition.SortFields[index].Field,
                    "SortFields");
            }

            for (int index = 0;
                index < reportDocument.DataDefinition.RunningTotalFields.Count;
                index++)
            {
                AddField(
                    byKey,
                    reportDocument.DataDefinition
                        .RunningTotalFields[index]
                        .SummarizedField,
                    "RunningTotalFields");
            }

            for (int index = 0;
                index < reportDocument.DataDefinition.GroupNameFields.Count;
                index++)
            {
                AddField(
                    byKey,
                    reportDocument.DataDefinition.GroupNameFields[index],
                    "GroupNameFields");
            }

            foreach (Section section
                in reportDocument.ReportDefinition.Sections)
            {
                foreach (ReportObject reportObject
                    in section.ReportObjects)
                {
                    if (reportObject.Kind != ReportObjectKind.FieldObject)
                    {
                        continue;
                    }

                    var fieldObject = (FieldObject)reportObject;

                    if (fieldObject.DataSource != null)
                    {
                        AddField(
                            byKey,
                            fieldObject.DataSource,
                            "Section:" + section.Name);
                    }
                }
            }

            AddExpressionReferences(
                byKey,
                reportDocument.DataDefinition.RecordSelectionFormula,
                "RecordSelectionFormula");

            AddExpressionReferences(
                byKey,
                reportDocument.DataDefinition.GroupSelectionFormula,
                "GroupSelectionFormula");

            metadata.Fields.AddRange(
                byKey.Values.OrderBy(field => field.Name));
        }

        private static void AddExpressionReferences(
            IDictionary<string, FieldUsageMetadata> byKey,
            string expression,
            string context)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            foreach (Match match in CrystalReferencePattern.Matches(expression))
            {
                string reference = match.Groups[1].Value.Trim();
                string kind = reference.StartsWith("?", StringComparison.Ordinal)
                    ? "ParameterField"
                    : reference.StartsWith("@", StringComparison.Ordinal)
                        ? "FormulaField"
                        : "DatabaseField";
                string key = string.Join("|", reference, kind, string.Empty);

                if (!byKey.TryGetValue(key, out FieldUsageMetadata field))
                {
                    field = new FieldUsageMetadata
                    {
                        Name = reference,
                        FieldKind = kind
                    };
                    byKey.Add(key, field);
                }

                if (!field.UsageContexts.Contains(context))
                {
                    field.UsageContexts.Add(context);
                }
            }
        }

        private static void AddFieldCollection(
            IDictionary<string, FieldUsageMetadata> byKey,
            object collection,
            string context)
        {
            foreach (object item in Enumerate(collection))
            {
                object field = FirstProperty(item, "Field", "SummarizedField", "ConditionField") ?? item;
                AddField(byKey, field, context);
            }
        }

        private static void AddField(
            IDictionary<string, FieldUsageMetadata> byKey,
            object field,
            string context)
        {
            string name = DescribeField(field);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string kind = FirstString(field, "Kind", "FieldKind");
            string valueType = FirstString(field, "ValueType", "Type");
            string key = string.Join("|", name, kind, valueType);

            if (!byKey.TryGetValue(key, out FieldUsageMetadata metadata))
            {
                metadata = new FieldUsageMetadata
                {
                    Name = name,
                    FieldKind = kind,
                    ValueType = valueType
                };
                byKey.Add(key, metadata);
            }

            if (!metadata.UsageContexts.Contains(context))
            {
                metadata.UsageContexts.Add(context);
            }
        }

        private static void ExtractSummaries(
    ReportDocument reportDocument,
    ReportMetadata metadata)
        {
            object summaryFields =
                GetProperty(
                    reportDocument.DataDefinition,
                    "SummaryFields");

            foreach (object summary in Enumerate(summaryFields))
            {
                // A Crystal Group object does not provide a useful name through
                // ToString(). Read its grouping condition field instead.
                object group =
                    FirstProperty(
                        summary,
                        "Group",
                        "GroupName");

                object groupConditionField =
                    FirstProperty(
                        group,
                        "ConditionField",
                        "Field");

                string groupName =
                    DescribeField(groupConditionField)
                    ?? FirstString(
                        group,
                        "Name",
                        "GroupName");

                metadata.Summaries.Add(
                    new SummaryMetadata
                    {
                        Name = FirstString(
                            summary,
                            "Name",
                            "FormulaName",
                            "FormulaForm"),

                        SummarizedField = DescribeField(
                            GetProperty(
                                summary,
                                "SummarizedField")),

                        SummaryOperation = FirstString(
                            summary,
                            "SummaryOperation",
                            "Operation"),

                        IsPercentage = GetNullableBool(
                            summary,
                            "IsPercentageSummary"),

                        Group = groupName
                    });
            }
        }

        private static void ExtractSections(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            foreach (Section section in reportDocument.ReportDefinition.Sections)
            {
                object format = GetProperty(section, "SectionFormat");
                var sectionMetadata = new SectionMetadata
                {
                    Name = section.Name,
                    Kind = FirstString(section, "Kind", "AreaKind"),
                    Height = GetNullableInt(section, "Height"),
                    Suppressed = GetNullableBool(format, "EnableSuppress"),
                    KeepTogether = GetNullableBool(format, "KeepTogether"),
                    NewPageBefore = GetNullableBool(format, "NewPageBefore"),
                    NewPageAfter = GetNullableBool(format, "NewPageAfter")
                };

                ExtractConditionalFormulas(format, sectionMetadata.ConditionalFormulas);

                foreach (ReportObject reportObject in section.ReportObjects)
                {
                    object objectFormat = GetProperty(reportObject, "ObjectFormat");
                    var objectMetadata = new ReportObjectMetadata
                    {
                        Name = reportObject.Name,
                        Kind = reportObject.Kind.ToString(),
                        Top = GetNullableInt(reportObject, "Top"),
                        Left = GetNullableInt(reportObject, "Left"),
                        Width = GetNullableInt(reportObject, "Width"),
                        Height = GetNullableInt(reportObject, "Height"),
                        Suppressed = GetNullableBool(objectFormat, "EnableSuppress"),
                        DataSource = DescribeField(FirstProperty(
                            reportObject, "DataSource", "SubreportName")),
                        Text = FirstString(reportObject, "Text")
                    };

                    ExtractConditionalFormulas(objectFormat, objectMetadata.ConditionalFormulas);
                    sectionMetadata.ReportObjects.Add(objectMetadata);
                }

                metadata.Sections.Add(sectionMetadata);
            }
        }

        private static void ExtractConditionalFormulas(
            object format,
            ICollection<ConditionalFormulaMetadata> destination)
        {
            object formulas = FirstProperty(format, "ConditionFormulas", "ConditionalFormulas");
            if (formulas == null)
            {
                return;
            }

            PropertyInfo indexer = formulas.GetType().GetProperty("Item");
            ParameterInfo[] parameters = indexer?.GetIndexParameters();
            if (parameters != null &&
                parameters.Length == 1 &&
                parameters[0].ParameterType.IsEnum)
            {
                foreach (object enumValue in Enum.GetValues(parameters[0].ParameterType))
                {
                    object formula = null;
                    try
                    {
                        formula = indexer.GetValue(formulas, new[] { enumValue });
                    }
                    catch (Exception)
                    {
                        formula = null;
                    }

                    AddConditionalFormula(
                        destination,
                        formula,
                        enumValue.ToString());
                }
                return;
            }

            foreach (object formula in Enumerate(formulas))
            {
                AddConditionalFormula(destination, formula, null);
            }
        }

        private static void AddConditionalFormula(
            ICollection<ConditionalFormulaMetadata> destination,
            object formula,
            string propertyName)
        {
            string expression = formula as string ?? FirstString(
                formula, "Text", "Formula", "FormulaForm", "Expression");
            if (!string.IsNullOrWhiteSpace(expression))
            {
                destination.Add(new ConditionalFormulaMetadata
                {
                    Property = FirstNonEmpty(
                        propertyName,
                        FirstString(formula, "Name", "Kind", "Type")),
                    Expression = expression
                });
            }
        }

        private static void TryCategory(
            string category,
            ReportMetadata metadata,
            Action extraction)
        {
            try
            {
                extraction();
            }
            catch (Exception)
            {
                metadata.ExtractionWarnings.Add(
                    "The " + category + " metadata could not be fully extracted.");
            }
        }

        private static object GetProperty(object source, string propertyName)
        {
            if (source == null)
            {
                return null;
            }

            try
            {
                PropertyInfo property = source.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                return property?.GetValue(source, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object FirstProperty(object source, params string[] names)
        {
            foreach (string name in names)
            {
                object value = GetProperty(source, name);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static string GetString(object source, string propertyName)
        {
            return ToInvariantString(GetProperty(source, propertyName));
        }

        private static string FirstString(object source, params string[] names)
        {
            foreach (string name in names)
            {
                string value = GetString(source, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string DescribeField(object source)
        {
            if (source == null)
            {
                return null;
            }

            if (!(source is string) && IsCollection(source))
            {
                return string.Join(", ", Enumerate(source)
                    .Select(DescribeField)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            return FirstNonEmpty(
                FirstString(source, "FormulaForm", "Name", "FieldName", "Alias"),
                ToInvariantString(source));
        }

        private static int? GetNullableInt(object source, string propertyName)
        {
            object value = GetProperty(source, propertyName);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool? GetNullableBool(object source, string propertyName)
        {
            object value = GetProperty(source, propertyName);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IEnumerable<object> Enumerate(object source)
        {
            if (source == null || source is string)
            {
                yield break;
            }

            int? count = GetNullableInt(source, "Count");
            // Crystal collections can expose several Item indexers, including
            // integer, string and enum variants. Select the integer indexer used
            // for positional collection access instead of allowing reflection to
            // throw AmbiguousMatchException.
            PropertyInfo indexer = source.GetType()
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .FirstOrDefault(
                    property =>
                    {
                        if (!string.Equals(
                            property.Name,
                            "Item",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        ParameterInfo[] indexParameters =
                            property.GetIndexParameters();

                        return indexParameters.Length == 1 &&
                            indexParameters[0].ParameterType == typeof(int);
                    });
            if (indexer != null)
            {
                ParameterInfo[] parameters = indexer.GetIndexParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum)
                {
                    foreach (object enumValue in Enum.GetValues(parameters[0].ParameterType))
                    {
                        object enumItem = null;
                        try
                        {
                            enumItem = indexer.GetValue(source, new[] { enumValue });
                        }
                        catch (Exception)
                        {
                            enumItem = null;
                        }

                        if (enumItem != null)
                        {
                            yield return enumItem;
                        }
                    }
                    yield break;
                }

                if (count.HasValue && parameters.Length == 1)
                {
                    // The managed Crystal collections exposed by the Engine and
                    // RAS .NET SDK use zero-based indexes. Do not probe an invalid
                    // one-based index: Visual Studio may break on that first-
                    // chance SDK exception even when application code catches it.
                    for (int offset = 0; offset < count.Value; offset++)
                    {
                        object item = null;
                        try
                        {
                            item = indexer.GetValue(
                                source,
                                new object[] { offset });
                        }
                        catch (Exception)
                        {
                            item = null;
                        }

                        if (item != null)
                        {
                            yield return item;
                        }
                    }

                    yield break;
                }
            }

            // Some Crystal SDK collections expose IEnumerable but throw when
            // GetEnumerator is called. If indexed access was unavailable, skip
            // that collection rather than invoking its unreliable enumerator.
            string sourceNamespace = source.GetType().Namespace ?? string.Empty;

            if (sourceNamespace.StartsWith(
                "CrystalDecisions.",
                StringComparison.Ordinal))
            {
                yield break;
            }

            // Use IEnumerable only as a fallback. A number of Crystal SDK
            // collections implement this interface but throw from MoveNext,
            // which is why indexed access above is preferred.
            if (source is IEnumerable enumerable)
            {
                IEnumerator enumerator = null;
                try
                {
                    enumerator = enumerable.GetEnumerator();
                }
                catch (Exception)
                {
                    yield break;
                }

                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = enumerator.MoveNext();
                    }
                    catch (Exception)
                    {
                        yield break;
                    }

                    if (!hasNext)
                    {
                        yield break;
                    }

                    object item;
                    try
                    {
                        item = enumerator.Current;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
        }

        private static bool IsCollection(object source)
        {
            return source is IEnumerable || GetProperty(source, "Count") != null;
        }

        private static string ToInvariantString(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
