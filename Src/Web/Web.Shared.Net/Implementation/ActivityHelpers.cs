namespace Microsoft.ApplicationInsights.Common
{
    using System.Collections.Generic;
    using System.Web;

    using Microsoft.ApplicationInsights.DataContracts;
#if NET40
    using Microsoft.ApplicationInsights.Net40;
#endif
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal class ActivityHelpers
    {
        internal const string CorrelationContextItemName = "Microsoft.AppInsights.Web.CorrelationContext";
        internal const string RequestActivityItemName = "Microsoft.AppInsights.Web.Activity";

        internal static string RootOperationIdHeaderName { get; set; }

        internal static string ParentOperationIdHeaderName { get; set; }

#if NET40
#pragma warning disable 618
        /// <summary>
        /// Parses incoming request headers; initializes Operation Context and stores it in CallContext.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        /// <returns>RequestTelemetry with OperationContext parsed from the request.</returns>
        internal static RequestTelemetry ParseRequest(HttpContext context)
        {
            RequestTelemetry requestTelemetry = new RequestTelemetry();
            var request = context.Request;

            IDictionary<string, string> correlationContext;
            string rootId, parentId;
            if (!TryParseStandardHeaders(request, out rootId, out parentId, out correlationContext))
            {
                TryParseCustomHeaders(request, out rootId, out parentId);
            }

            requestTelemetry.Context.Operation.ParentId = parentId;
            if (rootId != null)
            {
                requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(rootId);
            }
            else if (parentId != null)
            {
                requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
            }
            else
            {
                requestTelemetry.Id = AppInsightsActivity.GenerateRequestId();
            }

            requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(requestTelemetry.Id);
            if (correlationContext != null)
            {
                foreach (var item in correlationContext)
                {
                    requestTelemetry.Context.Properties[item.Key] = item.Value;
                }
            }

            CorrelationHelper.SetOperationContext(requestTelemetry, correlationContext);

            // save correlation-context in case CallContext will be lost, the rest of the context is saved in requestTelemetry
            context.Items[CorrelationContextItemName] = correlationContext;

            return requestTelemetry;
        }

        /// <summary>
        /// Restores CallContext if it was lost.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        internal static void RestoreOperationContextIfLost(HttpContext context)
        {
            var requestTelemetry = context.GetRequestTelemetry();
            var correlationContext = context.Items[CorrelationContextItemName] as IDictionary<string, string>;

            CorrelationHelper.SetOperationContext(requestTelemetry, correlationContext);
        }

        /// <summary>
        /// Cleans up CallContext.
        /// </summary>
        internal static void CleanOperationContext()
        {
            // it just allows to have the same API for Activity/CallContext
            CorrelationHelper.CleanOperationContext();
        }
#pragma warning restore 618
#endif

        private static bool TryParseStandardHeaders(HttpRequest request, out string rootId, out string parentId, out IDictionary<string, string> correlationContext)
        {
            rootId = null;
            correlationContext = null;
            parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);

            // don't bother parsing correlation-context if there was no RequestId
            if (!string.IsNullOrEmpty(parentId))
            {
                correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);

                bool isHierarchicalId = IsHierarchicalRequestId(parentId);

                if (correlationContext != null)
                {
                    foreach (var item in correlationContext)
                    {
                        if (!isHierarchicalId && item.Key == "Id")
                        {
                            rootId = item.Value;
                        }
                    }
                }

                return true;
            }

            parentId = null;
            return false;
        }

        private static bool IsHierarchicalRequestId(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && requestId[0] == '|';
        }

        private static bool TryParseCustomHeaders(HttpRequest request, out string rootId, out string parentId)
        {
            parentId = request.UnvalidatedGetHeader(ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(RootOperationIdHeaderName);

            if (rootId?.Length == 0)
            {
                rootId = null;
            }

            if (parentId?.Length == 0)
            {
                parentId = null;
            }

            return rootId != null || parentId != null;
        }
    }
}