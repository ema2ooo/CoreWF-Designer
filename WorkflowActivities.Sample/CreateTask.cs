using System.Activities;

namespace RERD.Workflow.Activities
{
    public sealed class CreateTask : NativeActivity<string>
    {
        [RequiredArgument]
        public InArgument<string> TargetName { get; set; } = null!;

        [RequiredArgument]
        public string WorkflowStatusCode { get; set; } = string.Empty;

        protected override void Execute(NativeActivityContext context)
        {

        }

        private void DispatchToEntity(NativeActivityContext context)
        {
        }

        private void DispatchToTargetEntity(NativeActivityContext context)
        {
        }

        private void DispatchToIndividual(NativeActivityContext context)
        {
        }

        private void DispatchToGroupOrRole(NativeActivityContext context)
        {
        }
    }
}
