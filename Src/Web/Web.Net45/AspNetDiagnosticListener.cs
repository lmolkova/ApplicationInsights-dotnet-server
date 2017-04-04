using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Web;
    using Microsoft.ApplicationInsights.Common;

    /// <summary>
    /// Listens to ASP.NET DiagnosticSource and enables instrumentation with Activity: let ASP.NET create root Activity for the request.
    /// </summary>
    internal class AspNetDiagnosticModule : IObserver<DiagnosticListener>, IDisposable, ITelemetryModule
    {
        private const string AspNetListenerName = "Microsoft.AspNet.Diagnostics";
        private const string IncomingRequestEventName = "Microsoft.AspNet.HttpReqIn";
        private const string IncomingRequestStartEventName = "Microsoft.AspNet.HttpReqIn.Start";
        private const string IncomingRequestStopEventName = "Microsoft.AspNet.HttpReqIn.Stop";

        private readonly IDisposable allListenerSubscription;
        private readonly RequestTrackingTelemetryModule requestModule;
        private readonly ExceptionTrackingTelemetryModule exceptionModule;

        private IDisposable aspNetSubscription;
        /// <summary>
        /// Indicates if module initialized successfully.
        /// </summary>
        private bool isEnabled = true;


        public AspNetDiagnosticModule()
        {
            try
            {
                // The call initializes TelemetryConfiguration that will create and Intialize modules
                TelemetryConfiguration configuration = TelemetryConfiguration.Active;

                foreach (var module in TelemetryModules.Instance.Modules)
                {
                    if (module is RequestTrackingTelemetryModule)
                    {
                        this.requestModule = (RequestTrackingTelemetryModule) module;
                    }
                    else if (module is ExceptionTrackingTelemetryModule)
                    {
                        this.exceptionModule = (ExceptionTrackingTelemetryModule) module;
                    }
                }
            }
            catch (Exception exc)
            {
                this.isEnabled = false;
                WebEventSource.Log.WebModuleInitializationExceptionEvent(exc.ToInvariantString());
            }

            this.allListenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }


        /// <summary>
        /// Initializes the telemetry module.
        /// </summary>
        /// <param name="configuration">Telemetry configuration to use for initialization.</param>
        public void Initialize(TelemetryConfiguration configuration)
        {
        }

        public void OnNext(DiagnosticListener value)
        {
            if (this.isEnabled && value.Name == AspNetListenerName)
            {
                this.aspNetSubscription = value.Subscribe(new AspNetEventObserver(this.requestModule, this.exceptionModule),  AspNetEventObserver.IsEnabled);
            }
        }

        public void Dispose()
        {
            this.aspNetSubscription?.Dispose();
            this.allListenerSubscription?.Dispose();
        }

        #region IObserver

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        #endregion

        private class AspNetEventObserver : IObserver<KeyValuePair<string, object>>
        {
            private readonly RequestTrackingTelemetryModule requestModule;
            private readonly ExceptionTrackingTelemetryModule exceptionModule;

            public static Func<string, object, object, bool> IsEnabled => (name, activityObj, _) =>
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
                        }
                    }
                }

                return true;
            };

            public AspNetEventObserver(RequestTrackingTelemetryModule requestModule, ExceptionTrackingTelemetryModule exceptionModule)
            {
                this.requestModule = requestModule;
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                var context = HttpContext.Current;

                if (value.Key == IncomingRequestStartEventName)
                {
                    this.requestModule.OnBeginRequest(context);
                }
                else if (value.Key == IncomingRequestStopEventName)
                {
                    exceptionModule?.OnError(context);
                    requestModule?.OnEndRequest(context);
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
    }
}
