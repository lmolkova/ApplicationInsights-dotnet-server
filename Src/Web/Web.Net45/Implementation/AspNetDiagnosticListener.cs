using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Microsoft.ApplicationInsights.Common;

namespace Microsoft.ApplicationInsights.Web.Implementation
{
    /// <summary>
    /// Listens to ASP.NET DiagnosticSource and enables instrumantation with Activity: let ASP.NET create root Activity for the request.
    /// </summary>
    class AspNetDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        private const string AspNetListenerName = "Microsoft.AspNet.Correlation";
        private const string IncomingRequestEventName = "Microsoft.AspNet.HttpReqIn";
        private const string IncomingRequestStartEventName = "Microsoft.AspNet.HttpReqIn.Start";

        private readonly IDisposable allListenerSubscription;
        private IDisposable aspNetSubscription;

        public AspNetDiagnosticListener()
        {
            allListenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == AspNetListenerName)
            {
                aspNetSubscription = value.Subscribe(new AspNetEventObserver(), (name, activityObj, _) =>
                {
                    if (name == IncomingRequestEventName)
                    {
                        var activity = activityObj as Activity;
                        if (activity == null)
                        {
                            // this is a first IsEnabled call without context that ensures that Activity instrumentation is on
                            return true;
                        }

                        // ParentId is null, means that there was no Request-Id header, which means we have to look for AppInsights/custom headers
                        if (activity.ParentId == null)
                        {
                            var context = HttpContext.Current;
                            var request = context.Request;
                            string rootId = request.UnvalidatedGetHeader(ActivityHelpers.RootOperationIdHeaderName);
                            if (!string.IsNullOrEmpty(rootId))
                            {
                                // Got legacy headers from older AppInsights version or some custom header.
                                // Let's set activity ParentId with custom root id
                                activity.SetParentId(rootId);

                                // we need to properly initialize request telemetry and store it in HttpContext
                                var parentId = request.UnvalidatedGetHeader(ActivityHelpers.ParentOperationIdHeaderName);
                                if (!string.IsNullOrEmpty(parentId))
                                {
                                    var requestTelemtry = context.ReadOrCreateRequestTelemetryPrivate();
                                    requestTelemtry.Context.Operation.ParentId = parentId;
                                }
                            }
                        }
                    }

                    return true;
                });
            }
        }

        public void Dispose()
        {
            aspNetSubscription?.Dispose();
            allListenerSubscription?.Dispose();
        }

        private class AspNetEventObserver : IObserver<KeyValuePair<string, object>>
        {
            public void OnNext(KeyValuePair<string, object> value)
            {
                if (value.Key == IncomingRequestStartEventName)
                {
                    var context = HttpContext.Current;
                    var currentActivity = Activity.Current;

                    var requestTelemetry = context.ReadOrCreateRequestTelemetryPrivate();
                    var requestContext = requestTelemetry.Context.Operation;
                    if (string.IsNullOrEmpty(requestContext.Id))
                    {
                        requestContext.Id = currentActivity.RootId;
                        foreach (var item in currentActivity.Baggage)
                        {
                            requestTelemetry.Context.Properties[item.Key] = item.Value;
                        }
                    }

                    // ParentId could be initialized in IsEnabled if legacy/custom headers were received
                    if (string.IsNullOrEmpty(requestContext.ParentId))
                    {
                        requestContext.ParentId = currentActivity.ParentId;
                    }

                    requestTelemetry.Id = currentActivity.Id;

                    // save current activity in case it will be lost and before it is restored in PreRequestHAndlerExecute, 
                    // we will use it in Web.OperationCorrelationTelemetryIntitalizer
                    context.Items[ActivityHelpers.RequestActivityItemName] = currentActivity;
                }
            }

            #region IObserver

            public void OnError(Exception error)
            {
            }

            public void OnCompleted()
            {
            }

            #endregion
        }

        #region IObserver

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        #endregion
    }
}
