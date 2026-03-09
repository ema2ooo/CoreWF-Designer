using System;
using System.Activities.Debugger;
using System.Activities.Presentation.Model;
using System.Collections.Generic;

namespace System.Activities.Presentation.Debug
{
    [Flags]
    public enum BreakpointTypes
    {
        None = 0,
        Enabled = 1,
        Bounded = 2,
        Conditional = 4,
    }    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class AllowBreakpointAttribute : Attribute
    {
        internal static bool IsBreakpointAllowed(Type breakpointCandidateType)
            => typeof(Activity).IsAssignableFrom(breakpointCandidateType);
    }

    public interface IDesignerDebugView
    {
        SourceLocation CurrentContext { get; set; }
        SourceLocation CurrentLocation { get; set; }
        bool IsDebugging { get; set; }
        bool HideSourceFileName { get; set; }
        SourceLocation SelectedLocation { get; }
        IDictionary<SourceLocation, BreakpointTypes> GetBreakpointLocations();
        void ResetBreakpoints();
        void DeleteBreakpoint(SourceLocation sourceLocation);
        SourceLocation GetExactLocation(SourceLocation approximateLocation);
        void InsertBreakpoint(SourceLocation sourceLocation, BreakpointTypes breakpointType);
        void UpdateBreakpoint(SourceLocation sourceLocation, BreakpointTypes breakpointType);
        void EnsureVisible(SourceLocation sourceLocation);
    }

    public class DebuggerService : IDesignerDebugView
    {
        private readonly Dictionary<SourceLocation, BreakpointTypes> breakpoints = new();

        public DebuggerService(EditingContext context)
        {
        }

        public SourceLocation CurrentContext { get; set; }
        public SourceLocation CurrentLocation { get; set; }
        public bool IsDebugging { get; set; }
        public bool HideSourceFileName { get; set; }
        public SourceLocation SelectedLocation => null;
        public IDictionary<SourceLocation, BreakpointTypes> GetBreakpointLocations() => breakpoints;
        public void ResetBreakpoints() => breakpoints.Clear();
        public void DeleteBreakpoint(SourceLocation sourceLocation) => breakpoints.Remove(sourceLocation);
        public SourceLocation GetExactLocation(SourceLocation approximateLocation) => approximateLocation;
        public void InsertBreakpoint(SourceLocation sourceLocation, BreakpointTypes breakpointType) => breakpoints[sourceLocation] = breakpointType;
        public void UpdateBreakpoint(SourceLocation sourceLocation, BreakpointTypes breakpointType)
        {
            if (breakpointType == BreakpointTypes.None) breakpoints.Remove(sourceLocation); else breakpoints[sourceLocation] = breakpointType;
        }
        public void EnsureVisible(SourceLocation sourceLocation) { }
        internal void InvalidateSourceLocationMapping(string fileName) { }
        internal void UpdateSourceLocations() { }
    }
}

