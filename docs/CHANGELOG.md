# Changelog

## 19 August 2026 — desktop-only package

- Removed the ASP.NET web project from the solution and package.
- Moved extractor services and metadata models into the Core project.
- Changed shared namespaces from `CrystalReportExtractor.Web` to
  `CrystalReportExtractor.Core`.
- Removed all linked compile items that pointed into the Web project.
- Retained the complete desktop extraction feature set and report-alert SDK
  limitation.
- Added a work-machine reconstruction guide and Git-safe ignore rules.

## Report-alert limitation documented — 18 August 2026

- Removed the experimental alert model, JSON property and reflection code after
  validation confirmed that SAP's .NET SDK does not expose the complete alert
  definition collection.
- Documented why `GetTriggeredAlerts()` is not used: it returns refresh-time
  runtime state rather than a complete static report definition.

## Corrected package — 17 August 2026

### Fixed

- Main-report special fields are now discovered from placed section field
  objects instead of relying on the RAS `ResultFields` collection.
- Duplicate special fields are suppressed by case-insensitive name.
- Conditional running totals now expose nullable `evaluateFormula` and
  `resetFormula` properties using guarded SDK reflection.

### Documentation

- Replaced the outdated desktop README.
- Added a current user guide.
- Added technical documentation describing the actual projects, JSON contract,
  security behaviour, limitations and validation set.

### Validation still required on Windows

- Rebuild all projects using `Debug | x64` or `Release | x64`.
- Re-extract `Running Totals Group.rpt`.
- Confirm `specialFields` contains `PrintDate`, `PrintTime` and `PageNumber`.
- Confirm formula-based totals populate `evaluateFormula`; if the SDK returns no
  text, the property should be `null` without failing the report.
