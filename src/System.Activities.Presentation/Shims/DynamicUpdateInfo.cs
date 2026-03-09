using System.Activities;
using System.Xaml;

namespace System.Activities.DynamicUpdate
{
    public static class DynamicUpdateInfo
    {
        private static readonly AttachableMemberIdentifier OriginalDefinitionProperty = new(typeof(DynamicUpdateInfo), "OriginalDefinition");
        private static readonly AttachableMemberIdentifier OriginalActivityBuilderProperty = new(typeof(DynamicUpdateInfo), "OriginalActivityBuilder");

        public static object GetOriginalDefinition(object target)
        {
            AttachablePropertyServices.TryGetProperty(target, OriginalDefinitionProperty, out object value);
            return value;
        }

        public static void SetOriginalDefinition(object target, object value)
            => AttachablePropertyServices.SetProperty(target, OriginalDefinitionProperty, value);

        public static ActivityBuilder GetOriginalActivityBuilder(object target)
        {
            AttachablePropertyServices.TryGetProperty(target, OriginalActivityBuilderProperty, out ActivityBuilder value);
            return value;
        }

        public static void SetOriginalActivityBuilder(object target, ActivityBuilder value)
            => AttachablePropertyServices.SetProperty(target, OriginalActivityBuilderProperty, value);
    }
}
