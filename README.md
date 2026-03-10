# DesignerPort

A modern Windows-only port of the classic Windows Workflow Foundation (WF) designer to (https://github.com/orosandrei/Rehosted-Workflow-Designer) .NET core WPF, backed by CoreWF.

`DesignerPort` restores the main WF designer layer on modern .NET so existing workflow XAML can be opened, edited, inspected, and saved from a WPF host application again.

## Highlights

- Ports `System.Activities.Presentation` to SDK-style modern .NET
- Ports `System.Activities.Core.Presentation` with built-in activity designers, metadata, themes, and resources
- Rehosts the designer in a modern WPF desktop application
- Supports toolbox population, property inspector hosting, and XAML round-trip editing
- Supports `Sequence`, `Flowchart`, and `StateMachine` in the validated path
- Includes sample custom activities and a bookmark-driven sample workflow runner

## Why this exists

The classic WF designer never shipped as an official modern .NET WPF package. This repository is a practical porting baseline for teams that still rely on WF/XAML authoring and need a Windows desktop designer on current .NET.

## Current status

Working now:
- `dotnet build`
- WPF designer host startup
- `DesignerMetadata` registration
- `WorkflowDesigner.View` hosting
- `WorkflowDesigner.PropertyInspectorView` hosting
- toolbox population for the supported activity set
- `Sequence`, `Flowchart`, and `StateMachine` rendering
- XAML open/save round-trip
- custom activity discovery in the designer toolbox
- sample bookmark-driven workflow execution through the runner sample

Current limitations:
- Windows only
- WPF only
- WF/WCF messaging runtime/designer family is still incomplete
- some legacy expression-editor scenarios are only partially compatible
- some advanced compatibility gaps remain

## Repository layout

- `src/System.Activities.Presentation` - ported designer infrastructure
- `src/System.Activities.Core.Presentation` - built-in designers, metadata, themes, and resources
- `src/RehostedDesigner.Port` - WPF host application
- `src/DesignerPort.Smoke` - automated smoke harness
- `WorkflowActivities.Sample` - sample custom activities project
- `WorkflowRunner.Sample` - sample workflow runner

## Prerequisites

- Windows
- .NET 10 SDK
- WPF-capable desktop workload
- local CoreWF checkout present at `CoreWF-runtime\CoreWF-develop`

## Quick start

Build the solution:

```powershell
dotnet build .\DesignerPort.sln -c Release
```

Run the rehosted designer:

```powershell
dotnet run --project .\src\RehostedDesigner.Port\RehostedDesigner.Port.csproj -c Release
```

Open a specific workflow file on startup:

```powershell
dotnet run --project .\src\RehostedDesigner.Port\RehostedDesigner.Port.csproj -c Release -- "C:\path\to\workflow.xaml"
```

Run the sample workflow runner:

```powershell
dotnet run --project .\WorkflowRunner.Sample\WorkflowRunner.Sample.csproj -c Release
```

## Using with Visual Studio

After you build the host, you can make it the default editor for workflow XAML files inside Visual Studio.

Typical setup:
- in Solution Explorer, right-click a workflow `.xaml` file
- choose `Open With...`
- add or select `RehostedDesigner.Port.exe`
- use the built executable from `src\RehostedDesigner.Port\bin\Release\net10.0-windows\RehostedDesigner.Port.exe` or your published host location
- click `Set as Default`

Once set, double-clicking supported workflow XAML files in Visual Studio can open them directly in the rehosted designer instead of the default XML/XAML editor.

## Important notes

- Keep workflow files as workflow XAML, not WPF XAML pages. In SDK-style projects they should typically be included as `None` or `Content`, not as `Page` or `ApplicationDefinition`.
- Prefer compiled custom activities for production use. The validated path in this repo is strongest when workflows reference activity types from compiled assemblies.
- Legacy .NET Framework-authored workflow XAML may require assembly, type, or expression cleanup before it loads cleanly on modern .NET.
- The current supported design-time path is centered on core WF activities plus custom activities that can be resolved from referenced assemblies.
- Messaging/WCF designer support is not the supported path yet.
- This repo intentionally does not reimplement or fork WPF itself; it uses modern WPF on Windows.

## Sample runner

The sample runner demonstrates a simple end-to-end workflow-hosting path outside the designer.

It:
- loads `WorkflowRunner.Sample\myworkflow.xaml`
- starts the workflow
- waits for bookmarks
- lets you resume bookmarks from the console
- runs until workflow completion

Example inputs:
- `Approved`
- `Sendback`
- `Rejected`
- `list`

## Custom activities

The rehosted designer can discover custom activities from referenced or loaded assemblies. `WorkflowActivities.Sample` shows the expected pattern for compiled custom activities used by workflow XAML.

## Scope

This repository intentionally stays:
- Windows-only
- WPF-based
- focused on the WF designer layer
- backed by CoreWF for runtime compatibility paths

## Publishing note

This repository includes porting work based on legacy WF designer/reference-source-era code plus modern WPF/CoreWF integration work. Review provenance, attribution, and licensing requirements before redistributing it.

Contributions of all kinds are welcome!
