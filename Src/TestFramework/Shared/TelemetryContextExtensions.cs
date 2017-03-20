namespace Microsoft.ApplicationInsights.TestFramework
{
    using System.Collections.Concurrent;
    using System.Reflection;
    using Microsoft.ApplicationInsights.DataContracts;

    internal static class TelemetryContextExtensions
    {
        internal static ConcurrentDictionary<string,string> GetCorrelationContext(this TelemetryContext context)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = typeof(TelemetryContext).GetField("correlationContext", bindFlags);
            return (ConcurrentDictionary<string, string>)field.GetValue(context);
        }
    }
}
