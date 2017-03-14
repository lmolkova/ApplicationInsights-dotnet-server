using System;
using System.Collections.Generic;
using System.Diagnostics;

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

            if (string.IsNullOrEmpty(parentContext.Id))
            {
                var currentRequestId = AppInsightsActivity.RequestId;
                // there is no operation id
                if (currentRequestId == null)
                {
                    //there is no Id in CorrelationContext, re-read headers
                    string rootId, parentId, requestId;
                    if (TryParseStandardHeader(platformContext.Request, out rootId, out parentId, out requestId) ||
                        TryParseCustomHeaders(platformContext.Request, out rootId, out parentId, out requestId))
                    {
                        parentContext.Id = rootId;
                        parentContext.ParentId = parentId;
                        requestTelemetry.Id = requestId;
                    }
                    else
                    {
                        requestTelemetry.Id = AppInsightsActivity.GenerateNewId();
                        parentContext.Id = AppInsightsActivity.GetRootId(requestTelemetry.Id);
                    }
                    AppInsightsActivity.ParentRequestId = parentId;
                    AppInsightsActivity.RequestId = requestTelemetry.Id;
                }
                else
                {
                    //there is no Operation context, but there is context in CallContext
                    requestTelemetry.Id = currentRequestId;
                    parentContext.Id = AppInsightsActivity.GetRootId(currentRequestId);
                    parentContext.ParentId = AppInsightsActivity.ParentRequestId;
                }
            }

            if (telemetry != requestTelemetry)
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

        private bool TryParseStandardHeader(
            HttpRequest request,
            out string rootId,
            out string parentId,
            out string requestId)
        {
            parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);
            if (!string.IsNullOrEmpty(parentId))
            {
                rootId = AppInsightsActivity.GetRootId(parentId);
                requestId = AppInsightsActivity.GenerateRequestId(parentId);
                //TODO
                //var correlationContext = request.UnvalidatedGetHeader(RequestResponseHeaders.CorrelationContextHeader);
                //CorrelationContext.Context = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Id", rootId) };
                return true;
            }
            rootId = null;
            requestId = null;
            return false;
        }

        private bool TryParseCustomHeaders(
            HttpRequest request,
            out string rootId,
            out string parentId,
            out string requestId)
        {
            parentId = request.UnvalidatedGetHeader(this.ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(this.RootOperationIdHeaderName);
            if (!string.IsNullOrEmpty(rootId))
            {
                requestId = AppInsightsActivity.GenerateRequestId(rootId);
                return true;
            }
            if (!string.IsNullOrEmpty(parentId))
            {
                requestId = AppInsightsActivity.GenerateRequestId(parentId);
                return true;
            }

            requestId = null;
            return false;
        }
    }
}
