namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    internal static class RequestTrackingExtensions
    {
        internal static IOperationHolder<RequestTelemetry> StartOperationPrivate(
            this HttpContext platformContext, TelemetryClient client)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }
            var request = platformContext.GetRequest();
            if (request == null)
            {
                throw new ArgumentException("Request is missing");
            }

            RequestTelemetry requestTelemetry = new RequestTelemetry
            {
                Name = platformContext.CreateRequestNamePrivate()
            };


            string parentId;
            IEnumerable<KeyValuePair<string, string>> correlationContext;
            //Parse standard correlation headers
            //If there are no standard headers, let OperationCorrelaitonInititlizer set Ids.
            if (TryParseStandardHeader(request, out parentId, out correlationContext))
            {
                bool isHierarchicalId = AppInsightsActivity.IsHierarchicalRequestId(parentId);
                requestTelemetry.Context.Operation.ParentId = parentId;
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
                            string.IsNullOrEmpty(item.Value) &&
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

                if (string.IsNullOrEmpty(requestTelemetry.Context.Operation.Id))
                {
                    //ok, upstream gave us non-hirarchical id and no Id in the correlation context
                    //We'll use parentId to generate our Ids.
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(parentId);
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                }
            }

            var result = client.StartOperation(requestTelemetry);
            platformContext.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, result);

            //CallContext/AsyncLocal may be lost on the way to OnPreRequestHandlerExecute and application, store it in HttpContext;
            var currentOperationContext = CallContextHelpers.GetCurrentOperationContext();
            platformContext.Items.Add(RequestTrackingConstants.CallContextItemName, currentOperationContext);

            WebEventSource.Log.WebTelemetryModuleRequestTelemetryCreated();

            return result;
        }

        internal static IOperationHolder<RequestTelemetry> ReadOrStartOperationPrivate(
            this HttpContext platformContext, TelemetryClient client)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }

            var result = platformContext.GetOperation() ??
                         platformContext.StartOperationPrivate(client);

            return result;
        }

        internal static IOperationHolder<RequestTelemetry> GetOperation(this HttpContext context)
        {
            if (context == null)
            {
                return null;
            }

            return context.Items[RequestTrackingConstants.RequestTelemetryItemName] as IOperationHolder<RequestTelemetry>;
        }

        internal static OperationContextForCallContext GetOperationCallContext(this HttpContext context)
        {
            if (context == null)
            {
                return null;
            }

            return context.Items[RequestTrackingConstants.CallContextItemName] as OperationContextForCallContext;
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

        private static bool TryParseStandardHeader(
            HttpRequest request,
            out string parentId,
            out IEnumerable<KeyValuePair<string,string>> correlationContext)
        {
            parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);
            if (!string.IsNullOrEmpty(parentId)) //don't bother parsing correlation-context if there was no RequestId
            {
                correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);
                return true;
            }
            correlationContext = null;
            return false;
        }
    }
}