using Microsoft.Win32;
using System;
using System.Activities;
using System.Activities.Core.Presentation;
using System.Activities.Presentation;
using System.Activities.Presentation.Services;
using System.Activities.Presentation.Toolbox;
using System.Activities.Presentation.View;
using System.Activities.Statements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace RehostedDesigner.Port;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private const string BaseWindowTitle = "Rehosted Workflow Designer Port";

    private readonly ToolboxControl toolbox;
    private readonly WorkflowToolboxCatalog toolboxCatalog;
    private WorkflowDesigner designer;
    private string currentFile;

    public bool MetadataRegistered { get; private set; }

    public WorkflowDesigner CurrentDesigner => designer;

    public ToolboxControl CurrentToolbox => toolbox;

    public UIElement CurrentDesignerView => DesignerHost.Child as UIElement;

    public UIElement CurrentPropertyInspectorView => PropertyHost.Child as UIElement;

    public string CurrentWorkflowFile => currentFile;

    public string CurrentLoadDiagnostics => designer?.LastLoadDiagnostics ?? string.Empty;

    public bool CurrentLoadHasErrors => designer != null && designer.IsInErrorState();

    public string FlowchartSamplePath => Path.Combine(AppContext.BaseDirectory, "Samples", "SimpleFlowchart.xaml");

    public string ToolboxParityReportPath => Path.Combine(AppContext.BaseDirectory, "TOOLBOX_PARITY_REPORT.md");

    public string ToolboxBaselinePath => Path.Combine(AppContext.BaseDirectory, "TOOLBOX_BASELINE_NET48.md");

    public string ToolboxParityWorkspaceReportPath => GetWorkspaceReportPath();

    public string ToolboxBaselineWorkspacePath => GetWorkspaceArtifactPath("TOOLBOX_BASELINE_NET48.md");

    public int LoadedActivityCount => toolboxCatalog.SurfacedCount;

    public MainWindow(string startupWorkflowPath = null)
    {
        InitializeComponent();
        UpdateWindowTitle();
        RegisterMetadata();
        toolboxCatalog = WorkflowToolboxCatalog.Create(startupWorkflowPath);
        toolbox = CreateToolbox();
        ToolboxHost.Child = toolbox;

        var baseline = toolboxCatalog.CreateBaselineMarkdown();
        File.WriteAllText(ToolboxBaselinePath, baseline);
        if (!string.IsNullOrWhiteSpace(ToolboxBaselineWorkspacePath))
        {
            File.WriteAllText(ToolboxBaselineWorkspacePath, baseline);
        }

        var report = toolboxCatalog.CreateParityReportMarkdown();
        File.WriteAllText(ToolboxParityReportPath, report);
        if (!string.IsNullOrWhiteSpace(ToolboxParityWorkspaceReportPath))
        {
            File.WriteAllText(ToolboxParityWorkspaceReportPath, report);
        }

        OpenStartupWorkflow(startupWorkflowPath);
    }

    public void RegisterMetadata()
    {
        new DesignerMetadata().Register();
        MetadataRegistered = true;
    }

    private ToolboxControl CreateToolbox()
        => toolboxCatalog.CreateToolboxControl();

    private void InitializeDesigner()
    {
        designer = new WorkflowDesigner();
        DesignerHost.Child = designer.View;
        PropertyHost.Child = designer.PropertyInspectorView;
    }

    public void LoadSequence()
    {
        currentFile = null;
        InitializeDesigner();
        designer.Load(new Sequence
        {
            Activities =
            {
                new WriteLine { Text = "Hello from the ported designer" },
                new If
                {
                    Condition = new InArgument<bool>(true),
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new WriteLine { Text = "Then branch" }
                        }
                    }
                }
            }
        });
        UpdateWindowTitle();
    }

    public void LoadFlowchartSample()
    {
        LoadFromFile(FlowchartSamplePath);
    }

    public void LoadSimpleStateMachine()
    {
        currentFile = null;
        InitializeDesigner();

        var initialState = new State
        {
            DisplayName = "State1"
        };

        designer.Load(new StateMachine
        {
            States =
            {
                initialState
            },
            InitialState = initialState
        });
        UpdateWindowTitle();
    }

    public void LoadFromFile(string path)
    {
        currentFile = path;
        InitializeDesigner();
        designer.Load(path);
        UpdateWindowTitle();
    }

    public void SaveToFile(string path)
    {
        if (designer is null)
        {
            throw new InvalidOperationException("WorkflowDesigner has not been initialized.");
        }

        currentFile = path;
        designer.Save(path);
        UpdateWindowTitle();
    }

    public string GetCurrentDesignerText()
    {
        if (designer is null)
        {
            throw new InvalidOperationException("WorkflowDesigner has not been initialized.");
        }

        designer.Flush();
        return designer.Text ?? string.Empty;
    }

    public bool ToolboxContainsTool(string toolName)
        => toolboxCatalog.Contains(toolName);

    public IReadOnlyDictionary<string, int> GetToolboxCategoryBreakdown()
        => toolboxCatalog.GetCategoryBreakdown();

    public IReadOnlyList<string> GetMissingBaselineActivities()
        => toolboxCatalog.GetMissingBaselineActivities();

    public IReadOnlyDictionary<string, string> GetDeferredBaselineActivities()
        => toolboxCatalog.GetDeferredBaselineActivities();

    public IReadOnlyList<string> GetPresentBaselineActivities()
        => toolboxCatalog.GetPresentBaselineActivities();

    public bool TryCreateProbeActivity(string displayName, out Activity activity, out string reason)
        => toolboxCatalog.TryCreateProbeActivity(displayName, out activity, out reason);

    public void SelectFirstSequenceActivity()
    {
        if (designer is null)
        {
            throw new InvalidOperationException("WorkflowDesigner has not been initialized.");
        }

        var modelService = designer.Context.Services.GetService<ModelService>();
        if (modelService?.Root is null)
        {
            throw new InvalidOperationException("WorkflowDesigner model service is not available.");
        }

        var activities = modelService.Root.Properties["Activities"]?.Collection;
        if (activities is null || activities.Count == 0)
        {
            throw new InvalidOperationException("The current workflow does not expose a selectable child activity.");
        }

        Selection.SelectOnly(designer.Context, activities[0]);
    }

    private void OpenStartupWorkflow(string startupWorkflowPath)
    {
        if (string.IsNullOrWhiteSpace(startupWorkflowPath))
        {
            LoadSequence();
            return;
        }

        try
        {
            LoadFromFile(startupWorkflowPath);
            if (CurrentLoadHasErrors)
            {
                MessageBox.Show(
                    this,
                    string.IsNullOrWhiteSpace(CurrentLoadDiagnostics)
                        ? $"Failed to open startup workflow '{startupWorkflowPath}'."
                        : CurrentLoadDiagnostics,
                    "Startup Workflow Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception exception)
        {
            UpdateWindowTitle(startupWorkflowPath);
            MessageBox.Show(
                this,
                $"Failed to open startup workflow '{startupWorkflowPath}'.{Environment.NewLine}{Environment.NewLine}{exception}",
                "Startup Workflow Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateWindowTitle()
        => UpdateWindowTitle(currentFile);

    private void UpdateWindowTitle(string workflowPath)
    {
        Title = string.IsNullOrWhiteSpace(workflowPath)
            ? BaseWindowTitle
            : $"{BaseWindowTitle} - {Path.GetFileName(workflowPath)}";
    }

    private static string GetWorkspaceReportPath()
        => GetWorkspaceArtifactPath("TOOLBOX_PARITY_REPORT.md");

    private static string GetWorkspaceArtifactPath(string fileName)
    {
        string candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", fileName));
        string parent = Path.GetDirectoryName(candidate);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
        {
            return candidate;
        }

        return null;
    }

    private void NewSequence_Click(object sender, RoutedEventArgs e)
        => LoadSequence();

    private void OpenFlowchartSample_Click(object sender, RoutedEventArgs e)
        => LoadFlowchartSample();

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Workflow XAML (*.xaml)|*.xaml"
        };

        if (dialog.ShowDialog(this) == true)
        {
            LoadFromFile(dialog.FileName);
            if (CurrentLoadHasErrors)
            {
                MessageBox.Show(
                    this,
                    string.IsNullOrWhiteSpace(CurrentLoadDiagnostics)
                        ? $"Failed to open workflow '{dialog.FileName}'."
                        : CurrentLoadDiagnostics,
                    "Workflow Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (designer is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentFile))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Workflow XAML (*.xaml)|*.xaml",
                FileName = "workflow.xaml"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            currentFile = dialog.FileName;
        }

        SaveToFile(currentFile);
    }
}

