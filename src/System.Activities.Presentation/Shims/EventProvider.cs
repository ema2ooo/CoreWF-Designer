using System;

namespace System.Diagnostics.Eventing
{
    internal sealed class EventProvider
    {
        public EventProvider(Guid providerId)
        {
        }

        public bool IsEnabled() => false;

        public void WriteEvent(ref EventDescriptor eventDescriptor)
        {
        }
    }
}
