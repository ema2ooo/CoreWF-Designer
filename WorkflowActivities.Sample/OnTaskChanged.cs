using WorkflowActivities.Sample;
using System;
using System.Activities;
using System.ComponentModel;

namespace RERD.Workflow.Activities
{
    public sealed class OnTaskChanged : NativeActivity<string>
    {
        [RequiredArgument]
        public string BookmarkName { get; set; } = string.Empty;

        protected override bool CanInduceIdle => true;

        protected override void Cancel(NativeActivityContext context)
        {
            base.Cancel(context);
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.CreateBookmark(BookmarkName, new BookmarkCallback(OnDataReceived));
            SaveBookmarkInfo(context, BookmarkName);
        }

        private void OnDataReceived(NativeActivityContext context, Bookmark bookmark, object data)
        {
            if (data is WorkflowData workflowData)
            {
                Result.Set(context, workflowData.Action);
            }
        }

        public void SaveBookmarkInfo(NativeActivityContext context, string bookmarkName)
        {
            Console.WriteLine($"bookmark {bookmarkName} is saved");
        }
    }
}

