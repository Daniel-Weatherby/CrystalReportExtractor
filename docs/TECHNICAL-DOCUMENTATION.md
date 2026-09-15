# Crystal Report Extractor — Technical Documentation

## 1. Scope

This document describes the desktop-only source package supplied on 19 August
2026. It is derived from the code in this solution. The assembly version
remains `1.0.0.0`.

The solution extracts accessible Crystal report-definition metadata into a
stable, camel-case JSON contract. It supports local folder-based batch
operation through the desktop project. There is no web project, upload endpoint
or server-hosting requirement.

## 2. Solution architecture

| Project | Target | Responsibility |
| --- | --- | --- |
| `CrystalReportExtractor.Core` | .NET Framework 4.8, x64 library | Reusable extractor, metadata models and Crystal SDK integration. All Core source files are physically contained in this project. |
| `CrystalReportExtractor.Desktop` | .NET Framework 4.8, x64 WinForms | Folder selection, batch orchestration, progress, cancellation and JSON output. |

The Desktop project references Core. Core has no source links or dependency on
another project, making the two-project solution self-contained.

## 3. Runtime dependencies

- C# language version 7.3.
- .NET Framework 4.8.
- 64-bit process architecture.
- SAP Crystal Reports Engine and Report Application Server assemblies version
  `13.0.4000.0`.
- Newtonsoft.Json 13.0.3 for desktop serialization.

RAS interop references set `EmbedInteropTypes` to `False` to avoid CS1762
embedded-interop warnings and runtime type incompatibilities.

## 4. Processing flow

1. The desktop scans the selected input folder for `*.rpt` files.
2. Files are ordered case-insensitively by full path.
3. `CrystalReportExtractor.Extract` opens each report using
   `OpenReportByTempCopy`.
4. Strongly typed Engine/RAS extraction populates core definitions.
5. `ExtendedMetadataExtractor` adds version-tolerant metadata using guarded
   reflection.
6. Embedded subreports are opened and mapped into nested `ReportMetadata`.
7. The report is closed and disposed in a `finally` block.
8. `ReportAnalyser` calculates direct and recursive structural metrics and a versioned complexity score.
9. Newtonsoft.Json serializes the enriched model with indented camel-case properties.
10. `CsvInventoryWriter` writes the flattened `crystal-report-inventory.csv` portfolio view.
11. `DependencyCsvWriter` writes `crystal-report-dependencies.csv` with report-to-database-object lineage.
12. Output is written through a same-directory temporary file and then moved or
   replaced atomically.
13. Processing continues after an individual report failure.

## 5. Extracted JSON contract

| Property | Content |
| --- | --- |
| `report` | Filename, report name, size, Summary Info, keywords and saved-data flag. |
| `extractedAtUtc` | Extraction start time in UTC. |
| `crystalSdkVersion` | Loaded Engine assembly version. |
| `tables` | Database objects, aliases, locations and accessible object classifications. |
| `dataSources` | Allow-listed server, database and connection-type values. |
| `relationships` | Accessible source/destination fields, join type and enforcement. |
| `fields` | Referenced fields, field kind, value type and usage contexts. |
| `formulas` | Formula names and Crystal expressions. |
| `sqlCommands` | Command name, alias and SQL text for accessible Command tables. |
| `parameters` | Prompt metadata, null/multiple flags, and accessible discrete/range defaults. |
| `runningTotals` | Accumulated field, operation, condition/reset types and accessible formulas. |
| `summaries` | Ordinary summary definitions. |
| `groupNameFields` | Generated group-name field definitions. |
| `specialFields` | Built-in special fields actually placed in report sections. |
| `selectionLogic` | Record-selection and group-selection formulas. |
| `groups` | Ordered grouping levels and condition fields. |
| `sorts` | Ordered record/group sort definitions and directions. |
| `sections` | Section layout, object placement, literal text, suppression and conditional formatting. |
| `subreports` | Nested report metadata, parent section/object and parameter links. |
| `extractionWarnings` | Safe descriptions of partial or inaccessible metadata. |
| `analysis` | Direct and whole-report-tree counts, complexity score, band and scoring-model version. |

### Running totals

Each running total contains `name`, `summarizedField`, `summaryOperation`,
`evaluateCondition`, `evaluateFormula`, `resetCondition` and `resetFormula`.

The two formula properties are nullable. The extractor reads
`EvaluationCondition` and `ResetCondition` reflectively, then checks the
condition wrapper for `Text`, `Formula`, `FormulaForm` or `Expression`. This
avoids binding the stable model to an internal condition-object type.

### Special fields

The corrected main-report implementation inspects `FieldObject.DataSource` for
every placed field in every section. A concrete SDK type containing `Special`
is treated as a special field. Results are deduplicated by case-insensitive
field name. This is intentionally based on usage: unplaced Field Explorer items
are not output.

