namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
#if NET46
    using System.Diagnostics;
#endif
    using System.Linq;
    using System.Web;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    
    internal static class RequestTrackingExtensions
    {
        internal static string RootOperationIdHeaderName { get; set; }
        internal static string ParentOperationIdHeaderName { get; set; }

        internal static RequestTelemetry CreateRequestTelemetryPrivate(
            this HttpContext platformContext)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }

            var result = new RequestTelemetry();
#if !NET46
            if (!TryParseStandardHeaders(result, platformContext.Request))
            {
                string rootId, parentId;
                if (TryParseCustomHeaders(platformContext.Request, out rootId, out parentId))
                {
                    if (!string.IsNullOrEmpty(rootId))
                    {
                        result.Context.Operation.Id = rootId;
                        result.Id = AppInsightsActivity.GenerateRequestId(rootId);
                    }
                    else
                    {
                        // we received invalid request with parent, but without root
                        result.Context.Operation.Id = parentId;
                        result.Id = AppInsightsActivity.GenerateRequestId(parentId);
                    }

                    if (!string.IsNullOrEmpty(parentId))
                    {
                        result.Context.Operation.ParentId = parentId;
                    }
                }
                else
                {
                    // there was nothing in the headers, mimic Activity API behavior
                    result.Id = AppInsightsActivity.GenerateRequestId();
                    result.Context.Operation.Id = AppInsightsActivity.GetRootId(result.Id);
                }
            }
#else
            // we've got no request-id from upstream server, let's check for legacy or custom headers
            Activity activity;
            if (!TryParseStandardHeaders(result, platformContext.Request, out activity))
            {
                string rootId, parentId;
                if (TryParseCustomHeaders(platformContext.Request, out rootId, out parentId))
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
                        result.Context.Operation.ParentId = parentId;
                    }
                }
            }

            if (activity == null)
            {
                activity = new Activity("HttpIn");
            }

            activity.Start();
            platformContext.Items.Add(RequestTrackingConstants.ActivityItemName, activity);
#endif

            platformContext.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, result);
            WebEventSource.Log.WebTelemetryModuleRequestTelemetryCreated();

            return result;
        }

        internal static RequestTelemetry ReadOrCreateRequestTelemetryPrivate(
            this HttpContext platformContext)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }

            var result = platformContext.GetRequestTelemetry() ??
                         CreateRequestTelemetryPrivate(platformContext);

            return result;
        }

        internal static string CreateRequestNamePrivate(this HttpContext platformContext)
        {
            var request = platformContext.Request;
            string name = request.UnvalidatedGetPath();

            if (request.RequestContext != null &&
                request.RequestContext.RouteData != null)
            {
                var routeValues = request.RequestContext.RouteData.Values;

                if (routeValues != null && routeValues.Count > 0)
                {
                    object controller;
                    routeValues.TryGetValue("controller", out controller);
                    string controllerString = (controller == null) ? string.Empty : controller.ToString();

                    if (!string.IsNullOrEmpty(controllerString))
                    {
                        object action;
                        routeValues.TryGetValue("action", out action);
                        string actionString = (action == null) ? string.Empty : action.ToString();

                        name = controllerString;
                        if (!string.IsNullOrEmpty(actionString))
                        {
                            name += "/" + actionString;
                        }
                        else
                        {
                            if (routeValues.Keys.Count > 1)
                            {
                                // We want to include arguments because in WebApi action is usually null 
                                // and action is resolved by controller, http method and number of arguments
                                var sortedKeys = routeValues.Keys
                                    .Where(key => !string.Equals(key, "controller", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();

                                string arguments = string.Join(@"/", sortedKeys);
                                name += " [" + arguments + "]";
                            }
                        }
                    }
                }
            }

            if (name.StartsWith("/__browserLink/requestData/", StringComparison.OrdinalIgnoreCase))
            {
                name = "/__browserLink";
            }

            name = request.HttpMethod + " " + name;

            return name;
        }

#if NET46
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
                bool isHierarchicalId = AppInsightsActivity.IsHierarchicalRequestId(parentId);
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
        private static bool TryParseStandardHeaders(
            RequestTelemetry requestTelemetry,
            HttpRequest request)
        {
            var parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);

            // don't bother parsing correlation-context if there was no RequestId
            if (!string.IsNullOrEmpty(parentId))
            {
                var correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);

                bool isHierarchicalId = AppInsightsActivity.IsHierarchicalRequestId(parentId);
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
        private static bool TryParseCustomHeaders(
            HttpRequest request,
            out string rootId,
            out string parentId)
        {
            parentId = request.UnvalidatedGetHeader(ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(RootOperationIdHeaderName);

            return !string.IsNullOrEmpty(rootId) || !string.IsNullOrEmpty(parentId);
        }
    }
}