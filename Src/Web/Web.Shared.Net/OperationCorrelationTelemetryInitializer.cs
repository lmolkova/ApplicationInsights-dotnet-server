namespace Microsoft.ApplicationInsights.Web
{
    using System.Web;
    using Common;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
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
            RequestTrackingExtensions.ParentOperationIdHeaderName = RequestResponseHeaders.StandardParentIdHeader;
            RequestTrackingExtensions.RootOperationIdHeaderName = RequestResponseHeaders.StandardRootIdHeader;
        }

        /// <summary>
        /// Gets or sets the name of the header to get parent operation Id from.
        /// </summary>
        public string ParentOperationIdHeaderName
        {
            get { return RequestTrackingExtensions.ParentOperationIdHeaderName; }
            set { RequestTrackingExtensions.ParentOperationIdHeaderName = value; }
        }

        /// <summary>
        /// Gets or sets the name of the header to get root operation Id from.
        /// </summary>
        public string RootOperationIdHeaderName
        {
            get { return RequestTrackingExtensions.RootOperationIdHeaderName; }
            set { RequestTrackingExtensions.RootOperationIdHeaderName = value; }
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
#if !NET46
            if (requestTelemetry != telemetry)
            {
                if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                {
                    telemetry.Context.Operation.ParentId = requestTelemetry.Id;
                }

                if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
                {
                    telemetry.Context.Operation.Id = requestTelemetry.Context.Operation.ParentId;
                }
            }
#endif
        }
    }
}
