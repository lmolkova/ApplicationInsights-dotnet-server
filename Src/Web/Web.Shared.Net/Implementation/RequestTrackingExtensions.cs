﻿namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Linq;
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

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

            var requestTelemetry = platformContext.GetOrCreateRequestTelemetry();
            var result = client.StartOperation(requestTelemetry);
            platformContext.Items.Add(RequestTrackingConstants.OperationItemName, result);

            WebEventSource.Log.WebTelemetryModuleRequestTelemetryCreated();

            return null; //result;
        }

        internal static IOperationHolder<RequestTelemetry> GetOrStartOperation(
            this HttpContext platformContext, TelemetryClient telemetryClient)
        {
            var operation = platformContext.GetOperation();
            if (operation == null)
            {
                platformContext.StartOperationPrivate(telemetryClient);
            }

            return operation;
        }

        internal static RequestTelemetry GetOrCreateRequestTelemetry(
            this HttpContext platformContext)
        {
            var telemetry = platformContext.GetRequestTelemetry();
            if (telemetry == null)
            {
                telemetry = new RequestTelemetry();
                platformContext.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, telemetry);
            }

            return telemetry;
        }

        internal static IOperationHolder<RequestTelemetry> GetOperation(this HttpContext context)
        {
            if (context == null)
            {
                return null;
            }

            return context.Items[RequestTrackingConstants.OperationItemName] as IOperationHolder<RequestTelemetry>;
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
    }
}