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
            this.ParentOperationIdHeaderName = RequestResponseHeaders.StandardParentIdHeader;
            this.RootOperationIdHeaderName = RequestResponseHeaders.StandardRootIdHeader;
        }

        /// <summary>
        /// Gets or sets the name of the header to get parent operation Id from.
        /// </summary>
        public string ParentOperationIdHeaderName { get; set; }

        /// <summary>
        /// Gets or sets the name of the header to get root operation Id from.
        /// </summary>
        public string RootOperationIdHeaderName { get; set; }

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
            OperationContext parentContext = requestTelemetry.Context.Operation;
            HttpRequest currentRequest = platformContext.Request;

            //Pares Headers only when RequestTelemetry is being initialized
            if (telemetry == requestTelemetry)
            {
                //We either have both root Id and parent Id or just rootId.
                //having single parentId is inconsistent and invalid and we'll update it.
                if (string.IsNullOrEmpty(parentContext.Id))
                {
                    string rootId, parentId;
                    if (TryParseCustomHeaders(currentRequest, out rootId, out parentId))
                    {
                        if (!string.IsNullOrEmpty(rootId))
                        {
                            parentContext.Id = rootId;
                            requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(rootId);
                        }
                        else //we received invalid request with parent, but without root
                        {
                            parentContext.Id = parentId;
                            requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                        }

                        if (!string.IsNullOrEmpty(parentId))
                        {
                            parentContext.ParentId = parentId;
                        }
                    }
                    else
                    {
                        //there was nothing in the headers, mimic Activity API behavior
                        requestTelemetry.Id = AppInsightsActivity.GenerateNewId();
                        parentContext.Id = AppInsightsActivity.GetRootId(requestTelemetry.Id);
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                {
                    telemetry.Context.Operation.ParentId = requestTelemetry.Id;
                }

                if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
                {
                    telemetry.Context.Operation.Id = parentContext.Id;
                }
            }
        }

        private bool TryParseCustomHeaders(
            HttpRequest request,
            out string rootId,
            out string parentId)
        {
            parentId = request.UnvalidatedGetHeader(this.ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(this.RootOperationIdHeaderName);
            if (string.IsNullOrEmpty(rootId) && string.IsNullOrEmpty(parentId))
            {
                return false;
            }
            return true;
        }
    }
}