### Report alerts

Report-alert definitions are deliberately outside the JSON contract. The
installed .NET interop assemblies contain alert models but do not expose the
internal `DataDefinition.Alerts` getter. The supported
`SearchController.GetTriggeredAlerts()` API returns only alerts triggered by
the report's most recent data refresh, so it cannot provide a complete static
inventory of alert definitions. Returning that runtime subset would create
false negatives during report analysis.

## 6. Deterministic analysis

Analysis is calculated from the stable in-memory `ReportMetadata` object before
JSON serialisation. `directMetrics` describes the parent report only;
`totalMetrics` includes recursively extracted subreports.

Complexity model 1.0 uses explicit structural weights: formulas, parameters,
tables, relationships and sorts each add 1; summaries, groups, selection
formulas and conditional formulas add 2; running totals add 3; SQL Commands and
subreports add 5. Scores of 0–10 are Low, 11–30 are Medium and 31 or more are
High. The raw counts remain authoritative; the score is a migration-planning
heuristic and can be recalculated under a future model.

The desktop writes `crystal-report-inventory.csv` after every run. Its counts
use `totalMetrics`, so nested report complexity is not hidden. Existing JSON
files skipped by the overwrite option are deserialised and analysed for the
CSV, but are not rewritten.

## 7. Subreport handling

The extractor discovers `SubreportObject` instances in parent sections and
opens them with `ReportDocument.OpenSubreport`. It extracts:

- identity using the embedded subreport name;
- Engine database tables;
- formulas and parameters;
- running totals and accessible condition formulas;
- selection formulas, groups and sorts;
- group-name and placed special fields;
- extended field, summary, section/object and connection metadata; and
- parent-to-subreport links.

Crystal does not support a subreport inside another subreport, so the code does
not attempt a deeper traversal. `MaximumSubreportDepth` remains a defensive
limit for parent traversal.

## 8. Security and privacy controls

- The desktop application contains no upload or network-transfer code.
- Database passwords, credentials and connection properties outside the
  explicit allow-list are not serialized.
- Batch JSON contains a generic failure message rather than detailed exception
  text.
- Full exceptions are written to local trace output for diagnosis.
- Report and subreport Engine wrappers are explicitly closed and disposed.
- Input and output directories must be different.

The output is not automatically non-sensitive. It may contain SQL, formulas,
server/database names, literal report text and business rules.

## 9. Batch behaviour

- Input enumeration is top-level or recursive according to the UI option.
- Input subfolder structure is preserved in the output.
- Existing JSON is skipped unless overwrite is selected.
- Cancellation is checked between reports.
- Every run overwrites `extraction-run-summary.json` with the latest run.
- Every run creates `crystal-report-dependencies.csv` for main-report and subreport database objects.
- Embedded SQL text remains in detailed JSON and is represented only by a flag in the dependency CSV.
- Atomic file writing prevents a partially serialized final JSON file.

## 10. Known limitations

- Extraction is limited to metadata exposed by the installed Crystal SDK and
  available database drivers.
- `null` does not distinguish “not configured” from “not exposed” in every SDK
  category.
- Conditional running-total formula text is best-effort.
- Special fields are emitted only when placed in a report section.
- Main-report Command SQL is supported. The current Engine-only subreport table
  fallback does not guarantee subreport Command SQL extraction.
- Relationship and formatting properties vary between report and SDK versions;
  guarded extraction may return partial results with warnings.
- The application does not validate semantic equivalence, execute SQL, refresh
  report data, render output or migrate Crystal logic.
- No automated Windows/Crystal SDK test project is included.

## 11. Build and validation

Build both projects in Visual Studio using `x64`. A successful rebuild should
report two succeeded projects and no failures.

| Sample | Expected focus |
| --- | --- |
| `Formulas.rpt` | Tables and formula expressions. |
| `Customer Orders, Grouped by Country.rpt` | Relationships, group, selection, sorts and group-name fields. |
| `Running Totals Group.rpt` | Four running totals; formula conditions; PrintDate, PrintTime and PageNumber. |
| `Top5USAwithSub.rpt` | Embedded subreport tables, group/special fields and links. |

After rebuilding, compare new JSON with a known-good baseline and inspect
`extractionWarnings`. Source review outside Windows cannot replace a Windows
x64 build because the SAP runtime is Windows-specific.

## 12. Maintenance guidance

- Add stable model properties deliberately; downstream consumers rely on JSON
  property names.
- Prefer strongly typed Engine APIs for stable collections.
- Use guarded reflection for properties that vary across SDK representations.
- Do not serialize SDK objects directly.
- Preserve per-category warnings so one inaccessible property does not discard
  the report.
- Keep `Close` and `Dispose` calls in `finally` blocks.
- Update this document and the user guide whenever the contract changes.
