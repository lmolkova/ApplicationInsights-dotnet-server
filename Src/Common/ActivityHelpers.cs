namespace Microsoft.ApplicationInsights.Common
{
#if NET46
    using System;
    using System.Diagnostics;
#endif
    using System.Web;

    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal class ActivityHelpers
    {
        internal static string RootOperationIdHeaderName { get; set; }

        internal static string ParentOperationIdHeaderName { get; set; }

#if NET46
        /// <summary>
        /// Parses incoming request headers and starts Activity.
        /// </summary>
        /// <param name="requestTelemetry">RequestTelemetry to initialize with Operation context.</param>
        /// <param name="context">HttpContext instance.</param>
        public static void StartActivity(RequestTelemetry requestTelemetry, HttpContext context)
        {
            var request = context.Request;

            Activity activity;
            if (!TryParseStandardHeaders(requestTelemetry, request, out activity))
            {
                // we've got no request-id from upstream server, let's check for legacy or custom headers
                string rootId, parentId;
                if (TryParseCustomHeaders(request, out rootId, out parentId))
                {
                    // We've got legacy or custom headers
                    if (!string.IsNullOrEmpty(rootId))
                    {
                        // Let's start another Activity, that will have proper details
                        activity = new Activity("HttpIn");
                        activity.SetParentId(rootId);
                    }

                    // we set ParentId to one from the custom headers
                    // so it will not be updated by telemetry initializers
                    if (!string.IsNullOrEmpty(parentId))
                    {
                        requestTelemetry.Context.Operation.ParentId = parentId;
                    }
                }
            }

            if (activity == null)
            {
                activity = new Activity("HttpIn");
            }

            activity.Start();
            
            // store activity in case it will be lost after OnBeginRequest
            context.Items.Add(RequestTrackingConstants.ActivityItemName, activity);

            // we may loose Activity.Current in the middle of HttpModule pipeline
            // and we will restore it in the PreRequestHandlerExecute
            // however some telemetry may be reported in the middle, where it was lost
            // so we will set operation context here so child telemetry may update itself from it
            requestTelemetry.Context.Operation.Id = activity.RootId;
            requestTelemetry.Id = activity.Id;
            if (string.IsNullOrEmpty(requestTelemetry.Context.Operation.ParentId))
            { 
                requestTelemetry.Context.Operation.Id = activity.ParentId;
            }
        }

        /// <summary>
        /// Restores Activity if it was lost.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        internal static void RestoreActivityIfLost(HttpContext context)
        {
            if (Activity.Current == null)
            {
                var lostActivity = context.Items[RequestTrackingConstants.ActivityItemName] as Activity;
                if (lostActivity != null)
                {
                    var restoredActivity = new Activity("HttpIn");
                    restoredActivity.SetParentId(lostActivity.Id);
                    restoredActivity.SetStartTime(lostActivity.StartTimeUtc);

                    foreach (var item in lostActivity.Baggage)
                    {
                        restoredActivity.AddBaggage(item.Key, item.Value);
                    }

                    restoredActivity.Start();
                }
            }
        }

        /// <summary>
        /// Stops all active activities.
        /// </summary>
        internal static void StopActivity()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        private static bool TryParseStandardHeaders(RequestTelemetry requestTelemetry, HttpRequest request, out Activity activity)
        {
            activity = null;
            var parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);

            // don't bother parsing correlation-context if there was no RequestId
            if (!string.IsNullOrEmpty(parentId))
            {
                activity = new Activity("HttpIn");
                var correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);
                bool isHierarchicalId = IsHierarchicalRequestId(parentId);
                bool parentIdIsSet = false;

                if (correlationContext != null)
                {
                    foreach (var item in correlationContext)
                    {
                        try
                        {
                            if (!isHierarchicalId && item.Key == "Id")
                            {
                                activity.SetParentId(item.Value);
                                requestTelemetry.Context.Operation.ParentId = parentId;
                                parentIdIsSet = true;
                            }

                            activity.AddBaggage(item.Key, item.Value);
                        }
                        catch (ArgumentException)
                        {
                        }
                    }
                }

                if (!parentIdIsSet)
                {
                    try
                    {
                        activity.SetParentId(parentId);
                    }
                    catch (ArgumentException)
                    {
                    }
                } 

                return true;
            }

            return false;
        }
