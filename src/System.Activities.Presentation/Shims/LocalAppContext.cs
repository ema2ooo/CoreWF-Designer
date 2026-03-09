namespace System.Activities.Presentation
{
    internal static class LocalAppContext
    {
        internal static bool GetCachedSwitchValue(string switchName, ref int cachedValue)
        {
            if (cachedValue == 0)
            {
                cachedValue = AppContext.TryGetSwitch(switchName, out var enabled) && enabled ? 1 : -1;
            }

            return cachedValue > 0;
        }
    }
}
