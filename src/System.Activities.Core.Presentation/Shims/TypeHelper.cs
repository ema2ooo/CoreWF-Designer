using System;

namespace System.Activities.Core.Presentation
{
    internal static class TypeHelper
    {
        internal static bool AreTypesCompatible(Type sourceType, Type targetType)
        {
            if (sourceType == null || targetType == null)
            {
                return false;
            }

            return targetType.IsAssignableFrom(sourceType) || sourceType == targetType;
        }
    }
}
