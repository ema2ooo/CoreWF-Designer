using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Concurrent;

#nullable disable

namespace WorkflowRunner.Sample
{
    public static class IdentityManager
    {
        private static readonly object SyncRoot = new object();
        private static ConcurrentDictionary<WorkflowIdentity, DynamicActivity> ActivityMap = new();

        public static WorkflowIdentity CurrentIdentity { get; private set; }

        public static string WorkflowDirectory { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Workflows");

        static IdentityManager()
        {
            Reload();
        }

        public static void Reload(string workflowDirectory = null)
        {
            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(workflowDirectory))
                {
                    WorkflowDirectory = Path.GetFullPath(workflowDirectory);
                }

                Directory.CreateDirectory(WorkflowDirectory);
                ActivityMap = new ConcurrentDictionary<WorkflowIdentity, DynamicActivity>();

                foreach (var workflowFile in Directory.GetFiles(WorkflowDirectory, "*.xaml", SearchOption.TopDirectoryOnly))
                {
                    AddActivity(workflowFile);
                }
            }
        }

        private static void AddActivity(string workflowFilePath)
        {
            using var stream = File.OpenRead(workflowFilePath);
            var settings = new ActivityXamlServicesSettings
            {
                CompileExpressions = true
            };

            if (ActivityXamlServices.Load(stream, settings) is not DynamicActivity workflow)
            {
                return;
            }

            var identity = CreateWorkflowIdentity(Path.GetFileName(workflowFilePath));
            ActivityMap[identity] = workflow;
        }

        private static WorkflowIdentity CreateWorkflowIdentity(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var workflowName = stem;
            var version = new Version(1, 0);

            var dashIndex = stem.LastIndexOf('-');
            if (dashIndex > 0)
            {
                var possibleVersion = stem[(dashIndex + 1)..];
                if (Version.TryParse(possibleVersion, out var parsedVersion))
                {
                    workflowName = stem[..dashIndex];
                    version = parsedVersion;
                }
            }

            return new WorkflowIdentity(workflowName, version, null);
        }

        public static DynamicActivity GetWorkflowActivity(string workflowName)
        {
            if (string.IsNullOrWhiteSpace(workflowName))
            {
                return null;
            }

            var candidate = ActivityMap
                .Where(entry => string.Equals(entry.Key.Name, workflowName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.Key.Version)
                .FirstOrDefault();

            if (candidate.Key is null)
            {
                return null;
            }

            CurrentIdentity = candidate.Key;
            return candidate.Value;
        }

        public static DynamicActivity GetWorkflowDefinition(WorkflowIdentity identity)
        {
            return identity is not null && ActivityMap.TryGetValue(identity, out var activity)
                ? activity
                : null;
        }
    }
}

