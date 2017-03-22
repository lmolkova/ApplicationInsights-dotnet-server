namespace Microsoft.ApplicationInsights.Common
{
    using System.Diagnostics;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    /// <summary>
    /// Telemetry initializer that initalizes operation context with current Activity
    /// </summary>
    public class ActivityTelemetryInitializer : ITelemetryInitializer
    {
        /// <summary>
        /// Initializes telemetry Item with Activity.Current properties
        /// </summary>
        /// <param name="telemetry"></param>
        public void Initialize(ITelemetry telemetry)
        {
            if (Activity.Current == null)
            {
                return;
            }

            var currentActivity = Activity.Current;

            if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
            {
                telemetry.Context.Operation.Id = currentActivity.RootId;
                telemetry.Context.Operation.ParentId = currentActivity.ParentId;

                var operationTelemetry = telemetry as OperationTelemetry;
                if (operationTelemetry != null)
                {
                    operationTelemetry.Id = currentActivity.Id;
                }

                foreach (var baggage in currentActivity.Baggage)
                {
                    if (!telemetry.Context.CorrelationContext.ContainsKey(baggage.Key))
                    {
                        telemetry.Context.CorrelationContext.Add(baggage);
                    }
                }

                foreach (var tag in currentActivity.Tags)
                {
                    if (!telemetry.Context.CorrelationContext.ContainsKey(tag.Key))
                    {
                        telemetry.Context.Properties.Add(tag);
                    }
                }
            }
        }
    }
}