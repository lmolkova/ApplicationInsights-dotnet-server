namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Web;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    internal sealed class AspNetDiagnosticListener : IObserver<KeyValuePair<string, object>>, IDisposable
    {
        public const string DiagnosticListenerName = "Microsoft.AspNet.Correlation";
        private readonly IDisposable subscription;
        private readonly DiagnosticListenerObserver listenerObserver;
        private readonly RequestTrackingTelemetryModule requestModule;
        private readonly ExceptionTrackingTelemetryModule exceptionModule;

        public AspNetDiagnosticListener(RequestTrackingTelemetryModule requestModule)
        {
            this.listenerObserver = new DiagnosticListenerObserver(this);
            this.subscription = DiagnosticListener.AllListeners.Subscribe(listenerObserver);
            this.requestModule = requestModule;

            foreach (var module in TelemetryModules.Instance.Modules)
            {
                var exModule = module as ExceptionTrackingTelemetryModule;
                if (exModule != null)
                {
                    this.exceptionModule = exModule;
                }
            }
        }

        public void OnNext(KeyValuePair<string, object> evnt)
        {
            var context = HttpContext.Current;

            try
            {
                if (evnt.Key.EndsWith("Start"))
                {
                    this.requestModule.OnBeginRequest(context);
                }
                else if (evnt.Key.EndsWith("Stop"))
                {
                    this.requestModule.OnEndRequest(context);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private class DiagnosticListenerObserver : IObserver<DiagnosticListener>, IDisposable
        {
            private readonly AspNetDiagnosticListener aspNetListener;
            private IDisposable subscription;

            public DiagnosticListenerObserver(AspNetDiagnosticListener aspNetListener)
            {
                this.aspNetListener = aspNetListener;
            }

            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == DiagnosticListenerName)
                {
                    subscription = value.Subscribe(aspNetListener);
                }
            }

            #region IObserverImpl
            public void OnError(Exception error)
            {
            }

            public void OnCompleted()
            {
            }
            #endregion

            public void Dispose()
            {
                subscription?.Dispose();
            }
        }

        #region IObserverImpl
        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
        #endregion

        public void Dispose()
        {
            subscription?.Dispose();
            listenerObserver?.Dispose();
        }
    }
}