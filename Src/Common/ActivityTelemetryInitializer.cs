namespace Microsoft.ApplicationInsights.Common
{
    using System.Diagnostics;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    /// <summary>
    /// Telemetry initializer that sets operation context to current Activity.
    /// </summary>
    public class ActivityTelemetryInitializer : ITelemetryInitializer
    {
        /// <summary>
        /// Initializes telemetry Item with Activity.Current properties.
        /// </summary>
        /// <param name="telemetry">ITelemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (Activity.Current == null)
            {
                return;
            }

            // Operation Context for the RequestTelemetry is set when it's created
            if (telemetry is RequestTelemetry)
            {
                return;
            }

            var currentActivity = Activity.Current;

            if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
            {
                telemetry.Context.Operation.Id = currentActivity.RootId;

                var operationTelemetry = telemetry as OperationTelemetry;
                if (operationTelemetry != null)
                {
                    // OperationTelemetry must be represented by its own Activity
                    operationTelemetry.Id = currentActivity.Id;
                    operationTelemetry.Context.Operation.ParentId = currentActivity.ParentId;

                    foreach (var tag in currentActivity.Tags)
                    {
                        if (!operationTelemetry.Context.Properties.ContainsKey(tag.Key))
                        {
                            operationTelemetry.Context.Properties.Add(tag);
                        }
                    }
                }
                else
                {
                    // all other telemetries created within the scope of this request, are it's children and don't share tags
                    telemetry.Context.Operation.ParentId = currentActivity.Id;
                }

                foreach (var baggage in currentActivity.Baggage)
                {
                    if (!telemetry.Context.CorrelationContext.ContainsKey(baggage.Key))
                    {
                        telemetry.Context.CorrelationContext.Add(baggage);
                    }
                }
            }
        }
    }
}