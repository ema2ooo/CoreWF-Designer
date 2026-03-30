using RehostedDesigner.Port;
using System;
using System.Activities.Statements;
using System.Activities.Presentation.Hosting;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DesignerPort.Smoke;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return RunOnStaThread(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int RunOnStaThread(string[] args)
    {
        Exception failure = null;
        int exitCode = 0;
        var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                ExecuteSmokeChecks(args);
            }
            catch (Exception exception)
            {
                failure = exception;
                exitCode = 1;
            }
            finally
            {
                completed.Set();
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }

            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        completed.Wait();
        thread.Join();

        if (failure != null)
        {
            throw new InvalidOperationException("Smoke checks failed.", failure);
        }

        return exitCode;
    }

    private static void ExecuteSmokeChecks(string[] args)
    {
        var app = Application.Current ?? new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        var window = new MainWindow();
        window.Show();
        DoEvents();

        if (args != null && args.Length > 0)
        {
            window.LoadFromFile(args[0]);
            DoEvents();
            Console.WriteLine("Load target: " + args[0]);
            Console.WriteLine("Designer error state: " + window.CurrentLoadHasErrors);
            if (!string.IsNullOrWhiteSpace(window.CurrentLoadDiagnostics))
            {
                Console.WriteLine("---LOAD DIAGNOSTICS---");
                Console.WriteLine(window.CurrentLoadDiagnostics);
            }

            window.Close();
            app.Shutdown();
            return;
        }


        Assert(window.MetadataRegistered, "Designer metadata was not registered.");
        Assert(window.CurrentDesigner != null, "WorkflowDesigner was not created.");
        Assert(window.CurrentDesignerView != null, "WorkflowDesigner.View was not hosted.");
        Assert(window.CurrentPropertyInspectorView != null, "WorkflowDesigner.PropertyInspectorView was not hosted.");
        var expressionEditorService = window.CurrentDesigner.Context.Services.GetService<IExpressionEditorService>();
        Assert(expressionEditorService != null, "IExpressionEditorService was not registered for the rehosted designer.");
        var assemblyContext = window.CurrentDesigner.Context.Items.GetValue<AssemblyContextControlItem>();
        Assert(assemblyContext != null, "Assembly context was not initialized for the rehosted designer.");
        var importedNamespaces = window.CurrentDesigner.Context.Items.GetValue<ImportedNamespaceContextItem>();
        Assert(importedNamespaces != null, "Imported namespace context was not initialized for the rehosted designer.");
        var expressionEditor = expressionEditorService.CreateExpressionEditor(assemblyContext, importedNamespaces, new System.Collections.Generic.List<ModelItem>(), "DateTime.", typeof(string));
        Assert(expressionEditor?.HostControl != null, "The rehosted expression editor service did not create a host control.");
        expressionEditor?.Close();
        window.SelectFirstSequenceActivity();
        DoEvents();
        Assert(window.CurrentPropertyInspectorView != null, "Property inspector view was lost after selecting the first sequence activity.");
        Assert(window.CurrentToolbox != null, "Toolbox was not initialized.");
        Assert(window.LoadedActivityCount > 7, "Toolbox did not expand beyond the MVP hard-coded activity set.");
        Assert(window.ToolboxContainsTool(typeof(Sequence).FullName), "Toolbox does not contain Sequence.");
        Assert(window.ToolboxContainsTool("While"), "Toolbox does not contain While.");
        Assert(window.ToolboxContainsTool("DoWhile"), "Toolbox does not contain DoWhile.");
        Assert(window.ToolboxContainsTool("Parallel"), "Toolbox does not contain Parallel.");
        Assert(window.ToolboxContainsTool("Pick"), "Toolbox does not contain Pick.");
        Assert(window.ToolboxContainsTool("TryCatch"), "Toolbox does not contain TryCatch.");
        Assert(window.ToolboxContainsTool("AddValidationError"), "Toolbox does not contain AddValidationError.");
        Assert(window.ToolboxContainsTool("AndAlso"), "Toolbox does not contain AndAlso.");
        Assert(window.ToolboxContainsTool("AssertValidation"), "Toolbox does not contain AssertValidation.");
        Assert(window.ToolboxContainsTool("CreateBookmarkScope"), "Toolbox does not contain CreateBookmarkScope.");
        Assert(window.ToolboxContainsTool("DeleteBookmarkScope"), "Toolbox does not contain DeleteBookmarkScope.");
        Assert(window.ToolboxContainsTool("DynamicActivity"), "Toolbox does not contain DynamicActivity.");
        Assert(window.ToolboxContainsTool("GetChildSubtree"), "Toolbox does not contain GetChildSubtree.");
        Assert(window.ToolboxContainsTool("GetParentChain"), "Toolbox does not contain GetParentChain.");
        Assert(window.ToolboxContainsTool("GetWorkflowTree"), "Toolbox does not contain GetWorkflowTree.");
        Assert(window.ToolboxContainsTool("InvokeAction"), "Toolbox does not contain InvokeAction.");
        Assert(window.ToolboxContainsTool("OrElse"), "Toolbox does not contain OrElse.");
        Assert(window.ToolboxContainsTool("StateMachine"), "Toolbox does not contain StateMachine.");
        Assert(window.ToolboxContainsTool("State"), "Toolbox does not contain State.");
        Assert(window.ToolboxContainsTool("Transition"), "Toolbox does not contain Transition.");
        Assert(window.ToolboxContainsTool("FinalState"), "Toolbox does not contain FinalState.");
        Assert(window.ToolboxContainsTool("InvokeMethod"), "Toolbox does not contain InvokeMethod.");
        Assert(window.ToolboxContainsTool("CancellationScope"), "Toolbox does not contain CancellationScope.");
        Assert(window.ToolboxContainsTool("CreateTask"), "Toolbox does not contain CreateTask from the custom activity assembly.");
        Assert(window.ToolboxContainsTool("TransactionScope"), "Toolbox does not contain TransactionScope.");
        Assert(window.ToolboxContainsTool("FlowSwitch<T>"), "Toolbox does not contain FlowSwitch<T>.");
        Assert(window.GetToolboxCategoryBreakdown().Count >= 5, "Toolbox category breakdown is unexpectedly small.");
        Assert(File.Exists(window.ToolboxParityReportPath), "Toolbox parity report was not generated.");
        Assert(File.Exists(window.ToolboxBaselinePath), "Toolbox baseline report was not generated.");
        var parityReport = File.ReadAllText(window.ToolboxParityReportPath);
        var baselineReport = File.ReadAllText(window.ToolboxBaselinePath);
        Assert(baselineReport.Contains("AddValidationError", StringComparison.Ordinal), "Baseline report does not include AddValidationError.");
        Assert(baselineReport.Contains("StateMachine", StringComparison.Ordinal), "Baseline report does not include StateMachine.");
        Assert(parityReport.Contains("Loaded activity/tool count", StringComparison.Ordinal), "Toolbox parity report did not include the loaded count.");
        Assert(parityReport.Contains("## Missing From Toolbox Only", StringComparison.Ordinal), "Toolbox parity report did not include the strict missing section.");
        Assert(parityReport.Contains("## Intentionally Deferred", StringComparison.Ordinal), "Toolbox parity report did not include the deferred section.");

        Assert(window.TryCreateProbeActivity("While", out var whileActivity, out var whileReason) && whileActivity != null, $"While probe creation failed: {whileReason}");
        Assert(window.TryCreateProbeActivity("Parallel", out var parallelActivity, out var parallelReason) && parallelActivity != null, $"Parallel probe creation failed: {parallelReason}");
        Assert(window.TryCreateProbeActivity("Pick", out var pickActivity, out var pickReason) && pickActivity != null, $"Pick probe creation failed: {pickReason}");
        Assert(window.TryCreateProbeActivity("Flowchart", out var flowchartActivity, out var flowchartReason) && flowchartActivity != null, $"Flowchart probe creation failed: {flowchartReason}");
        Assert(window.TryCreateProbeActivity("AddValidationError", out var addValidationErrorActivity, out var addValidationErrorReason) && addValidationErrorActivity != null, $"AddValidationError probe creation failed: {addValidationErrorReason}");
        Assert(window.TryCreateProbeActivity("DynamicActivity", out var dynamicActivity, out var dynamicActivityReason) && dynamicActivity != null, $"DynamicActivity probe creation failed: {dynamicActivityReason}");
        Assert(window.GetCurrentDesignerText().Contains("Sequence", StringComparison.Ordinal), "Default sequence did not load correctly.");

        Assert(window.TryCreateProbeActivity("InvokeAction", out var invokeActionActivity, out var invokeActionReason) && invokeActionActivity != null, $"InvokeAction probe creation failed: {invokeActionReason}");
        Assert(window.TryCreateProbeActivity("StateMachine", out var stateMachineActivity, out var stateMachineReason) && stateMachineActivity != null, $"StateMachine probe creation failed: {stateMachineReason}");

        window.LoadSimpleStateMachine();
        DoEvents();
        Assert(window.GetCurrentDesignerText().Contains("StateMachine", StringComparison.Ordinal), "Simple state machine did not load correctly.");

        window.LoadFlowchartSample();
        DoEvents();
        Assert(window.GetCurrentDesignerText().Contains("Flowchart", StringComparison.Ordinal), "Flowchart sample did not load correctly.");

        var roundTripDirectory = Path.Combine(Path.GetTempPath(), "DesignerPort.Smoke");
        Directory.CreateDirectory(roundTripDirectory);
        var roundTripPath = Path.Combine(roundTripDirectory, "roundtrip.xaml");

        window.SaveToFile(roundTripPath);
        Assert(File.Exists(roundTripPath), "Round-trip save did not create the output XAML file.");
        var savedText = File.ReadAllText(roundTripPath);
        Assert(savedText.Contains("Flowchart", StringComparison.Ordinal), "Saved XAML does not contain the loaded flowchart definition.");

        window.LoadFromFile(roundTripPath);
        DoEvents();
        Assert(window.GetCurrentDesignerText().Contains("Flowchart", StringComparison.Ordinal), "Reloading the saved XAML did not restore the flowchart.");

        window.LoadSequence();
        DoEvents();
        Assert(window.GetCurrentDesignerText().Contains("Sequence", StringComparison.Ordinal), "Reloading the default sequence failed.");

        window.Close();
        app.Shutdown();

        Console.WriteLine("Smoke checks passed.");
    }

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(static f =>
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }), frame);
        Dispatcher.PushFrame(frame);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}









