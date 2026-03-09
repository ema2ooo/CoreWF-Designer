using System;
using System.Activities;
using System.Activities.Core.Presentation.Factories;
using System.Activities.Presentation;
using System.Activities.Presentation.Toolbox;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RehostedDesigner.Port;

internal sealed class WorkflowToolboxCatalog
{
    private const string SampleSource = "Original RehostedDesigner sample InitializeActivitiesToolbox()";
    private const string MessagingDeferredReason = "Deferred: messaging/ServiceModel designers are not enabled. System.Activities.Core.Presentation/System/Activities/Core/Presentation/DesignerMetadata.cs leaves messaging registration deferred, and System.Activities.Core.Presentation/System.Activities.Core.Presentation.csproj removes System\\ServiceModel\\** from compilation.";
    private const string StateMachineDeferredReason = "Deferred: state-machine designers are not enabled. System.Activities.Core.Presentation/System.Activities.Core.Presentation.csproj removes State*.*, Transition*.*, FinalState*.*, StateContainerEditor*.*, StateMachine*.* and Factories\\StateMachineWithInitialStateFactory.cs from compilation.";

    private readonly IReadOnlyList<BaselineToolboxItem> baseline;
    private readonly IReadOnlyList<WorkflowToolboxEntry> surfacedEntries;
    private readonly IReadOnlyList<ToolboxDiffRow> diffRows;
    private readonly IReadOnlyDictionary<string, WorkflowToolboxEntry> entriesByDisplayName;

