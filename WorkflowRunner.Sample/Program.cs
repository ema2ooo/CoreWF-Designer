using System.Activities;
using System.Threading;
using WorkflowActivities.Sample;
using WorkflowRunner.Sample;

var workflowArgument = args.FirstOrDefault();
var workflowFileName = string.IsNullOrWhiteSpace(workflowArgument) ? "myworkflow.xaml" : workflowArgument;
var workflowPath = ResolveWorkflowPath(workflowFileName);
var workflowDirectory = Path.GetDirectoryName(workflowPath)!;
var workflowLookupName = GetWorkflowLookupName(workflowPath);

IdentityManager.Reload(workflowDirectory);
var workflow = IdentityManager.GetWorkflowActivity(workflowLookupName);
if (workflow is null)
{
    Console.Error.WriteLine($"Workflow '{workflowLookupName}' could not be loaded from '{workflowDirectory}'.");
    return 1;
}

Console.WriteLine($"Loaded workflow: {Path.GetFileName(workflowPath)}");
Console.WriteLine("Type a bookmark name to resume it, 'list' to show current bookmarks, or 'exit' to quit.");
Console.WriteLine();

var idleSignal = new AutoResetEvent(false);
var activeBookmarks = new List<string>();
var workflowCompleted = false;
var exitCode = 0;

var application = new WorkflowApplication(workflow);
application.Completed = eventArgs =>
{
    workflowCompleted = true;
    Console.WriteLine();
    Console.WriteLine($"Workflow completed with state: {eventArgs.CompletionState}");
    idleSignal.Set();
};
application.Aborted = eventArgs =>
{
    workflowCompleted = true;
    exitCode = 2;
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Workflow aborted: {eventArgs.Reason}");
    idleSignal.Set();
};
application.OnUnhandledException = eventArgs =>
{
    workflowCompleted = true;
    exitCode = 3;
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Unhandled workflow exception: {eventArgs.UnhandledException}");
    idleSignal.Set();
    return UnhandledExceptionAction.Terminate;
};
application.Idle = eventArgs =>
{
    activeBookmarks = eventArgs.Bookmarks
        .Select(bookmark => bookmark.BookmarkName)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Workflow idle. Bookmarks: {(activeBookmarks.Count == 0 ? "<none>" : string.Join(", ", activeBookmarks))}");
    idleSignal.Set();
};

application.Run();

while (!workflowCompleted)
{
    idleSignal.WaitOne();
    if (workflowCompleted)
    {
        break;
    }

    while (!workflowCompleted)
    {
        Console.Write("> ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Exiting without resuming another bookmark.");
            return exitCode;
        }

        if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(activeBookmarks.Count == 0
                ? "No active bookmarks."
                : $"Active bookmarks: {string.Join(", ", activeBookmarks)}");
            continue;
        }

        var bookmarkName = activeBookmarks.FirstOrDefault(name => string.Equals(name, input, StringComparison.OrdinalIgnoreCase));
        if (bookmarkName is null)
        {
            Console.WriteLine($"Unknown bookmark '{input}'.");
            continue;
        }

        var result = application.ResumeBookmark(
            bookmarkName,
            new WorkflowData(new Dictionary<string, object>(), bookmarkName));

        Console.WriteLine($"ResumeBookmark('{bookmarkName}') => {result}");
        if (result == BookmarkResumptionResult.Success)
        {
            break;
        }
    }
}

return exitCode;

static string ResolveWorkflowPath(string workflowArgument)
{
    if (Path.IsPathRooted(workflowArgument) && File.Exists(workflowArgument))
    {
        return Path.GetFullPath(workflowArgument);
    }

    if (File.Exists(workflowArgument))
    {
        return Path.GetFullPath(workflowArgument);
    }

    var baseDirectory = AppContext.BaseDirectory;
    var workflowsDirectory = Path.Combine(baseDirectory, "Workflows");
    Directory.CreateDirectory(workflowsDirectory);

    var candidateInWorkflows = Path.Combine(workflowsDirectory, Path.GetFileName(workflowArgument));
    if (File.Exists(candidateInWorkflows))
    {
        return candidateInWorkflows;
    }

    throw new FileNotFoundException($"Workflow file '{workflowArgument}' was not found.");
}

static string GetWorkflowLookupName(string workflowPath)
{
    var stem = Path.GetFileNameWithoutExtension(workflowPath);
    var dashIndex = stem.LastIndexOf('-');
    if (dashIndex > 0 && Version.TryParse(stem[(dashIndex + 1)..], out _))
    {
        return stem[..dashIndex];
    }

    return stem;
}