#else
        /// <summary>
        /// Parses incoming request headers; initializes Operation Context and stores it in CallContext.
        /// </summary>
        /// <param name="requestTelemetry">RequestTelemetry to initialize with Operation context.</param>
        /// <param name="context">HttpContext instance.</param>
        internal static void StartActivity(RequestTelemetry requestTelemetry, HttpContext context)
        {
            var request = context.Request;
            if (!TryParseStandardHeaders(requestTelemetry, request))
            {
                string rootId, parentId;
                if (TryParseCustomHeaders(request, out rootId, out parentId))
                {
                    if (!string.IsNullOrEmpty(rootId))
                    {
                        requestTelemetry.Context.Operation.Id = rootId;
                        requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(rootId);
                    }
                    else
                    {
                        // we received invalid request with parent, but without root
                        requestTelemetry.Context.Operation.Id = parentId;
                        requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                    }

                    if (!string.IsNullOrEmpty(parentId))
                    {
                        requestTelemetry.Context.Operation.ParentId = parentId;
                    }
                }
                else
                {
                    // there was nothing in the headers, mimic Activity API behavior
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId();
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(requestTelemetry.Id);
                }
            }

            var operationContext = new OperationContextForCallContext
            {
                RootOperationId = requestTelemetry.Context.Operation.Id,
                ParentOperationId = requestTelemetry.Id,
                CorrelationContext = requestTelemetry.Context.CorrelationContext
            };

            CallContextHelpers.SaveOperationContext(operationContext);
        }

        /// <summary>
        /// Restores CallContext for the operation if it was lost.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        internal static void RestoreActivityIfLost(HttpContext context)
        {
            if (CallContextHelpers.GetCurrentOperationContext() == null)
            {
                var requestTelemetry = context.GetRequestTelemetry();
                if (requestTelemetry != null)
                {
                    var operationContext = new OperationContextForCallContext
                    {
                        RootOperationId = requestTelemetry.Context.Operation.Id,
                        ParentOperationId = requestTelemetry.Id,
                        CorrelationContext = requestTelemetry.Context.CorrelationContext
                    };
                    CallContextHelpers.SaveOperationContext(operationContext);
                }
            }
        }

        /// <summary>
        /// Cleans up operation call context.
        /// </summary>
        internal static void StopActivity()
        {
            CallContextHelpers.SaveOperationContext(null);
        }

        private static bool TryParseStandardHeaders(RequestTelemetry requestTelemetry, HttpRequest request)
        {
            var parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);

            // don't bother parsing correlation-context if there was no RequestId
            if (!string.IsNullOrEmpty(parentId))
            {
                var correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);

                bool isHierarchicalId = IsHierarchicalRequestId(parentId);
                if (isHierarchicalId)
                {
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(parentId);
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                }

                if (correlationContext != null)
                {
                    foreach (var item in correlationContext)
                    {
                        if (!string.IsNullOrEmpty(item.Key) &&
                            !string.IsNullOrEmpty(item.Value) &&
                            item.Key.Length <= 16 &&
                            item.Value.Length < 42)
                        {
                            if (!isHierarchicalId && item.Key == "Id")
                            {
                                requestTelemetry.Context.Operation.Id = item.Value;
                                requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(item.Value);
                            }

                            requestTelemetry.Context.CorrelationContext[item.Key] = item.Value;
                        }
                    }
                }

                requestTelemetry.Context.Operation.ParentId = parentId;

                if (string.IsNullOrEmpty(requestTelemetry.Context.Operation.Id))
                {
                    // ok, upstream gave us non-hirarchical id and no Id in the correlation context
                    // We'll use parentId to generate our Ids.
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(parentId);
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                }

                return true;
            }

            return false;
        }
#endif

        private static bool IsHierarchicalRequestId(string requestId)
        {
            return requestId != null && requestId.Length > 0 && requestId[0] == '|';
        }

        private static bool TryParseCustomHeaders(HttpRequest request, out string rootId, out string parentId)
        {
            parentId = request.UnvalidatedGetHeader(ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(RootOperationIdHeaderName);

            return !string.IsNullOrEmpty(rootId) || !string.IsNullOrEmpty(parentId);
        }
    }
}