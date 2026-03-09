using System.Activities;
using System.Activities.Validation;
using System.Collections.Generic;

namespace System.ServiceModel.Activities
{
    public class WorkflowService : Activity
    {
        public Activity Body { get; set; }

        public ValidationResults Validate(ValidationSettings settings) => new(new List<ValidationError>());

        public Activity GetWorkflowRoot() => Body;
    }

    public class Send : Activity { }
    public class Receive : Activity { }
    public class SendReply : Activity { }
    public class ReceiveReply : Activity { }
}
