using System;
using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace System.ServiceModel.Activities
{
    public class CorrelationHandle
    {
    }

    public abstract class CorrelationInitializer
    {
    }

    public class RequestReplyCorrelationInitializer : CorrelationInitializer
    {
        public InArgument<CorrelationHandle> CorrelationHandle { get; set; }
    }

    public class ContextCorrelationInitializer : CorrelationInitializer
    {
    }

    public class QueryCorrelationInitializer : CorrelationInitializer
    {
    }

    public class CallbackCorrelationInitializer : CorrelationInitializer
    {
    }

    public abstract class MessageContent
    {
    }

    public class SendMessageContent : MessageContent
    {
        public object Message { get; set; }
        public Type DeclaredMessageType { get; set; }
    }

    public class SendParametersContent : MessageContent
    {
        public object Parameters { get; set; } = new DynamicArgumentValueDictionary();
    }

    public class ReceiveMessageContent : MessageContent
    {
        public object Message { get; set; }
        public Type DeclaredMessageType { get; set; }
    }

    public class ReceiveParametersContent : MessageContent
    {
        public object Parameters { get; set; } = new DynamicArgumentValueDictionary();
    }

    public class MessageQuerySet
    {
    }

    public class DynamicArgumentValueDictionary : Collection<KeyValuePair<string, object>>
    {
    }

    public class CorrelationData
    {
        public InArgument<CorrelationHandle> CorrelationHandle { get; set; }
        public DynamicArgumentValueDictionary Where { get; } = new DynamicArgumentValueDictionary();
    }

    public class WorkflowService : Activity
    {
        public Activity Body { get; set; }
        public object Endpoints { get; set; }
        public object ImplementedContracts { get; set; }
        public string Name { get; set; }

        public ValidationResults Validate(ValidationSettings settings) => new(new List<ValidationError>());

        public Activity GetWorkflowRoot() => Body;
    }

    public class Send : NativeActivity
    {
        public InArgument<CorrelationHandle> CorrelatesWith { get; set; }
        public Collection<CorrelationInitializer> CorrelationInitializers { get; } = new Collection<CorrelationInitializer>();
        public object Endpoint { get; set; }
        public System.ServiceModel.EndpointAddress EndpointAddress { get; set; }
        public string EndpointConfigurationName { get; set; }
        public string OperationName { get; set; }
        public string Action { get; set; }
        public object ProtectionLevel { get; set; }
        public object SerializerOption { get; set; }
        public System.Xml.Linq.XName ServiceContractName { get; set; }
        public object KnownTypes { get; set; }
        public MessageContent Content { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class Receive : NativeActivity
    {
        public InArgument<CorrelationHandle> CorrelatesWith { get; set; }
        public Collection<CorrelationInitializer> CorrelationInitializers { get; } = new Collection<CorrelationInitializer>();
        public string OperationName { get; set; }
        public object ProtectionLevel { get; set; }
        public object SerializerOption { get; set; }
        public System.Xml.Linq.XName ServiceContractName { get; set; }
        public object KnownTypes { get; set; }
        public MessageContent Content { get; set; }
        public bool CanCreateInstance { get; set; }

        public static Receive FromOperationDescription(object operation) => new Receive();

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class SendReply : NativeActivity
    {
        public Receive Request { get; set; }
        public MessageContent Content { get; set; }

        public static SendReply FromOperationDescription(object operation, out IEnumerable<SendReply> faultReplies)
        {
            faultReplies = Array.Empty<SendReply>();
            return new SendReply();
        }

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class ReceiveReply : NativeActivity
    {
        public Send Request { get; set; }
        public MessageContent Content { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class InitializeCorrelation : NativeActivity
    {
        public InArgument<CorrelationHandle> CorrelationHandle { get; set; }
        public DynamicArgumentValueDictionary CorrelationData { get; } = new DynamicArgumentValueDictionary();

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class CorrelationScope : NativeActivity
    {
        public InArgument<CorrelationHandle> CorrelatesWith { get; set; }
        public Activity Body { get; set; }
        public Collection<Variable> Variables { get; } = new Collection<Variable>();

        protected override void Execute(NativeActivityContext context)
        {
        }
    }

    public class TransactedReceiveScope : NativeActivity
    {
        public Receive Request { get; set; }
        public Activity Body { get; set; }
        public Collection<Variable> Variables { get; } = new Collection<Variable>();

        protected override void Execute(NativeActivityContext context)
        {
        }
    }
}

namespace System.ServiceModel
{
    public class EndpointAddress
    {
        public EndpointAddress(string uri)
        {
            Uri = uri;
        }

        public string Uri { get; }
    }
}

