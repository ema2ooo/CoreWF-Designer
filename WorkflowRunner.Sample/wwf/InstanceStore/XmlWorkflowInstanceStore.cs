using System;
using System.Activities.DurableInstancing;
using System.Activities.Runtime.DurableInstancing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

#nullable disable

namespace JsonFileInstanceStore
{
    public abstract class XmlWorkflowInstanceStore : InstanceStore, IDisposable
    {

        /// <summary>
        /// A unique identifier for the store of instances. There will usually be one store id for all workflows
        /// in an application. If one is not specified, then one will be generated.
        /// </summary>
        private Guid _storeId;

        /// <summary>
        /// Internal handle used to identify the workflow owner.
        /// </summary>
        private InstanceHandle _handle;

        public XmlWorkflowInstanceStore(Guid storeId)
        {
            _storeId = storeId;

            _handle = this.CreateInstanceHandle();
            var view = this.Execute(_handle, new CreateWorkflowOwnerCommand(), TimeSpan.FromSeconds(30));
            this.DefaultInstanceOwner = view.InstanceOwner;

        }

        public abstract void Save(Guid instanceId, string doc);
        public abstract string Load(Guid instanceId);

        public abstract bool Clean(Guid instanceId);

        // Synchronous version of the Begin/EndTryCommand functions
        protected override bool TryCommand(InstancePersistenceContext context, InstancePersistenceCommand command, TimeSpan timeout)
        {
            return EndTryCommand(BeginTryCommand(context, command, timeout, null, null));
        }
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            Converters = new[] { new TypeJsonConverter() }
        };

        class TypeJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => throw new NotImplementedException();
            public override bool CanRead => true;
            public override bool CanConvert(Type objectType) => typeof(Type).IsAssignableFrom(objectType);
        }
        // The persistence engine will send a variety of commands to the configured InstanceStore,
        // such as CreateWorkflowOwnerCommand, SaveWorkflowCommand, and LoadWorkflowCommand.
        // This method is where we will handle those commands.
        protected override IAsyncResult BeginTryCommand(InstancePersistenceContext context, InstancePersistenceCommand command, TimeSpan timeout, AsyncCallback callback, object state)
        {
            
            //The CreateWorkflowOwner command instructs the instance store to create a new instance owner bound to the instanace handle
            if (command is CreateWorkflowOwnerCommand)
            {
                context.BindInstanceOwner(_storeId, Guid.NewGuid());
            }
            //The SaveWorkflow command instructs the instance store to modify the instance bound to the instance handle or an instance key
            else if (command is SaveWorkflowCommand)
            {

                SaveWorkflowCommand saveCommand = (SaveWorkflowCommand)command;
                var instanceStateData = saveCommand.InstanceData;

                Dictionary<string, InstanceValue> instanceData = SerializeablePropertyBagConvertXNameInstanceValue(instanceStateData);
                var serializedInstanceData = JsonConvert.SerializeObject(instanceData, Formatting.Indented, _jsonSerializerSettings);

                //  var instanceStateXml = DictionaryToXml(instanceStateData);
                Save(context.InstanceView.InstanceId, serializedInstanceData);

                if (context.InstanceVersion == -1)
                {
                    context.BindAcquiredLock(0);
                }

                if (saveCommand.CompleteInstance)
                {


                    Clean(context.InstanceView.InstanceId);
                }

            }
            //The LoadWorkflow command instructs the instance store to lock and load the instance bound to the identifier in the instance handle
            else if (command is LoadWorkflowCommand)
            {
                IDictionary<XName, InstanceValue> instanceData = null;
             
                Dictionary<string, InstanceValue> serializableInstanceData;
            
                var serializedInstanceData = Load(context.InstanceView.InstanceId);

                serializableInstanceData = JsonConvert.DeserializeObject<Dictionary<string, InstanceValue>>(serializedInstanceData, _jsonSerializerSettings);

                instanceData = this.DeserializePropertyBagConvertXNameInstanceValue(serializableInstanceData);

                // instanceStateData = XmlToDictionary(xml);
                //load the data into the persistence Context
                context.LoadedInstance(InstanceState.Initialized, instanceData, null, null, null);
            }

            return new CompletedAsyncResult<bool>(true, callback, state);
        }

        protected override bool EndTryCommand(IAsyncResult result)
        {
            return CompletedAsyncResult<bool>.End(result);
        }

        
     

        private Dictionary<string, InstanceValue> SerializeablePropertyBagConvertXNameInstanceValue(IDictionary<XName, InstanceValue> source)
        {
            Dictionary<string, InstanceValue> scratch = new Dictionary<string, InstanceValue>();
            foreach (KeyValuePair<XName, InstanceValue> property in source)
            {
                bool writeOnly = (property.Value.Options & InstanceValueOptions.WriteOnly) != 0;

                if (!writeOnly && !property.Value.IsDeletedValue)
                {
                    scratch.Add(property.Key.ToString(), property.Value);
                }
            }

            return scratch;
        }


        private IDictionary<XName, InstanceValue> DeserializePropertyBagConvertXNameInstanceValue(Dictionary<string, InstanceValue> source)
        {
            Dictionary<XName, InstanceValue> destination = new Dictionary<XName, InstanceValue>();

            foreach (KeyValuePair<string, InstanceValue> property in source)
            {
                destination.Add(property.Key, property.Value);
            }

            return destination;
        }
      

        public void Dispose()
        {
            this.Execute(_handle, new DeleteWorkflowOwnerCommand(), TimeSpan.FromSeconds(30));
            _handle.Free();
        }
    }

}
