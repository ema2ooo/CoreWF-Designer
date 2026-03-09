using System;
using System.Diagnostics;

namespace System.Runtime
{
    internal static class Fx
    {
        public static void Assert(string description)
        {
            Debug.Assert(false, description);
        }

        public static void Assert(bool condition)
        {
            if (!condition)
            {
                Debug.Assert(false);
            }
        }

        public static void Assert(bool condition, string description)
        {
            if (!condition)
            {
                Debug.Assert(false, description);
            }
        }

        public static bool IsFatal(Exception exception)
        {
            while (exception != null)
            {
                if (exception is OutOfMemoryException or AccessViolationException or AppDomainUnloadedException or BadImageFormatException or CannotUnloadAppDomainException)
                {
                    return true;
                }

                exception = exception switch
                {
                    TypeInitializationException tie => tie.InnerException,
                    System.Reflection.TargetInvocationException tie => tie.InnerException,
                    AggregateException aggregate when HasFatalInner(aggregate) => aggregate,
                    _ => null,
                };

                if (exception is AggregateException)
                {
                    return true;
                }
            }

            return false;
        }

        public static Exception AssertAndFailFast(string description)
        {
            Debug.Assert(false, description);
            throw new InvalidOperationException(description);
        }

        private static bool HasFatalInner(AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (IsFatal(inner))
                {
                    return true;
                }
            }

            return false;
        }

        internal static class Tag
        {
            [AttributeUsage(AttributeTargets.All, Inherited = false)]
            internal sealed class KnownXamlExternalAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.All, Inherited = false)]
            internal sealed class XamlVisibleAttribute : Attribute
            {
                public XamlVisibleAttribute(bool visible)
                {
                    Visible = visible;
                }

                public bool Visible { get; }
            }
        }
    }
}
