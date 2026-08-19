// ============================================================================
// File: CrystalReportExtractor.cs
// Purpose:
//   Loads a Crystal Reports .rpt file and maps accessible report definition
//   information into the application's stable metadata model.
//
// Role in the architecture:
//   Core extraction service between the temporary uploaded report file and the
//   JSON metadata returned to the browser.
//
// Key behaviour:
//   - Loads reports using the SAP Crystal Reports .NET SDK.
//   - Extracts report identity and SDK-version metadata.
//   - Extracts formula names and preserves their original Crystal expressions.
//   - Will progressively extract database, parameter, selection, grouping,
//     sorting, section and subreport metadata.
//   - Closes and disposes Crystal resources after every extraction attempt.
//   - Uses the embedded RAS model to extract object names, aliases and classes.
//   - Detects SQL Command objects and preserves their embedded SQL text.
//   - Extracts parameters and accessible discrete or range default values.
//   - Extracts running-total fields and their calculation behaviour.
//   - Extracts built-in Crystal special fields used by the report.
//   - Recursively extracts embedded subreports with a defensive depth limit.
//
// Dependencies:
//   - SAP Crystal Reports .NET SDK
//   - ReportMetadata domain models
//
// Important considerations:
//   - ReportDocument owns unmanaged resources and must always be closed and
//     disposed, including when report loading or extraction fails.
//   - OpenReportByTempCopy prevents Crystal from retaining an unnecessary lock
//     on the application's temporary upload file.
//   - Database credentials must never be included in extraction output.
//   - Formula expressions are preserved exactly as exposed by the SDK.
//   - Failure to read one formula is recorded as a warning and does not prevent
//     extraction of the remainder of the report.
//
// Maintenance notes:
//   Map Crystal SDK objects into deliberate domain models rather than exposing
//   SDK objects directly through JSON.
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using CrystalReportExtractor.Core.Models.Extraction;
using System.Globalization;
using System.Diagnostics;


namespace CrystalReportExtractor.Core.Services
{
    
    /// <summary>
    /// Extracts structured metadata from Crystal Reports files using the SAP
    /// Crystal Reports .NET SDK.
    /// </summary>
    public class CrystalReportExtractor : ICrystalReportExtractor
    {
        private const int MaximumSubreportDepth = 10;

        /// <summary>
        /// Loads a Crystal Reports file and extracts its currently supported
        /// metadata.
        /// </summary>
        /// <param name="reportFilePath">
        /// Absolute path to the application-managed temporary report file.
        /// </param>
        /// <param name="sourceFileName">
        /// Sanitised original filename recorded in the output.
        /// </param>
        /// <param name="fileSizeBytes">
        /// Size of the uploaded report file in bytes.
        /// </param>
        /// <returns>A stable metadata representation of the report.</returns>
        public ReportMetadata Extract(
            string reportFilePath,
            string sourceFileName,
            long fileSizeBytes)
        {
            if (string.IsNullOrWhiteSpace(reportFilePath))
            {
                throw new ArgumentException(
                    "A report file path is required.",
                    nameof(reportFilePath));
            }

            if (!File.Exists(reportFilePath))
            {
                throw new FileNotFoundException(
                    "The temporary Crystal Reports file was not found.",
                    reportFilePath);
            }

            var metadata = new ReportMetadata
            {
                ExtractedAtUtc = DateTime.UtcNow,
                CrystalSdkVersion = GetCrystalSdkVersion(),
                Report = new ReportIdentityMetadata
                {
                    SourceFileName = sourceFileName,
                    FileSizeBytes = fileSizeBytes
                }
            };

            ReportDocument reportDocument = new ReportDocument();

            try
            {
                // Crystal creates its own working copy so that its native report
                // engine does not unnecessarily retain a lock on our upload file.
                reportDocument.Load(
                    reportFilePath,
                    OpenReportMethod.OpenReportByTempCopy);

                ExtractReportIdentity(
                    reportDocument,
                    sourceFileName,
                    metadata);
                
                ExtractTables(
                    reportDocument,
                    metadata);

                ExtractFormulas(
                    reportDocument,
                    metadata);

                ExtractParameters(
                    reportDocument,
                    metadata);

                ExtractSelectionLogic(
                    reportDocument,
                    metadata);

                ExtractGroups(
                    reportDocument,
                    metadata);

                ExtractSorts(
                    reportDocument,
                    metadata);

                ExtractRunningTotals(
                    reportDocument,
                    metadata);

                ExtractGroupNameFields(
                    reportDocument,
                    metadata);

                ExtractSpecialFields(
                    reportDocument,
                    metadata);

                ExtendedMetadataExtractor.Extract(
                    reportDocument,
                    metadata,
                    includeRasRelationships: true);

                ExtractSubreports(
                    reportDocument,
                    metadata,
                    recursionDepth: 0);
            }
            finally
            {
                // Close releases report-engine state, while Dispose releases the
                // managed wrapper. Both are required because ReportDocument owns
                // native resources that garbage collection alone cannot promptly
                // or reliably release.
                reportDocument.Close();
                reportDocument.Dispose();
            }

            return metadata;
        }

