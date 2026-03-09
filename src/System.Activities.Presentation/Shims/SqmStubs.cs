using System;

namespace System.Activities.Presentation.Sqm
{
    public interface IVSSqmService
    {
        void SetDatapoint(int dataPointId, uint value);
        void AddItemToStream(int dataPointId, uint value);
        void AddArrayToStream(int dataPointId, uint[] data, int count);
    }

    internal enum WorkflowDesignerFeatureId
    {
        Breadcrumb,
        CollapseAll,
        CopyAsImage,
        ExpandAll,
        FitToScreen,
        Minimap,
        OpenChild,
        ResetZoom,
        Restore,
        SaveAsImage,
        ViewParent,
    }

    internal static class FeatureUsageCounter
    {
        internal static void ReportUsage(IVSSqmService sqmService, WorkflowDesignerFeatureId featureId)
        {
        }
    }

    internal static class ActivityUsageCounter
    {
        internal static void ReportUsage(IVSSqmService sqmService, Type activityType)
        {
        }
    }
}
