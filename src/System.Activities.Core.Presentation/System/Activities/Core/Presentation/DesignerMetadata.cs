//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace System.Activities.Core.Presentation
{
    using System.Activities.Presentation;
    using System.Activities.Presentation.Converters;
    using System.Activities.Presentation.Metadata;
    using System.Activities.Presentation.Model;
    using System.Activities.Presentation.PropertyEditing;
    using System.Activities.Presentation.View;
    using System.Activities.Statements;
    using System.Activities.Validation;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Xml.Linq;

    public class DesignerMetadata : IRegisterMetadata
    {
        // Called by the designer to register any design-time metadata.
        //
        // Be aware of the accidential performance impact when adding things into this method.
        // In particular, pay attention to calls that will lead to loading extra assemblies.
        //
        public void Register()
        {
            AttributeTableBuilder builder = new AttributeTableBuilder();

            //shared component
            builder.AddCustomAttributes(typeof(Collection<Constraint>), new BrowsableAttribute(false));
            builder.AddCustomAttributes(typeof(string), new EditorReuseAttribute(false));
            builder.AddCustomAttributes(typeof(ActivityAction), new EditorReuseAttribute(false));
            builder.AddCustomAttributes(typeof(XName), new EditorReuseAttribute(false));

            //Flowchart activities
            FlowchartDesigner.RegisterMetadata(builder);
            FlowSwitchDesigner.RegisterMetadata(builder);
            FlowDecisionDesigner.RegisterMetadata(builder);
            // Messaging activities are deferred for the MVP port.

            //Procedural activities
            AssignDesigner.RegisterMetadata(builder);
            IfElseDesigner.RegisterMetadata(builder);
            InvokeMethodDesigner.RegisterMetadata(builder);
            DoWhileDesigner.RegisterMetadata(builder);
            WhileDesigner.RegisterMetadata(builder);
            ForEachDesigner.RegisterMetadata(builder);
            TryCatchDesigner.RegisterMetadata(builder);
            CatchDesigner.RegisterMetadata(builder);
            ParallelDesigner.RegisterMetadata(builder);
            SequenceDesigner.RegisterMetadata(builder);
            SwitchDesigner.RegisterMetadata(builder);
            CaseDesigner.RegisterMetadata(builder);

            //Compensation/Transaction
            CancellationScopeDesigner.RegisterMetadata(builder);
            CompensableActivityDesigner.RegisterMetadata(builder);
            TransactionScopeDesigner.RegisterMetadata(builder);

            //Misc activities            
            PickDesigner.RegisterMetadata(builder);
            PickBranchDesigner.RegisterMetadata(builder);
            WriteLineDesigner.RegisterMetadata(builder);
            NoPersistScopeDesigner.RegisterMetadata(builder);

            InvokeDelegateDesigner.RegisterMetadata(builder);

            // StateMachine
            StateMachineDesigner.RegisterMetadata(builder);
            StateDesigner.RegisterMetadata(builder);
            TransitionDesigner.RegisterMetadata(builder);

            builder.AddCustomAttributes(typeof(AddToCollection<>), new FeatureAttribute(typeof(UpdatableGenericArgumentsFeature)));
            builder.AddCustomAttributes(typeof(RemoveFromCollection<>), new FeatureAttribute(typeof(UpdatableGenericArgumentsFeature)));
            builder.AddCustomAttributes(typeof(ClearCollection<>), new FeatureAttribute(typeof(UpdatableGenericArgumentsFeature)));
            builder.AddCustomAttributes(typeof(ExistsInCollection<>), new FeatureAttribute(typeof(UpdatableGenericArgumentsFeature)));

            builder.AddCustomAttributes(typeof(AddToCollection<>), new DefaultTypeArgumentAttribute(typeof(int)));
            builder.AddCustomAttributes(typeof(RemoveFromCollection<>), new DefaultTypeArgumentAttribute(typeof(int)));
            builder.AddCustomAttributes(typeof(ClearCollection<>), new DefaultTypeArgumentAttribute(typeof(int)));
            builder.AddCustomAttributes(typeof(ExistsInCollection<>), new DefaultTypeArgumentAttribute(typeof(int)));

            MetadataStore.AddAttributeTable(builder.CreateTable());

            MorphHelper.AddPropertyValueMorphHelper(typeof(InArgument<>), MorphHelpers.ArgumentMorphHelper);
            MorphHelper.AddPropertyValueMorphHelper(typeof(OutArgument<>), MorphHelpers.ArgumentMorphHelper);
            MorphHelper.AddPropertyValueMorphHelper(typeof(InOutArgument<>), MorphHelpers.ArgumentMorphHelper);
            MorphHelper.AddPropertyValueMorphHelper(typeof(ActivityAction<>), MorphHelpers.ActivityActionMorphHelper);
            MorphHelper.AddPropertyValueMorphHelper(typeof(ActivityFunc<,>), MorphHelpers.ActivityFuncMorphHelper);

            // There is no need to keep an reference to this delayed worker since the AppDomain event handler will do it.
            RegisterMetadataDelayedWorker delayedWorker = new RegisterMetadataDelayedWorker();
            delayedWorker.RegisterMetadataDelayed("System.Workflow.Runtime", InteropDesigner.RegisterMetadata);
            delayedWorker.WorkNowIfApplicable();
        }

        private static void RegisterMetadataForMessagingActivitiesPropertyEditors(AttributeTableBuilder builder)
        {
        }

        private static void RegisterMetadataForMessagingActivitiesSearchMetadata(AttributeTableBuilder builder)
        {
        }
    }
}