        /// <summary>
        /// Extracts the report title, falling back to the filename when Crystal
        /// does not contain a title.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="sourceFileName">The sanitised uploaded filename.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractReportIdentity(
            ReportDocument reportDocument,
            string sourceFileName,
            ReportMetadata metadata)
        {
            string reportTitle =
                reportDocument.SummaryInfo.ReportTitle;

            // Many legacy reports do not define a title in Summary Info.
            // Falling back to the filename provides a useful identity while
            // leaving the original source filename available separately.
            metadata.Report.ReportName =
                string.IsNullOrWhiteSpace(reportTitle)
                    ? Path.GetFileNameWithoutExtension(sourceFileName)
                    : reportTitle;
        }
        /// <summary>
        /// Extracts database object names, aliases and SDK classifications using
        /// the richer Report Application Server object model.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        /// <summary>
        /// Extracts database object definitions and any embedded SQL Command
        /// statements using the richer RAS object model.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractTables(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            // ReportDocument provides convenient report loading, while its
            // embedded ReportClientDocument exposes the richer RAS definition
            // required to distinguish names, aliases and object classes.
            CrystalDecisions.ReportAppServer.DataDefModel.Database database =
                reportDocument.ReportClientDocument
                    .DatabaseController
                    .Database;

            foreach (
                CrystalDecisions.ReportAppServer.DataDefModel.Table table
                in database.Tables)
            {
                try
                {
                    metadata.Tables.Add(
                        new TableMetadata
                        {
                            Name = table.Name,
                            Alias = table.Alias,
                            Location = ReadSdkText(table, "QualifiedName")
                                ?? table.Name,
                            ObjectType = table.ClassName,
                            ProviderObjectType = ReadSdkText(
                                table, "TableType", "Type", "ObjectType")
                        });

                    if (string.Equals(
                        table.ClassName,
                        "CrystalReports.CommandTable",
                        StringComparison.Ordinal))
                    {
                        ExtractSqlCommand(table, metadata);
                    }
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        $"Database object '{table.Alias}' could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts the original SQL from a RAS CommandTable object.
        /// </summary>
        /// <param name="table">
        /// The RAS table already classified as a Crystal Command object.
        /// </param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractSqlCommand(
            CrystalDecisions.ReportAppServer.DataDefModel.Table table,
            ReportMetadata metadata)
        {
            var commandTable =
                (CrystalDecisions.ReportAppServer.DataDefModel.CommandTable)table;

            metadata.SqlCommands.Add(
                new SqlCommandMetadata
                {
                    Name = commandTable.Name,
                    Alias = commandTable.Alias,

                    // Preserve the SQL exactly as stored in the report. The
                    // extractor analyses report definitions and never executes
                    // this statement.
                    Sql = commandTable.CommandText
                });
        }



        /// <summary>
        /// Extracts formula field names and their original Crystal expressions.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractFormulas(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            foreach (FormulaFieldDefinition formula
                in reportDocument.DataDefinition.FormulaFields)
            {
                try
                {
                    metadata.Formulas.Add(
                        new FormulaMetadata
                        {
                            Name = formula.Name,
                            Expression = formula.Text
                        });
                }
                catch (Exception)
                {
                    // Some reports contain formula definitions that the managed
                    // SDK cannot fully resolve. Preserve the formula's identity
                    // where possible and flag the incomplete extraction without
                    // exposing potentially sensitive SDK exception details.
                    string formulaName =
                        string.IsNullOrWhiteSpace(formula.Name)
                            ? "[unknown formula]"
                            : formula.Name;

                    metadata.ExtractionWarnings.Add(
                        $"Formula '{formulaName}' could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts parameters and their accessible stored default values.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractParameters(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            foreach (ParameterFieldDefinition parameter
                in reportDocument.DataDefinition.ParameterFields)
            {
                try
                {
                    var parameterMetadata = new ParameterMetadata
                    {
                        Name = parameter.Name,
                        PromptText = parameter.PromptText,
                        ValueType = parameter.ValueType.ToString(),
                        AllowsMultipleValues =
                            parameter.EnableAllowMultipleValue,
                        AllowsNullValue =
                            parameter.EnableNullValue
                    };

                    ExtractParameterDefaultValues(
                        parameter,
                        parameterMetadata);

                    metadata.Parameters.Add(parameterMetadata);
                }
                catch (Exception)
                {
                    string parameterName =
                        string.IsNullOrWhiteSpace(parameter.Name)
                            ? "[unknown parameter]"
                            : parameter.Name;

                    metadata.ExtractionWarnings.Add(
                        $"Parameter '{parameterName}' could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Maps Crystal discrete and range defaults into the stable parameter
        /// value model.
        /// </summary>
        /// <param name="parameter">The Crystal parameter definition.</param>
        /// <param name="metadata">The parameter model being populated.</param>
        private static void ExtractParameterDefaultValues(
            ParameterFieldDefinition parameter,
            ParameterMetadata metadata)
        {
            foreach (ParameterValue defaultValue in parameter.DefaultValues)
            {
                if (defaultValue is ParameterDiscreteValue discreteValue)
                {
                    metadata.DefaultValues.Add(
                        new ParameterValueMetadata
                        {
                            Kind = "Discrete",
                            Value = FormatParameterValue(discreteValue.Value)
                        });
                }
                else if (defaultValue is ParameterRangeValue rangeValue)
                {
                    metadata.DefaultValues.Add(
                        new ParameterValueMetadata
                        {
                            Kind = "Range",
                            StartValue =
                                FormatParameterValue(rangeValue.StartValue),
                            EndValue =
                                FormatParameterValue(rangeValue.EndValue),
                            LowerBoundType =
                                rangeValue.LowerBoundType.ToString(),
                            UpperBoundType =
                                rangeValue.UpperBoundType.ToString()
                        });
                }
                else
                {
                    // Preserve visibility of an SDK value type that this
                    // extractor does not yet explicitly understand.
                    metadata.DefaultValues.Add(
                        new ParameterValueMetadata
                        {
                            Kind = defaultValue.GetType().Name,
                            Value = defaultValue.ToString()
                        });
                }
            }
        }

        /// <summary>
        /// Converts a Crystal parameter value into culture-independent text so
        /// JSON output remains stable across server regional settings.
        /// </summary>
        /// <param name="value">The value exposed by Crystal.</param>
        /// <returns>Invariant text, or <c>null</c> for a null value.</returns>
        private static string FormatParameterValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(
                    format: null,
                    formatProvider: CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// Extracts running-total definitions, including the accumulated field,
        /// summary operation, evaluation rule and reset rule.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractRunningTotals(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            foreach (RunningTotalFieldDefinition runningTotal
                in reportDocument.DataDefinition.RunningTotalFields)
            {
                try
                {
                    metadata.RunningTotals.Add(
                        new RunningTotalMetadata
                        {
                            Name = runningTotal.Name,
                            SummarizedField =
                                runningTotal.SummarizedField?.Name,
                            SummaryOperation =
                                runningTotal.Operation.ToString(),
                            EvaluateCondition =
                                runningTotal.EvaluationConditionType.ToString(),
                            EvaluateFormula =
                                GetRunningTotalConditionExpression(
                                    runningTotal,
                                    "EvaluationCondition"),
                            ResetCondition =
                                runningTotal.ResetConditionType.ToString(),
                            ResetFormula =
                                GetRunningTotalConditionExpression(
                                    runningTotal,
                                    "ResetCondition")
                        });
                }
                catch (Exception)
                {
                    string runningTotalName =
                        string.IsNullOrWhiteSpace(runningTotal.Name)
                            ? "[unknown running total]"
                            : runningTotal.Name;

                    metadata.ExtractionWarnings.Add(
                        $"Running total '{runningTotalName}' could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts record-level and group-level selection formulas exactly as
        /// stored in the report definition.
        /// </summary>
        /// <param name="reportDocument">The loaded main report or subreport.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractSelectionLogic(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            try
            {
                metadata.SelectionLogic.RecordSelectionFormula =
                    reportDocument.DataDefinition.RecordSelectionFormula;
            }
            catch (Exception)
            {
                metadata.ExtractionWarnings.Add(
                    "The record selection formula could not be extracted.");
            }

            try
            {
                metadata.SelectionLogic.GroupSelectionFormula =
                    reportDocument.DataDefinition.GroupSelectionFormula;
            }
            catch (Exception)
            {
                metadata.ExtractionWarnings.Add(
                    "The group selection formula could not be extracted.");
            }
        }

        /// <summary>
        /// Reads a formula-backed running-total condition without binding the
        /// stable metadata model to Crystal's internal condition object type.
        /// Crystal 13 exposes EvaluationCondition and ResetCondition through
        /// objects whose concrete representation varies by condition kind.
        /// </summary>
        private static string GetRunningTotalConditionExpression(
            RunningTotalFieldDefinition runningTotal,
            string conditionPropertyName)
        {
            try
            {
                PropertyInfo conditionProperty =
                    runningTotal.GetType().GetProperty(
                        conditionPropertyName,
                        BindingFlags.Instance | BindingFlags.Public);

                object condition = conditionProperty?.GetValue(
                    runningTotal,
                    null);

                if (condition == null)
                {
                    return null;
                }

                if (condition is string conditionText)
                {
                    return string.IsNullOrWhiteSpace(conditionText)
                        ? null
                        : conditionText;
                }

                // Formula field definitions normally expose Text. Formula and
                // FormulaForm cover alternate Engine/RAS condition wrappers.
                string[] expressionPropertyNames =
                {
                    "Text",
                    "Formula",
                    "FormulaForm",
                    "Expression"
                };

                foreach (string expressionPropertyName
                    in expressionPropertyNames)
                {
                    PropertyInfo expressionProperty =
                        condition.GetType().GetProperty(
                            expressionPropertyName,
                            BindingFlags.Instance | BindingFlags.Public);

                    object value = expressionProperty?.GetValue(
                        condition,
                        null);

                    if (value != null
                        && !string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        return value.ToString();
                    }
                }
            }
            catch (Exception)
            {
                // Condition formulas are supplementary metadata. Failure to
                // read one must not prevent the running-total definition from
                // being emitted.
            }

            return null;
        }

        /// <summary>
        /// Extracts every explicit group level in report order.
        /// </summary>
        /// <param name="reportDocument">The loaded main report or subreport.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractGroups(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            for (int index = 0;
                index < reportDocument.DataDefinition.Groups.Count;
                index++)
            {
                Group group = reportDocument.DataDefinition.Groups[index];

                try
                {
                    FieldDefinition conditionField = group.ConditionField;

                    metadata.Groups.Add(
                        new GroupMetadata
                        {
                            Level = index,
                            ConditionField = conditionField?.Name,
                            FieldKind = conditionField?.Kind.ToString(),
                            ValueType = conditionField?.ValueType.ToString()
                        });
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        $"Group level {index} could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts record-sort and group-sort definitions in evaluation order.
        /// </summary>
        /// <param name="reportDocument">The loaded main report or subreport.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractSorts(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            for (int index = 0;
                index < reportDocument.DataDefinition.SortFields.Count;
                index++)
            {
                SortField sortField =
                    reportDocument.DataDefinition.SortFields[index];

                try
                {
                    metadata.Sorts.Add(
                        new SortMetadata
                        {
                            Priority = index,
                            Field = sortField.Field?.Name,
                            SortType = sortField.SortType.ToString(),
                            Direction = sortField.SortDirection.ToString()
                        });
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        $"Sort priority {index} could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts a generated group-name field for every group defined in the
        /// report, including groups whose name field is not visibly placed.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractGroupNameFields(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            CrystalDecisions.ReportAppServer.DataDefModel.Groups groups =
                reportDocument.ReportClientDocument
                    .DataDefController
                    .DataDefinition
                    .Groups;

            foreach (
                CrystalDecisions.ReportAppServer.DataDefModel.Group group
                in groups)
            {
                try
                {
                    // Crystal generates a GroupNameField from its owning group.
                    // It does not have to appear in ResultFields or be visibly
                    // placed in a report section to be a valid report field.
                    var groupNameField =
                        new CrystalDecisions.ReportAppServer.DataDefModel
                            .GroupNameFieldClass
                        {
                            Group = group
                        };

                    metadata.GroupNameFields.Add(
                        new GroupNameFieldMetadata
                        {
                            Name = group.ConditionField?.Name,
                            Expression = groupNameField.FormulaForm,
                            ValueType = groupNameField.Type.ToString()
                        });
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        "A group name field could not be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts built-in Crystal special fields that are used by the report,
        /// such as page number, print date, report title or record number.
        /// </summary>
        /// <param name="reportDocument">The loaded Crystal report.</param>
        /// <param name="metadata">The output model being populated.</param>
        private static void ExtractSpecialFields(
            ReportDocument reportDocument,
            ReportMetadata metadata)
        {
            // Special fields are not guaranteed to appear in the RAS
            // ResultFields collection. Inspect fields actually placed in the
            // report, which is the reliable source for used special fields.
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
                    FieldDefinition field = fieldObject.DataSource;

                    if (field == null
                        || field.GetType().Name.IndexOf(
                            "Special",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    // One special field can be placed in several sections.
                    // Emit one definition for each distinct field name.
                    if (metadata.SpecialFields.Exists(
                        item => string.Equals(
                            item.Name,
                            field.Name,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    try
                    {
                        metadata.SpecialFields.Add(
                            new SpecialFieldMetadata
                            {
                                Name = field.Name,
                                Expression = field.Name,
                                ValueType = field.ValueType.ToString()
                            });
                    }
                    catch (Exception)
                    {
                        metadata.ExtractionWarnings.Add(
                            $"Special field '{field.Name}' could not be fully extracted.");
                    }
                }
            }
        }

        /// <summary>
        /// Finds subreport objects in their parent sections and recursively
        /// extracts each embedded report definition.
        /// </summary>
        /// <param name="parentReport">The loaded parent report.</param>
        /// <param name="parentMetadata">The parent metadata being populated.</param>
        /// <param name="recursionDepth">The current nested-report depth.</param>
        private static void ExtractSubreports(
            ReportDocument parentReport,
            ReportMetadata parentMetadata,
            int recursionDepth)
        {
            if (recursionDepth >= MaximumSubreportDepth)
            {
                parentMetadata.ExtractionWarnings.Add(
                    $"Subreport recursion stopped at the safety limit of "
                    + $"{MaximumSubreportDepth} levels.");

                return;
            }

            foreach (Section section
                in parentReport.ReportDefinition.Sections)
            {
                foreach (ReportObject reportObject
                    in section.ReportObjects)
                {
                    if (reportObject.Kind !=
                        ReportObjectKind.SubreportObject)
                    {
                        continue;
                    }

                    var subreportObject =
                        (SubreportObject)reportObject;

                    ExtractSubreport(
                        parentReport,
                        section,
                        subreportObject,
                        parentMetadata,
                        recursionDepth);
                }
            }
        }

        /// <summary>
        /// Extracts database objects from a subreport through its parent-owned
        /// RAS client document.
        /// </summary>
        /// <summary>
        /// Extracts subreport database objects from the database definition
        /// supplied by the parent report's SubreportController.
        /// </summary>
        private static void ExtractSubreportTables(
            CrystalDecisions.ReportAppServer.DataDefModel.Database database,
            ReportMetadata metadata)
        {
            foreach (
                CrystalDecisions.ReportAppServer.DataDefModel.Table table
                in database.Tables)
            {
                try
                {
                    metadata.Tables.Add(
                        new TableMetadata
                        {
                            Name = table.Name,
                            Alias = table.Alias,
                            Location = ReadSdkText(table, "QualifiedName")
                                ?? table.Name,
                            ObjectType = table.ClassName,
                            ProviderObjectType = ReadSdkText(
                                table, "TableType", "Type", "ObjectType")
                        });

                    if (string.Equals(
                        table.ClassName,
                        "CrystalReports.CommandTable",
                        StringComparison.Ordinal))
                    {
                        ExtractSqlCommand(table, metadata);
                    }
                }
                catch (Exception)
                {
                    metadata.ExtractionWarnings.Add(
                        $"Subreport database object '{table.Alias}' could not "
                        + "be fully extracted.");
                }
            }
        }

        /// <summary>
        /// Extracts database tables from an opened embedded subreport.
        ///
        /// The RAS SubreportController cannot reliably return a database object
        /// for every embedded Crystal subreport. This fallback uses the Crystal
        /// Engine table collection exposed by the opened subreport.
        /// </summary>
        /// <param name="subreportDocument">
        /// The opened Crystal Reports subreport.
        /// </param>
        /// <param name="metadata">
        /// The metadata object receiving the extracted table definitions.
        /// </param>
        private static void ExtractSubreportTables(
            ReportDocument subreportDocument,
            ReportMetadata metadata)
        {
            foreach (CrystalDecisions.CrystalReports.Engine.Table table
                in subreportDocument.Database.Tables)
            {
                metadata.Tables.Add(
                    new TableMetadata
                    {
                        Name = table.Name,
                        Alias = table.Name,
                        Location = table.Location,
                        ObjectType = table.GetType().Name,
                        ProviderObjectType = ReadSdkText(
                            table, "TableType", "Type", "ObjectType")
                    });
            }
        }
        /// <summary>
        /// Extracts group-name fields from an opened Engine subreport.
        /// </summary>
        private static void ExtractSubreportGroupNameFields(
            ReportDocument subreportDocument,
            ReportMetadata metadata)
        {
            foreach (FieldDefinition field
                in subreportDocument.DataDefinition.GroupNameFields)
            {
                metadata.GroupNameFields.Add(
                    new GroupNameFieldMetadata
                    {
                        Name = field.Name,

                        // The Engine API does not expose RAS FormulaForm for
                        // this subreport field, so preserve its Crystal name.
                        Expression = field.Name,
                        ValueType = field.ValueType.ToString()
                    });
            }
        }

        /// <summary>
        /// Extracts built-in special fields from an opened Engine subreport.
        /// </summary>
        /// <summary>
        /// Extracts built-in special fields visibly used by an opened Engine
        /// subreport by inspecting its placed field objects.
        /// </summary>
        private static void ExtractSubreportSpecialFields(
            ReportDocument subreportDocument,
            ReportMetadata metadata)
        {
            foreach (Section section
                in subreportDocument.ReportDefinition.Sections)
            {
                foreach (ReportObject reportObject
                    in section.ReportObjects)
                {
                    if (reportObject.Kind != ReportObjectKind.FieldObject)
                    {
                        continue;
                    }

                    var fieldObject = (FieldObject)reportObject;
                    FieldDefinition field = fieldObject.DataSource;

                    if (field == null)
                    {
                        continue;
                    }

                    string sdkFieldClass =
                        field.GetType().Name;

                    // The Engine object model does not expose a SpecialFields
                    // collection for subreports. Its concrete SDK definition
                    // class identifies placed special fields.
                    if (sdkFieldClass.IndexOf(
                        "Special",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    metadata.SpecialFields.Add(
                        new SpecialFieldMetadata
                        {
                            Name = field.Name,
                            Expression = field.Name,
                            ValueType = field.ValueType.ToString()
                        });
                }
            }
        }
        /// <summary>
        /// Extracts values passed from a parent report into a subreport.
        /// </summary>
        /// <param name="parentReport">The loaded parent report.</param>
        /// <param name="subreportName">The Crystal subreport name.</param>
        /// <param name="metadata">
        /// The relationship model being populated.
        /// </param>
        private static void ExtractSubreportLinks(
            ReportDocument parentReport,
            string subreportName,
            SubreportMetadata metadata)
        {
            CrystalDecisions.ReportAppServer.ReportDefModel.SubreportLinks links =
                parentReport.ReportClientDocument
                    .SubreportController
                    .GetSubreportLinks(subreportName);

            foreach (
                CrystalDecisions.ReportAppServer.ReportDefModel.SubreportLink link
                in links)
            {
                metadata.Links.Add(
                    new SubreportLinkMetadata
                    {
                        ParentValue = link.MainReportFieldName,
                        SubreportParameter = link.SubreportFieldName
                    });
            }
        }
        /// <summary>
        /// Opens one embedded subreport, extracts its definition and releases
        /// the associated native Crystal resources.
        /// </summary>
        /// <param name="parentReport">The loaded parent report.</param>
        /// <param name="parentSection">
        /// The section containing the subreport object.
        /// </param>
        /// <param name="subreportObject">
        /// The visual object referencing the embedded subreport.
        /// </param>
        /// <param name="parentMetadata">The parent metadata being populated.</param>
        /// <param name="recursionDepth">The current nested-report depth.</param>
        private static void ExtractSubreport(
            ReportDocument parentReport,
            Section parentSection,
            SubreportObject subreportObject,
            ReportMetadata parentMetadata,
            int recursionDepth)
        {
            ReportDocument subreportDocument = null;

            try
            {
                // Open the embedded subreport through the Crystal Engine API.
                // The RAS SubreportController throws NullReferenceException for
                // some valid embedded subreports.
                subreportDocument =
                    parentReport.OpenSubreport(
                        subreportObject.SubreportName);

                var nestedMetadata = new ReportMetadata
                {
                    ExtractedAtUtc = DateTime.UtcNow,
                    CrystalSdkVersion = GetCrystalSdkVersion(),
                    Report = new ReportIdentityMetadata
                    {
                        SourceFileName = null,
                        FileSizeBytes = 0
                    }
                };

                // Crystal does not support SummaryInfo on opened subreports.
                // Use the embedded Crystal name as its identity.
                nestedMetadata.Report.ReportName =
                    subreportObject.SubreportName;

                ExtractSubreportTables(
                    subreportDocument,
                    nestedMetadata);

                ExtractFormulas(
                    subreportDocument,
                    nestedMetadata);

                ExtractParameters(
                    subreportDocument,
                    nestedMetadata);

                ExtractRunningTotals(
                    subreportDocument,
                    nestedMetadata);

                ExtractSelectionLogic(
                    subreportDocument,
                    nestedMetadata);

                ExtractGroups(
                    subreportDocument,
                    nestedMetadata);

                ExtractSorts(
                    subreportDocument,
                    nestedMetadata);

                ExtractSubreportGroupNameFields(
                    subreportDocument,
                    nestedMetadata);

                ExtractSubreportSpecialFields(
                    subreportDocument,
                    nestedMetadata);

                ExtendedMetadataExtractor.Extract(
                    subreportDocument,
                    nestedMetadata,
                    includeRasRelationships: false);

                // Crystal Reports does not support placing a subreport inside
                // another subreport, so there is no deeper level to traverse.

                var subreportMetadata = new SubreportMetadata
                {
                    Name = subreportObject.SubreportName,
                    ParentSection = parentSection.Name,
                    ReportObjectName = subreportObject.Name,
                    Definition = nestedMetadata
                };

                ExtractSubreportLinks(
                    parentReport,
                    subreportObject.SubreportName,
                    subreportMetadata);

                parentMetadata.Subreports.Add(subreportMetadata);
            }
            catch (Exception exception)
            {
                // Log the full SDK exception locally for diagnosis. The JSON
                // receives only the exception type so local paths, connection
                // details and other sensitive information are not disclosed.
                Trace.TraceError(
                    "Subreport extraction failed for '{0}': {1}",
                    subreportObject.SubreportName,
                    exception);

                parentMetadata.ExtractionWarnings.Add(
                    $"Subreport '{subreportObject.SubreportName}' could not "
                    + $"be fully extracted ({exception.GetType().Name}).");
            }
            finally
            {
                if (subreportDocument != null)
                {
                    // An opened subreport is another ReportDocument wrapper
                    // around native report-engine resources and must be closed
                    // independently after its metadata has been copied.
                    subreportDocument.Close();
                    subreportDocument.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns the installed Crystal report-engine assembly version.
        /// </summary>
        /// <returns>The SDK assembly version as text.</returns>
        private static string GetCrystalSdkVersion()
        {
            Assembly engineAssembly =
                typeof(ReportDocument).Assembly;

            Version version = engineAssembly.GetName().Version;

            return version?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Reads the first available version/provider-specific SDK property as
        /// text without creating a compile-time dependency on that property.
        /// </summary>
        private static string ReadSdkText(object source, params string[] names)
        {
            if (source == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                try
                {
                    PropertyInfo property = source.GetType().GetProperty(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);
                    object value = property?.GetValue(source, null);
                    if (value != null &&
                        !string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        return value.ToString();
                    }
                }
                catch (Exception)
                {
                    // Crystal drivers expose different table properties. A
                    // missing optional classification must not fail extraction.
                }
            }

            return null;
        }
    }
}
