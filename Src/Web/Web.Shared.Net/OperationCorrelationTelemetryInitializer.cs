namespace Microsoft.ApplicationInsights.Web
{
    using System.Web;
    using Common;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    /// <summary>
    /// A telemetry initializer that will set the correlation context for all telemetry items in web application.
    /// </summary>
    public class OperationCorrelationTelemetryInitializer : WebTelemetryInitializerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationCorrelationTelemetryInitializer"/> class.
        /// </summary>
        public OperationCorrelationTelemetryInitializer()
        {
            ActivityHelpers.ParentOperationIdHeaderName = RequestResponseHeaders.StandardParentIdHeader;
            ActivityHelpers.RootOperationIdHeaderName = RequestResponseHeaders.StandardRootIdHeader;
        }

        /// <summary>
        /// Gets or sets the name of the header to get parent operation Id from.
        /// </summary>
        public string ParentOperationIdHeaderName
        {
            get { return ActivityHelpers.ParentOperationIdHeaderName; }
            set { ActivityHelpers.ParentOperationIdHeaderName = value; }
        }

        /// <summary>
        /// Gets or sets the name of the header to get root operation Id from.
        /// </summary>
        public string RootOperationIdHeaderName
        {
            get { return ActivityHelpers.RootOperationIdHeaderName; }
            set { ActivityHelpers.RootOperationIdHeaderName = value; }
        }

        /// <summary>
        /// Implements initialization logic.
        /// </summary>
        /// <param name="platformContext">Http context.</param>
        /// <param name="requestTelemetry">Request telemetry object associated with the current request.</param>
        /// <param name="telemetry">Telemetry item to initialize.</param>
        protected override void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry)
        {
            // Telemetry is initialized by Base SDK OperationCorrelationTelemetryInitializer from the call context on .NET 40
            // And by ActivityTelemetry initializer otherwise
            // However we still may loose CorrelationContext/AsyncLocal
            // In application code, we protect from it with PreRequestHandlerExecute event, that happens right before the handler
            // However Applivation_Error looses exectution context and some telemetry may be reported in between of HttpModule pipeline
            // where the exectution context could be lost as well
            // So we will initialize telemetry with RequestTelemetry stored in HttpContext
            if (telemetry != requestTelemetry && CallContextHelpers.GetCurrentOperationContext() == null)
            {
                if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                {
                    telemetry.Context.Operation.ParentId = requestTelemetry.Id;
                }

                if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
                {
                    telemetry.Context.Operation.Id = requestTelemetry.Context.Operation.Id;
                }
            }
        }
    }
}