    private WorkflowToolboxCatalog(
        IReadOnlyList<BaselineToolboxItem> baseline,
        IReadOnlyList<WorkflowToolboxEntry> surfacedEntries,
        IReadOnlyList<ToolboxDiffRow> diffRows)
    {
        this.baseline = baseline;
        this.surfacedEntries = surfacedEntries;
        this.diffRows = diffRows;
        this.entriesByDisplayName = surfacedEntries.ToDictionary(static entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    public int BaselineCount => this.baseline.Count(static item => item.AppearedInOriginalSampleToolbox);

    public int SurfacedCount => this.surfacedEntries.Count;

    public int RestoredCount => this.diffRows.Count(static row => row.Status == ToolboxDiffStatus.PresentAndWorking && row.Baseline.AppearedInOriginalSampleToolbox && !row.Baseline.DependsOnDeferredSubsystem);

    public int StillMissingCount => this.diffRows.Count(static row => row.Status != ToolboxDiffStatus.PresentAndWorking && row.Baseline.AppearedInOriginalSampleToolbox && !row.Baseline.DependsOnDeferredSubsystem);

    public static WorkflowToolboxCatalog Create(string startupWorkflowPath = null)
    {
        Dictionary<string, Type> discoveredTypes = DiscoverRuntimeTypes(startupWorkflowPath);
        IReadOnlyList<BaselineToolboxItem> baseline = BuildNet48Baseline(discoveredTypes);
        IReadOnlyList<WorkflowToolboxEntry> surfacedEntries = BuildSurfacedEntries(baseline, discoveredTypes)
            .OrderBy(static entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        IReadOnlyList<ToolboxDiffRow> diffRows = BuildStrictDiff(baseline, surfacedEntries, discoveredTypes);
        return new WorkflowToolboxCatalog(baseline, surfacedEntries, diffRows);
    }

    public ToolboxControl CreateToolboxControl()
    {
        ToolboxControl toolbox = new ToolboxControl();
        foreach (IGrouping<string, WorkflowToolboxEntry> categoryGroup in this.surfacedEntries.GroupBy(static entry => entry.Category).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            ToolboxCategory category = new ToolboxCategory(categoryGroup.Key);
            foreach (WorkflowToolboxEntry entry in categoryGroup)
            {
                category.Add(entry.CreateToolboxItemWrapper());
            }

            if (category.Tools.Count > 0)
            {
                toolbox.Categories.Add(category);
            }
        }

        return toolbox;
    }

    public bool Contains(string toolNameOrDisplayName)
        => !string.IsNullOrWhiteSpace(toolNameOrDisplayName)
            && this.surfacedEntries.Any(entry => string.Equals(entry.DisplayName, toolNameOrDisplayName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.ToolName, toolNameOrDisplayName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, int> GetCategoryBreakdown()
        => this.surfacedEntries
            .GroupBy(static entry => entry.Category)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetMissingBaselineActivities()
        => this.diffRows
            .Where(static row => row.Baseline.AppearedInOriginalSampleToolbox && !row.Baseline.DependsOnDeferredSubsystem && row.Status != ToolboxDiffStatus.PresentAndWorking)
            .Select(static row => row.Baseline.ActivityName)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyDictionary<string, string> GetDeferredBaselineActivities()
        => this.diffRows
            .Where(static row => row.Baseline.AppearedInOriginalSampleToolbox && row.Baseline.DependsOnDeferredSubsystem)
            .OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static row => row.Baseline.ActivityName, static row => row.Reason, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetPresentBaselineActivities()
        => this.diffRows
            .Where(static row => row.Baseline.AppearedInOriginalSampleToolbox && row.Status == ToolboxDiffStatus.PresentAndWorking)
            .Select(static row => row.Baseline.ActivityName)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool TryCreateProbeActivity(string displayName, out Activity activity, out string reason)
    {
        activity = null;
        reason = null;

        WorkflowToolboxEntry entry;
        if (!this.entriesByDisplayName.TryGetValue(displayName, out entry))
        {
            reason = "Toolbox item was not surfaced.";
            return false;
        }

        return entry.TryCreateProbeActivity(out activity, out reason);
    }

    public string CreateBaselineMarkdown()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# .NET 4.8 Toolbox Baseline");
        builder.AppendLine();
        builder.AppendLine($"Reconstructed from: {SampleSource}");
        builder.AppendLine();
        builder.AppendLine("| Activity | Source Assembly | Category | In Original Sample Toolbox | Special Factory | Deferred Dependency |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (BaselineToolboxItem item in this.baseline.OrderBy(static item => item.Category, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "| {0} | {1} | {2} | {3} | {4} | {5} |",
                item.ActivityName,
                item.SourceAssembly,
                item.Category,
                item.AppearedInOriginalSampleToolbox ? "Yes" : "No",
                item.SpecialFactoryDisplayName ?? "No",
                item.DependsOnDeferredSubsystem ? item.DeferredReason : "No"));
        }

        return builder.ToString();
    }

    public string CreateParityReportMarkdown()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Toolbox Parity Report");
        builder.AppendLine();
        builder.AppendLine($"Generated on {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}.");
        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine();
        builder.AppendLine($"- Original .NET 4.8 items: {this.BaselineCount}");
        builder.AppendLine($"- Current surfaced items: {this.SurfacedCount}");
        builder.AppendLine($"- Restored non-deferred items: {this.RestoredCount}");
        builder.AppendLine($"- Still-missing non-deferred items: {this.StillMissingCount}");
        builder.AppendLine();
        builder.AppendLine("## Current Surfaced Toolbox");
        builder.AppendLine();
        builder.AppendLine($"- Loaded activity/tool count: {this.SurfacedCount}");
        foreach (KeyValuePair<string, int> pair in this.GetCategoryBreakdown())
        {
            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        }

        builder.AppendLine();
        builder.AppendLine("## Present And Working");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.PresentAndWorking && row.Baseline.AppearedInOriginalSampleToolbox).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category})");
        }

        builder.AppendLine();
        builder.AppendLine("## Present But Hidden");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.PresentButHidden).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing From Toolbox Only");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.MissingFromToolboxOnly).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing Metadata Registration");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.MissingMetadataRegistration).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing Designer");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.MissingDesigner).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing Resources");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.MissingResources).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("## Intentionally Deferred");
        foreach (ToolboxDiffRow row in this.diffRows.Where(static row => row.Status == ToolboxDiffStatus.IntentionallyDeferred).OrderBy(static row => row.Baseline.ActivityName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {row.Baseline.ActivityName} ({row.Baseline.Category}) — {row.Reason}");
        }

        return builder.ToString();
    }

    private static Dictionary<string, Type> DiscoverRuntimeTypes(string startupWorkflowPath)
    {
        Dictionary<string, Type> types = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (Assembly assembly in GetCandidateAssemblies(startupWorkflowPath))
        {
            foreach (Type type in GetExportedTypes(assembly))
            {
                if (!string.IsNullOrWhiteSpace(type.FullName) && !types.ContainsKey(type.FullName))
                {
                    types.Add(type.FullName, type);
                }
            }
        }

        return types;
    }

    private static IEnumerable<Assembly> GetCandidateAssemblies(string startupWorkflowPath)
    {
        Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        void AddAssembly(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                return;
            }

            string key;
            try
            {
                key = string.IsNullOrWhiteSpace(assembly.Location) ? assembly.FullName : assembly.Location;
            }
            catch
            {
                key = assembly.FullName;
            }

            if (!string.IsNullOrWhiteSpace(key) && !assemblies.ContainsKey(key))
            {
                assemblies.Add(key, assembly);
            }
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddAssembly(assembly);
        }

        foreach (Assembly assembly in new[] { typeof(Activity).Assembly, typeof(ForEachWithBodyFactory<object>).Assembly })
        {
            AddAssembly(assembly);
        }

        foreach (string assemblyPath in GetCandidateAssemblyPaths(startupWorkflowPath))
        {
            try
            {
                AddAssembly(Assembly.LoadFrom(assemblyPath));
            }
            catch (Exception exception) when (exception is BadImageFormatException || exception is FileLoadException || exception is FileNotFoundException)
            {
            }
        }

        return assemblies.Values;
    }

    private static IEnumerable<string> GetCandidateAssemblyPaths(string startupWorkflowPath)
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddDllsFromDirectory(string directory, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string dllPath in Directory.EnumerateFiles(directory, "*.dll", searchOption))
            {
                if (dllPath.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                paths.Add(dllPath);
            }
        }

        AddDllsFromDirectory(AppContext.BaseDirectory, false);

        if (!string.IsNullOrWhiteSpace(startupWorkflowPath))
        {
            AddDllsFromDirectory(Path.GetDirectoryName(startupWorkflowPath), false);
        }

        string solutionRoot = FindSolutionRoot();
        if (!string.IsNullOrWhiteSpace(solutionRoot))
        {
            foreach (string binDirectory in Directory.EnumerateDirectories(solutionRoot, "bin", SearchOption.AllDirectories))
            {
                AddDllsFromDirectory(binDirectory, true);
            }
        }

        return paths;
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DesignerPort.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type != null);
        }
    }

    private static IReadOnlyList<BaselineToolboxItem> BuildNet48Baseline(IReadOnlyDictionary<string, Type> discoveredTypes)
    {
        List<BaselineToolboxItem> items = new List<BaselineToolboxItem>();

        AddSample(items, "AddValidationError", "System.Activities", "Validation", typeof(System.Activities.Validation.AddValidationError).FullName, null, false, null);
        AddSample(items, "AndAlso", "System.Activities", "Flow Control", typeof(System.Activities.Expressions.AndAlso).FullName, null, false, null);
        AddSample(items, "AssertValidation", "System.Activities", "Validation", typeof(System.Activities.Validation.AssertValidation).FullName, null, false, null);
        AddSample(items, "Assign", "System.Activities", "Primitives", typeof(Assign).FullName, null, false, null);
        AddSample(items, "CancellationScope", "System.Activities", "Compensation & Transactions", typeof(CancellationScope).FullName, null, false, null);
        AddSample(items, "CompensableActivity", "System.Activities", "Compensation & Transactions", typeof(CompensableActivity).FullName, null, false, null);
        AddSample(items, "Compensate", "System.Activities", "Compensation & Transactions", typeof(Compensate).FullName, null, false, null);
        AddSample(items, "Confirm", "System.Activities", "Compensation & Transactions", typeof(Confirm).FullName, null, false, null);
        AddSample(items, "CreateBookmarkScope", "System.Activities", "Runtime", typeof(CreateBookmarkScope).FullName, null, false, null);
        AddSample(items, "Delay", "System.Activities", "Primitives", typeof(Delay).FullName, null, false, null);
        AddSample(items, "DeleteBookmarkScope", "System.Activities", "Runtime", typeof(DeleteBookmarkScope).FullName, null, false, null);
        AddSample(items, "DoWhile", "System.Activities", "Flow Control", typeof(DoWhile).FullName, null, false, null);
        AddSample(items, "DynamicActivity", "System.Activities", "Primitives", typeof(DynamicActivity).FullName, null, false, null);
        AddSample(items, "Flowchart", "System.Activities", "Flowchart", typeof(Flowchart).FullName, null, false, null);
        AddSample(items, "FlowDecision", "System.Activities", "Flowchart", typeof(FlowDecision).FullName, null, false, null);
        AddSample(items, "FlowStep", "System.Activities", "Flowchart", typeof(FlowStep).FullName, null, false, null);
        AddSample(items, "FlowSwitch<T>", "System.Activities", "Flowchart", typeof(FlowSwitch<>).FullName, typeof(string), false, null);
        AddSample(items, "ForEach<T>", "System.Activities", "Flow Control", typeof(ForEach<>).FullName, typeof(object), false, null, typeof(ForEachWithBodyFactory<object>), "ForEachWithBodyFactory<object>");
        AddSample(items, "GetChildSubtree", "System.Activities", "Validation", typeof(System.Activities.Validation.GetChildSubtree).FullName, null, false, null);
        AddSample(items, "GetParentChain", "System.Activities", "Validation", typeof(System.Activities.Validation.GetParentChain).FullName, null, false, null);
        AddSample(items, "GetWorkflowTree", "System.Activities", "Validation", typeof(System.Activities.Validation.GetWorkflowTree).FullName, null, false, null);
        AddSample(items, "If", "System.Activities", "Flow Control", typeof(If).FullName, null, false, null);
        AddSample(items, "InvokeAction", "System.Activities", "Primitives", typeof(System.Activities.Statements.InvokeAction).FullName, null, false, null);
        AddSample(items, "InvokeDelegate", "System.Activities", "Primitives", typeof(InvokeDelegate).FullName, null, false, null);
        AddSample(items, "InvokeMethod", "System.Activities", "Primitives", typeof(InvokeMethod).FullName, null, false, null);
        AddSample(items, "NoPersistScope", "System.Activities", "Runtime", typeof(NoPersistScope).FullName, null, false, null);
        AddSample(items, "OrElse", "System.Activities", "Flow Control", typeof(System.Activities.Expressions.OrElse).FullName, null, false, null);
        AddSample(items, "Parallel", "System.Activities", "Flow Control", typeof(Parallel).FullName, null, false, null);
        AddSample(items, "ParallelForEach<T>", "System.Activities", "Flow Control", typeof(ParallelForEach<>).FullName, typeof(object), false, null, typeof(ParallelForEachWithBodyFactory<object>), "ParallelForEachWithBodyFactory<object>");
        AddSample(items, "Persist", "System.Activities", "Runtime", typeof(Persist).FullName, null, false, null);
        AddSample(items, "Pick", "System.Activities", "Flow Control", typeof(Pick).FullName, null, false, null, typeof(PickWithTwoBranchesFactory), "PickWithTwoBranchesFactory");
        AddSample(items, "Rethrow", "System.Activities", "Error Handling", typeof(Rethrow).FullName, null, false, null);
        AddSample(items, "Sequence", "System.Activities", "Flow Control", typeof(Sequence).FullName, null, false, null);
        AddSample(items, "Switch<T>", "System.Activities", "Flow Control", typeof(Switch<>).FullName, typeof(string), false, null);
        AddSample(items, "TerminateWorkflow", "System.Activities", "Error Handling", typeof(TerminateWorkflow).FullName, null, false, null);
        AddSample(items, "Throw", "System.Activities", "Error Handling", typeof(Throw).FullName, null, false, null);
        AddSample(items, "TransactionScope", "System.Activities", "Compensation & Transactions", typeof(TransactionScope).FullName, null, false, null);
        AddSample(items, "TryCatch", "System.Activities", "Error Handling", typeof(TryCatch).FullName, null, false, null);
        AddSample(items, "While", "System.Activities", "Flow Control", typeof(While).FullName, null, false, null);
        AddSample(items, "WriteLine", "System.Activities", "Primitives", typeof(WriteLine).FullName, null, false, null);

        AddDeferred(items, "Send", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.Send", MessagingDeferredReason);
        AddDeferred(items, "Receive", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.Receive", MessagingDeferredReason);
        AddDeferred(items, "SendReply", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.SendReply", MessagingDeferredReason);
        AddDeferred(items, "ReceiveReply", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.ReceiveReply", MessagingDeferredReason);
        AddDeferred(items, "InitializeCorrelation", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.InitializeCorrelation", MessagingDeferredReason);
        AddDeferred(items, "CorrelationScope", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.CorrelationScope", MessagingDeferredReason);
        AddDeferred(items, "TransactedReceiveScope", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.TransactedReceiveScope", MessagingDeferredReason);
        AddDeferred(items, "Service", "System.ServiceModel.Activities", "Messaging", "System.ServiceModel.Activities.Service", MessagingDeferredReason);

        AddSample(items, "StateMachine", "System.Activities", "State Machine", typeof(StateMachine).FullName, null, false, null, typeof(StateMachineWithInitialStateFactory), "StateMachineWithInitialStateFactory");
        AddSample(items, "State", "System.Activities", "State Machine", typeof(State).FullName, null, false, null);
        AddSample(items, "Transition", "System.Activities", "State Machine", typeof(Transition).FullName, null, false, null);
        AddSample(items, "FinalState", "System.Activities.Core.Presentation", "State Machine", "System.Activities.Core.Presentation.FinalState", null, false, null);

        return items;

        static void AddSample(
            ICollection<BaselineToolboxItem> items,
            string activityName,
            string sourceAssembly,
            string category,
            string runtimeTypeName,
            Type genericArgument,
            bool dependsOnDeferredSubsystem,
            string deferredReason,
            Type factoryType = null,
            string factoryDisplayName = null)
        {
            items.Add(new BaselineToolboxItem(activityName, sourceAssembly, category, true, factoryType, factoryDisplayName, dependsOnDeferredSubsystem, deferredReason, runtimeTypeName, genericArgument));
        }

        static void AddDeferred(ICollection<BaselineToolboxItem> items, string activityName, string sourceAssembly, string category, string runtimeTypeName, string deferredReason)
        {
            items.Add(new BaselineToolboxItem(activityName, sourceAssembly, category, true, null, null, true, deferredReason, runtimeTypeName, null));
        }
    }

    private static IReadOnlyList<WorkflowToolboxEntry> BuildSurfacedEntries(IReadOnlyList<BaselineToolboxItem> baseline, IReadOnlyDictionary<string, Type> discoveredTypes)
    {
        Dictionary<string, WorkflowToolboxEntry> entries = new Dictionary<string, WorkflowToolboxEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (BaselineToolboxItem item in baseline.Where(static item => item.AppearedInOriginalSampleToolbox && !item.DependsOnDeferredSubsystem))
        {
            if (!string.IsNullOrWhiteSpace(item.RuntimeTypeName) && !discoveredTypes.ContainsKey(item.RuntimeTypeName))
            {
                continue;
            }

            if (item.FactoryType != null)
            {
                entries[item.ActivityName] = WorkflowToolboxEntry.ForFactory(item.FactoryType, item.Category, item.ActivityName);
                continue;
            }

            Type runtimeType = ResolveRuntimeType(item, discoveredTypes);
            if (runtimeType == null)
            {
                continue;
            }

            entries[item.ActivityName] = WorkflowToolboxEntry.ForType(runtimeType, item.Category, item.ActivityName);
        }

        foreach (Type customType in discoveredTypes.Values.Where(IsCustomToolboxActivity).OrderBy(static type => type.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (entries.ContainsKey(customType.Name))
            {
                continue;
            }

            entries[customType.Name] = WorkflowToolboxEntry.ForType(customType, GetCustomCategory(customType), customType.Name);
        }

        return entries.Values.ToList();
    }

    private static bool IsCustomToolboxActivity(Type type)
    {
        if (type == null || string.IsNullOrWhiteSpace(type.FullName))
        {
            return false;
        }

        if (!type.IsPublic || type.IsAbstract || type.ContainsGenericParameters)
        {
            return false;
        }

        if (!typeof(Activity).IsAssignableFrom(type))
        {
            return false;
        }

        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        if (assemblyName.StartsWith("System.Activities", StringComparison.OrdinalIgnoreCase)
            || assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            || assemblyName.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase)
            || assemblyName.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase)
            || assemblyName.StartsWith("UiPath.Workflow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    private static string GetCustomCategory(Type type)
    {
        string assemblyName = type.Assembly.GetName().Name;
        return string.IsNullOrWhiteSpace(assemblyName) ? "Custom Activities" : $"Custom Activities ({assemblyName})";
    }
    private static IReadOnlyList<ToolboxDiffRow> BuildStrictDiff(
        IReadOnlyList<BaselineToolboxItem> baseline,
        IReadOnlyList<WorkflowToolboxEntry> surfacedEntries,
        IReadOnlyDictionary<string, Type> discoveredTypes)
    {
        HashSet<string> surfacedNames = new HashSet<string>(surfacedEntries.Select(static entry => entry.DisplayName), StringComparer.OrdinalIgnoreCase);
        List<ToolboxDiffRow> rows = new List<ToolboxDiffRow>();

        foreach (BaselineToolboxItem item in baseline.Where(static item => item.AppearedInOriginalSampleToolbox))
        {
            if (item.DependsOnDeferredSubsystem)
            {
                rows.Add(new ToolboxDiffRow(item, ToolboxDiffStatus.IntentionallyDeferred, item.DeferredReason));
                continue;
            }

            Type runtimeType = ResolveRuntimeType(item, discoveredTypes);
            if (runtimeType == null)
            {
                rows.Add(new ToolboxDiffRow(item, ToolboxDiffStatus.MissingDesigner, "The runtime type was not found in the scanned modern assemblies for the port."));
                continue;
            }

            if (!surfacedNames.Contains(item.ActivityName))
            {
                rows.Add(new ToolboxDiffRow(item, ToolboxDiffStatus.MissingFromToolboxOnly, "The runtime type exists but was not surfaced into the toolbox."));
                continue;
            }

            rows.Add(new ToolboxDiffRow(item, ToolboxDiffStatus.PresentAndWorking, "Surfaced in toolbox."));
        }

        return rows;
    }

    private static Type ResolveRuntimeType(BaselineToolboxItem item, IReadOnlyDictionary<string, Type> discoveredTypes)
    {
        if (string.IsNullOrWhiteSpace(item.RuntimeTypeName))
        {
            return null;
        }

        Type runtimeType;
        if (!discoveredTypes.TryGetValue(item.RuntimeTypeName, out runtimeType))
        {
            return null;
        }

        if (item.GenericArgument != null)
        {
            return runtimeType.MakeGenericType(item.GenericArgument);
        }

        return runtimeType;
    }

    internal sealed class WorkflowToolboxEntry
    {
        private readonly Type toolType;
        private readonly bool useFactory;

        private WorkflowToolboxEntry(Type toolType, string category, string displayName, bool useFactory)
        {
            this.toolType = toolType;
            this.Category = category;
            this.DisplayName = displayName;
            this.useFactory = useFactory;
        }

        public string Category { get; }

        public string DisplayName { get; }

        public string ToolName => this.toolType.FullName;

        public static WorkflowToolboxEntry ForType(Type toolType, string category, string displayName)
            => new WorkflowToolboxEntry(toolType, category, displayName, false);

        public static WorkflowToolboxEntry ForFactory(Type factoryType, string category, string displayName)
            => new WorkflowToolboxEntry(factoryType, category, displayName, true);

        public ToolboxItemWrapper CreateToolboxItemWrapper()
            => new ToolboxItemWrapper(this.toolType, this.DisplayName);

        public bool TryCreateProbeActivity(out Activity activity, out string reason)
        {
            activity = null;
            reason = null;

            try
            {
                if (this.useFactory)
                {
                    IActivityTemplateFactory factory = (IActivityTemplateFactory)Activator.CreateInstance(this.toolType);
                    activity = factory.Create(null);
                    return activity != null;
                }

                if (!typeof(Activity).IsAssignableFrom(this.toolType))
                {
                    reason = "Not a root Activity-derived type.";
                    return false;
                }

                activity = (Activity)Activator.CreateInstance(this.toolType);
                return activity != null;
            }
            catch (Exception exception)
            {
                reason = exception.ToString();
                return false;
            }
        }
    }

    private sealed class BaselineToolboxItem
    {
        public BaselineToolboxItem(
            string activityName,
            string sourceAssembly,
            string category,
            bool appearedInOriginalSampleToolbox,
            Type factoryType,
            string specialFactoryDisplayName,
            bool dependsOnDeferredSubsystem,
            string deferredReason,
            string runtimeTypeName,
            Type genericArgument)
        {
            this.ActivityName = activityName;
            this.SourceAssembly = sourceAssembly;
            this.Category = category;
            this.AppearedInOriginalSampleToolbox = appearedInOriginalSampleToolbox;
            this.FactoryType = factoryType;
            this.SpecialFactoryDisplayName = specialFactoryDisplayName;
            this.DependsOnDeferredSubsystem = dependsOnDeferredSubsystem;
            this.DeferredReason = deferredReason;
            this.RuntimeTypeName = runtimeTypeName;
            this.GenericArgument = genericArgument;
        }

        public string ActivityName { get; }

        public string SourceAssembly { get; }

        public string Category { get; }

        public bool AppearedInOriginalSampleToolbox { get; }

        public Type FactoryType { get; }

        public string SpecialFactoryDisplayName { get; }

        public bool DependsOnDeferredSubsystem { get; }

        public string DeferredReason { get; }

        public string RuntimeTypeName { get; }

        public Type GenericArgument { get; }
    }

    private sealed class ToolboxDiffRow
    {
        public ToolboxDiffRow(BaselineToolboxItem baseline, ToolboxDiffStatus status, string reason)
        {
            this.Baseline = baseline;
            this.Status = status;
            this.Reason = reason;
        }

        public BaselineToolboxItem Baseline { get; }

        public ToolboxDiffStatus Status { get; }

        public string Reason { get; }
    }

    private enum ToolboxDiffStatus
    {
        PresentAndWorking,
        PresentButHidden,
        MissingFromToolboxOnly,
        MissingMetadataRegistration,
        MissingDesigner,
        MissingResources,
        IntentionallyDeferred,
    }
}


