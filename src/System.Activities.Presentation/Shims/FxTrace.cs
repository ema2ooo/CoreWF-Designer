using System;

namespace System.Runtime
{
    internal static class FxTrace
    {
        internal static class Exception
        {
            public static global::System.Exception AsError(global::System.Exception exception) => exception;

            public static global::System.Exception ArgumentNull(string paramName)
                => new ArgumentNullException(paramName);

            public static global::System.Exception Argument(string paramName, string message)
                => new ArgumentException(message, paramName);
        }
    }
}
