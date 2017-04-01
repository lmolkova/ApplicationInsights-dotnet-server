using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Microsoft.ApplicationInsights.Common;
using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.ApplicationInsights.Web.Implementation
{
    class AspNetDiagnosticListener : IObserver<DiagnosticListener>, IDisposable
    {
        private const string AspNetListenerName = "Microsoft.AspNet.Correlation";
        private readonly AspNetEventObserver eventObserver = new AspNetEventObserver();
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
                aspNetSubscription = value.Subscribe(eventObserver, AspNetEventObserver.IsEnabled);
            }
        }

        public void Dispose()
        {
            aspNetSubscription?.Dispose();
            allListenerSubscription?.Dispose();
        }

        private class AspNetEventObserver : IObserver<KeyValuePair<string, object>>
        {
            private const string IncomingRequestEventName = "Microsoft.AspNet.HttpReqIn";
            private const string IncomingRequestStopEventName = "Microsoft.AspNet.HttpReqIn.Stop";

            internal static readonly Func<string, object, object, bool> IsEnabled = (name, activityObj, _) =>
            {
                if (name == IncomingRequestEventName)
                {
                    var activity = activityObj as Activity;
                    if (activity == null)
                    {
                        // write event log
                        
                        // ASP.NET HTTP Module API has changed! Let it flow, there is nothing else we can do
                        return true;
                    }

                    if (activity.ParentId == null)
                    {
                        var context = HttpContext.Current;
                        // ParentId is null, means that there was no Request-Id header, which means we have to look for AppInsights/custom headers
                        var request = context.Request;
                        string rootId = request.UnvalidatedGetHeader(ActivityHelpers.RootOperationIdHeaderName);
                        if (!string.IsNullOrEmpty(rootId))
                        {
                            // Got legacy headers from older AppInsights version or some custom header.
                            // Let's set activity ParentId with custom root id
                            activity.SetParentId(rootId);
                        }

                        return true;
                    }
                }

//                var requestTelemetry = CreateRequestTelemetry(rootId, parentId);
  //              context.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, requestTelemetry);
//                ActivityHelpers.StartActivity(context);
//                return false;

                return name == IncomingRequestStopEventName;
            };

            /// <summary>
            /// Parses incoming request headers: initializes Operation Context and stores it in Activity.
            /// </summary>
            /// <param name="rootId">RootId from custom header.</param>
            /// <param name="parentId">ParentID from custom header.</param>/// 
            /// <returns>RequestTelemetry with OperationContext parsed from the request.</returns>
            private static RequestTelemetry CreateRequestTelemetry(string rootId, string parentId)
            {
                RequestTelemetry requestTelemetry = new RequestTelemetry();

                var requestActivity = new Activity("Microsoft.AppInsights.Web.Request");

                var effectiveParent = rootId ?? parentId;
                if (effectiveParent != null)
                {
                    requestActivity.SetParentId(effectiveParent);
                }

                requestActivity.Start();

                // Initialize requestTelemetry Context immediately: 
                // even though it will be initialized with Base OperationCorrelationTelemetryInitializer,
                // activity may be lost in native/managed thread hops.
                requestTelemetry.Context.Operation.ParentId = parentId;
                requestTelemetry.Context.Operation.Id = requestActivity.RootId;
                requestTelemetry.Id = requestActivity.Id;

                return requestTelemetry;
            }

            private static bool TryParseCustomHeaders(HttpRequest request, out string rootId, out string parentId)
            {
                parentId = request.UnvalidatedGetHeader(ActivityHelpers.ParentOperationIdHeaderName);
                rootId = request.UnvalidatedGetHeader(ActivityHelpers.RootOperationIdHeaderName);

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

            public void OnNext(KeyValuePair<string, object> value)
            {

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
