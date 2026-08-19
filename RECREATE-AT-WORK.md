# Recreating the Desktop Extractor at Work

## Preferred approach: clone the repository

1. Obtain approval to use the repository and confirm it contains no work
   reports, output JSON, credentials or connection secrets.
2. Install Visual Studio with **.NET desktop development** and the .NET
   Framework 4.8 SDK/targeting pack.
3. Install SAP Crystal Reports for Visual Studio/runtime 64-bit version
   13.0.40 so the `13.0.4000.0` assemblies are available.
4. Clone the repository, then open `CrystalReportExtractor.slnx`.
5. Restore NuGet packages when Visual Studio prompts. This restores
   Newtonsoft.Json 13.0.3; the `packages` folder is intentionally not stored in
   source control.
6. Select `Debug`, `x64`, set `CrystalReportExtractor.Desktop` as the startup
   project, and rebuild the solution.
7. Confirm: **2 succeeded, 0 failed**.
8. Test first with approved, non-sensitive sample `.rpt` files.

## If source files must be recreated manually

Create a blank solution named `CrystalReportExtractor`, then add:

1. A **Class Library (.NET Framework)** named
   `CrystalReportExtractor.Core`, targeting .NET Framework 4.8 and x64.
2. A **Windows Forms App (.NET Framework)** named
   `CrystalReportExtractor.Desktop`, targeting .NET Framework 4.8 and x64.
3. In Desktop, add a project reference to Core and install NuGet package
   `Newtonsoft.Json` version 13.0.3.
4. Add the installed Crystal assemblies listed in each supplied `.csproj`.
   Set **Embed Interop Types = False** on the ReportAppServer references.
5. Recreate the exact folders shown below and copy each supplied source file
   into the corresponding location.

```text
CrystalReportExtractor.Core/
  Models/Extraction/       metadata contract classes
  Services/                extractor implementations and interface
  Properties/AssemblyInfo.cs

CrystalReportExtractor.Desktop/
  BatchModels.cs
  BatchProcessor.cs
  MainForm.cs
  Program.cs
  SQLCommandTest.cs
  SQLCommandTest.rpt
  App.config
  Properties/AssemblyInfo.cs
```

Use the supplied project files as the definitive reference for build settings,
references and compile items. If email blocks `.config`, create `App.config`
inside Visual Studio and paste the supplied text into it.

## What must not be transferred

- Organisational `.rpt` files or extracted JSON through personal storage.
- Passwords, tokens, database credentials or private connection strings.
- `.vs`, `bin`, `obj` and `packages` folders; they are generated locally and
  make transfers unnecessarily large.

## Acceptance check

- The solution contains exactly Core and Desktop projects.
- Both projects build as x64 with no failures.
- The desktop application opens and accepts separate input/output folders.
- A known sample produces `<name>.metadata.json` plus
  `extraction-run-summary.json`.
- Review `extractionWarnings` before treating any category as complete.
