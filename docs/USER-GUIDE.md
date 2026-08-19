# Crystal Report Extractor Desktop — User Guide

## Purpose

Crystal Report Extractor Desktop scans a local folder for Crystal Reports
`.rpt` files and writes one readable JSON metadata file for each report. It is
designed to support report discovery, migration analysis and documentation. It
does not render reports, migrate them or translate their business rules.

The desktop program itself does not upload reports or JSON. Files remain in the
input and output folders selected by the operator.

## Requirements

- 64-bit Windows.
- .NET Framework 4.8.
- SAP Crystal Reports runtime compatible with SDK assembly version
  `13.0.4000.0`.
- Read permission for the report folder.
- Write permission for the output folder.

Visual Studio is required to build the source but is not required merely to run
a correctly packaged executable on a machine with the compatible runtime.

## Building from source

1. Extract the solution ZIP to a normal local folder.
2. Open `CrystalReportExtractor.slnx` in Visual Studio.
3. Restore NuGet packages if prompted.
4. Select the `x64` solution platform.
5. Set `CrystalReportExtractor.Desktop` as the startup project.
6. Select **Build > Rebuild Solution**.
7. Confirm that both projects succeed with no failed projects.

Do not change the Core or Desktop projects to `Any CPU` or `x86`.

## Running an extraction

1. Start `CrystalReportExtractor.Desktop`.
2. Select an **Input folder** containing `.rpt` files.
3. Select a different **Output folder**.
4. Leave **Include subfolders** selected to scan the whole input tree, or clear
   it to scan only the chosen folder.
5. Select **Overwrite existing JSON files** only when previous outputs should
   be replaced.
6. Select **Start extraction**.

The progress area shows each report as `Succeeded`, `Failed` or `Skipped`.
**Cancel** stops before the next report; it does not undo reports already
processed.

## Output files

The input folder structure is preserved. For example:

```text
Input\Correspondence\Notice.rpt
Output\Correspondence\Notice.metadata.json
```

Every run also writes `Output\extraction-run-summary.json`.

The run summary records counts, status, relative paths, warning totals and
elapsed milliseconds. Detailed SDK exceptions are written only to local trace
output and are not copied into the JSON files.

## How to interpret a result

- An empty array means the extractor found no accessible items of that type.
- `null` means the property was not defined or the SDK did not expose it.
- `extractionWarnings: []` means no category reported a partial extraction.
- A successful extraction with warnings still produced usable partial JSON.
- `evaluateFormula` and `resetFormula` are populated only for formula-backed
  running-total conditions when Crystal exposes the underlying formula.
- `specialFields` contains special fields actually placed in report sections,
  such as `PrintDate`, `PrintTime` and `PageNumber`.

## Recommended smoke tests

Before processing organisational reports, use non-sensitive SAP samples:

1. `Formulas.rpt` — formula fields.
2. `Customer Orders, Grouped by Country.rpt` — grouping, selection and sorting.
3. `Running Totals Group.rpt` — ordinary and conditional running totals plus
   special fields.
4. `Top5USAwithSub.rpt` — embedded subreport definitions and links.

For `Running Totals Group.rpt`, expect four running totals and the placed
special fields `PrintDate`, `PrintTime` and `PageNumber`.

## Troubleshooting

### The program will not start

Confirm that .NET Framework 4.8 and the matching 64-bit Crystal runtime are
installed. Confirm the executable is accompanied by its DLLs and configuration
file.

### A report fails while others succeed

Check `extraction-run-summary.json`. Common causes include an unreadable or
damaged report, an incompatible Crystal version, a missing database driver, or
SDK metadata unavailable for that report design.

### Existing reports are skipped

Select **Overwrite existing JSON files** or remove the previous metadata files.

### Special fields are empty

Only special fields placed in a report section are emitted. A special field
available in Crystal's Field Explorer but unused by the report is not output.

### A conditional running total has no formula text

The condition type is still recorded. Formula text is best-effort because the
Crystal SDK uses different internal condition representations. Check
`extractionWarnings` and validate the definition in Crystal Reports Designer.

### Report alerts are not present

This is expected. SAP Crystal Reports .NET SDK 13.0.40 does not expose the
complete report-alert definition collection through its supported .NET API.
The extractor deliberately omits an `alerts` property rather than returning an
empty array that could be mistaken for confirmation that a report has no
alerts.

## Information handling

Report definitions and extracted JSON may contain business logic, SQL, server
names, database names, literal text and document structure. Treat the JSON at
the same sensitivity as its source report. Use only organisation-approved
storage and transfer channels.
