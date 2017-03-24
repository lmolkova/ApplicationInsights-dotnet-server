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

                var operationTelemetry = telemetry as OperationTelemetry;
                if (operationTelemetry != null)
                {
                    // if it is RequestTelemetry, then Activity.Current represents it
                    operationTelemetry.Id = currentActivity.Id;

                    // there may be the case when:
                    //  - this is RequestTelemetry initialization
                    //  - and we received legacy or custom header with parentId
                    // so Activity.ParentId is null, but we have set operation.ParentId in the RequestTrackingTelemetryModule
                    // so we don't update it here
                    if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                    {
                        telemetry.Context.Operation.ParentId = currentActivity.ParentId;
                    }

                    foreach (var tag in currentActivity.Tags)
                    {
                        if (!telemetry.Context.CorrelationContext.ContainsKey(tag.Key))
                        {
                            telemetry.Context.Properties.Add(tag);
                        }
                    }
                }
                else
                {
                    // all other telemetries created within the scope of this request, are it's children and don't share tags
                    if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                    {
                        telemetry.Context.Operation.ParentId = currentActivity.Id;
                    }
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