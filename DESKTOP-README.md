# Crystal Report Extractor Desktop

Windows desktop utility for batch-extracting metadata from SAP Crystal Reports
`.rpt` files into structured JSON. Processing is local: the application reads
reports from a selected folder and writes JSON to another selected folder.

This package contains only the Core library and Windows desktop application.
The former ASP.NET web project and all web-hosting code have been removed.

## Quick start

1. Install the 64-bit SAP Crystal Reports runtime compatible with SDK
   `13.0.4000.0`.
2. Open `CrystalReportExtractor.slnx` in Visual Studio.
3. Select `x64` and set `CrystalReportExtractor.Desktop` as the startup project.
4. Rebuild and run.
5. Choose separate input and output folders, then select **Start extraction**.

Each report produces `<report-name>.metadata.json`. Each run also produces
`extraction-run-summary.json`.

The metadata contract includes formulas, SQL Commands, parameters, running
totals, summaries, groups, sorts, selection logic, placed special fields,
layout/objects and first-level subreports.

## Known SDK limitation: report alerts

Report-alert definitions are not included in the JSON. SAP's Crystal Reports
.NET SDK 13.0.40 does not expose the report's complete alert-definition
collection through its supported .NET API. It exposes only alerts triggered by
the most recent data refresh, which is runtime state rather than a complete and
reliable description of the report design.

## Documentation

- `docs/USER-GUIDE.md` — installation, operation and troubleshooting.
- `docs/TECHNICAL-DOCUMENTATION.md` — architecture, extraction coverage,
  JSON contract, security controls and maintenance guidance.
- `docs/CHANGELOG.md` — changes made to this corrected source package.
- `RECREATE-AT-WORK.md` — precise setup and manual reconstruction checklist.

## Platform

- Windows x64
- .NET Framework 4.8
- C# 7.3
- SAP Crystal Reports SDK/runtime 13.0.40 (`13.0.4000.0` assemblies)
- Newtonsoft.Json 13.0.3

Do not transfer work reports or their extracted metadata through personal or
otherwise unapproved storage. Validate deployment with non-sensitive SAP
sample reports first.
