using System;
using System.Collections.Generic;
using System.Text;

namespace WorkflowActivities.Sample
{
    public class WorkflowData
    {
        public Dictionary<string, object> Arguments { get; set; }
        public string Action { get; set; }

        public WorkflowData(Dictionary<string, object> args, string action)
        {
            Action = action;
            Arguments = args;
        }
    }
}

