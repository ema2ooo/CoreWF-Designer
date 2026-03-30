# Repository Guidelines

## Project Structure & Module Organization
`DesignerPort.sln` is the entry point for the repository. Core ported designer code lives in `src/System.Activities.Presentation` and `src/System.Activities.Core.Presentation`. The WPF host app is `src/RehostedDesigner.Port`, and the regression harness is `src/DesignerPort.Smoke`. Sample custom activities live in `WorkflowActivities.Sample`, while `WorkflowRunner.Sample` exercises workflow execution and persistence outside the designer. Keep new assets and sample XAML near the owning project, for example `src/RehostedDesigner.Port/Samples/`.

## Build, Test, and Development Commands
Use the .NET 10 SDK on Windows.

- `dotnet build .\DesignerPort.sln -c Release` builds the full solution.
- `dotnet run --project .\src\RehostedDesigner.Port\RehostedDesigner.Port.csproj -c Release` launches the designer host.
- `dotnet run --project .\src\RehostedDesigner.Port\RehostedDesigner.Port.csproj -c Release -- "C:\path\to\workflow.xaml"` opens a workflow on startup.
- `dotnet run --project .\src\DesignerPort.Smoke\DesignerPort.Smoke.csproj -c Release` runs the smoke harness.
- `dotnet run --project .\WorkflowRunner.Sample\WorkflowRunner.Sample.csproj -c Release` runs the sample workflow runner.

The solution references a sibling checkout at `..\CoreWF-runtime\CoreWF-develop`; builds will fail until that path exists.

## Coding Style & Naming Conventions
Use 4-space indentation in C# and XAML. Follow the surrounding namespace style: most production code under `src/` uses file-scoped namespaces, while some sample code still uses block-scoped namespaces. Use PascalCase for types, dependency properties, and public members; use camelCase for locals and private fields. Keep XAML and code-behind names aligned, and avoid broad formatting churn in ported files.

## Testing Guidelines
There is no conventional unit test project yet; `DesignerPort.Smoke` is the primary regression check. Add or update smoke assertions when changing toolbox population, metadata registration, designer hosting, or XAML round-tripping. For activity or sample workflow changes, also verify the host app and `WorkflowRunner.Sample` still load the workflow successfully.

## Commit & Pull Request Guidelines
Recent commits use short, imperative, sentence-case subjects such as `Add note about contributions to README`. Keep commits focused and descriptive. Pull requests should summarize behavior changes, call out any required CoreWF-side updates, link related issues, and include screenshots for WPF UI changes. List the manual validation you ran, especially build and smoke commands, plus any gaps you could not verify.

## Configuration Notes
This repository is Windows-only and WPF-only. Shared defaults in `Directory.Build.props` target `net10.0-windows`, disable nullable reference types for the main solution, and disable implicit usings unless a project overrides them.
