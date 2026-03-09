using System;
using System.Activities.Statements.Tracking;
using System.Activities.Tracking;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkflowRunner.Sample.InstanceStore
{
    public class TypedTrackingParticipant : TrackingParticipant
    {
        // Methods
        protected virtual void Track(StateMachineStateRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(ActivityScheduledRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(ActivityStateRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(BookmarkResumptionRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(CancelRequestedRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(CustomTrackingRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(FaultPropagationRecord record, TimeSpan timeout)
        {
        }

        protected override void Track(TrackingRecord record, TimeSpan timeout)
        {
            if (record is ActivityStateRecord)
            {
                this.Track((ActivityStateRecord)record, timeout);
            }
            else if (record is ActivityScheduledRecord)
            {
                this.Track((ActivityScheduledRecord)record, timeout);
            }
            else if (record is BookmarkResumptionRecord)
            {
                this.Track((BookmarkResumptionRecord)record, timeout);
            }
            else if (record is CancelRequestedRecord)
            {
                this.Track((CancelRequestedRecord)record, timeout);
            }
            else if (record is StateMachineStateRecord)
            {
                this.Track((StateMachineStateRecord)record, timeout);
            }
          
            else if (record is CustomTrackingRecord)
            {
                this.Track((CustomTrackingRecord)record, timeout);
            }
            else if (record is FaultPropagationRecord)
            {
                this.Track((FaultPropagationRecord)record, timeout);
            }
            else if (record is WorkflowInstanceAbortedRecord)
            {
                this.Track((WorkflowInstanceAbortedRecord)record, timeout);
            }
            else if (record is WorkflowInstanceSuspendedRecord)
            {
                this.Track((WorkflowInstanceSuspendedRecord)record, timeout);
            }
            else if (record is WorkflowInstanceTerminatedRecord)
            {
                this.Track((WorkflowInstanceTerminatedRecord)record, timeout);
            }
            else if (record is WorkflowInstanceUnhandledExceptionRecord)
            {
                this.Track((WorkflowInstanceUnhandledExceptionRecord)record, timeout);
            }
            else
            {
                if (!(record is WorkflowInstanceRecord))
                {
                    throw new NotImplementedException(string.Format("There is no track handler for type {0}", record.GetType().Name));
                }
                this.Track((WorkflowInstanceRecord)record, timeout);
            }
        }

        protected virtual void Track(WorkflowInstanceAbortedRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(WorkflowInstanceRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(WorkflowInstanceSuspendedRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(WorkflowInstanceTerminatedRecord record, TimeSpan timeout)
        {
        }

        protected virtual void Track(WorkflowInstanceUnhandledExceptionRecord record, TimeSpan timeout)
        {
        }

        
    }

}

