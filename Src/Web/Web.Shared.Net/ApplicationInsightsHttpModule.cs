﻿namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Reflection;
    using System.Web;

    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.Web.Implementation;

#pragma warning disable 0612

    /// <summary>
    /// Platform agnostic module for web application instrumentation.
    /// </summary>
    public sealed class ApplicationInsightsHttpModule : IHttpModule
    {
        private readonly RequestTrackingTelemetryModule requestModule;
        private readonly ExceptionTrackingTelemetryModule exceptionModule;

        /// <summary>
        /// Indicates if module initialized successfully.
        /// </summary>
        private bool isEnabled = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsHttpModule"/> class.
        /// </summary>
        public ApplicationInsightsHttpModule()
        {
            try
            {
                // The call initializes TelemetryConfiguration that will create and Intialize modules
                TelemetryConfiguration configuration = TelemetryConfiguration.Active;

                foreach (var module in TelemetryModules.Instance.Modules)
                {
                    if (module is RequestTrackingTelemetryModule)
                    {
                        this.requestModule = (RequestTrackingTelemetryModule)module;
                    }
                    else
                    {
                        if (module is ExceptionTrackingTelemetryModule)
                        {
                            this.exceptionModule = (ExceptionTrackingTelemetryModule)module;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                this.isEnabled = false;
                WebEventSource.Log.WebModuleInitializationExceptionEvent(exc.ToInvariantString());
            }
        }

        /// <summary>
        /// Initializes module for a given application.
        /// </summary>
        /// <param name="context">HttpApplication instance.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Context cannot be null")]
        public void Init(HttpApplication context)
        {
            if (this.isEnabled)
            {
                try
                {
                    context.BeginRequest += this.OnBeginRequest;
                    context.EndRequest += this.OnEndRequest;
                    context.PreRequestHandlerExecute += this.OnPreRequestHandlerExecute;
                }
                catch (Exception exc)
                {
                    this.isEnabled = false;
                    WebEventSource.Log.WebModuleInitializationExceptionEvent(exc.ToInvariantString());
                }
            }
        }

        /// <summary>
        /// Required IDisposable implementation.
        /// </summary>
        public void Dispose()
        {
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            if (this.isEnabled)
            {
                HttpApplication httpApplication = (HttpApplication)sender;

                this.TraceCallback("OnBegin", httpApplication);

                if (this.requestModule != null)
                {
                    if (this.requestModule.SetComponentCorrelationHttpHeaders)
                    {
                        this.AddCorreleationHeaderOnSendRequestHeaders(httpApplication);
                    }

                    this.requestModule.OnBeginRequest(httpApplication.Context);
                }

                // Kept for backcompat. Should be removed in 2.3 SDK
                WebEventsPublisher.Log.OnBegin();
            }
        }

        /// <summary>
        /// When sending the response headers, allow request module to add the IKey's target hash.
        /// </summary>
        /// <param name="httpApplication">HttpApplication instance.</param>
        private void AddCorreleationHeaderOnSendRequestHeaders(HttpApplication httpApplication)
        {
            try
            {
                if (httpApplication != null && httpApplication.Response != null)
                {
                    // We use reflection here because 'AddOnSendingHeaders' is only available post .net framework 4.5.2. Hence we call it if we can find it.
                    // Not using reflection would result in MissingMethodException when 4.5 or 4.5.1 is present. 
                    MethodInfo addOnSendingHeadersMethod = httpApplication.Response.GetType().GetMethod("AddOnSendingHeaders");

                    if (addOnSendingHeadersMethod != null)
                    {
                        var parameters = new object[]
                        {
                            new Action<HttpContext>((httpContext) =>
                            {
                                try
                                {
                                    if (this.requestModule != null)
                                    {
                                        this.requestModule.AddTargetHashForResponseHeader(httpApplication.Context);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    WebEventSource.Log.AddTargetHeaderFailedWarning(ex.ToInvariantString());
                                }
                            })
                        };

                        addOnSendingHeadersMethod.Invoke(httpApplication.Response, parameters);
                    }
                }
            }
            catch (Exception ex)
            {
                WebEventSource.Log.HookAddOnSendingHeadersFailedWarning(ex.ToInvariantString());
            }
        }

        private void OnPreRequestHandlerExecute(object sender, EventArgs eventArgs)
        {
            if (this.isEnabled)
            {
                HttpApplication httpApplication = (HttpApplication)sender;

                this.TraceCallback("OnPreRequestHandlerExecute", httpApplication);

                requestModule?.OnPreRequestHandlerExecute(httpApplication.Context);
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            if (this.isEnabled)
            {
                var httpApplication = (HttpApplication)sender;
                this.TraceCallback("OnEndRequest", httpApplication);

                if (this.IsFirstRequest(httpApplication))
                {
                    if (this.exceptionModule != null)
                    {
                        this.exceptionModule.OnError(httpApplication.Context);
                    }

                    if (this.requestModule != null)
                    {
                        this.requestModule.OnEndRequest(httpApplication.Context);
                    }

                    // Kept for backcompat. Should be removed in 2.3 SDK
                    WebEventsPublisher.Log.OnError();
                    WebEventsPublisher.Log.OnEnd();
                }
                else
                {
                    WebEventSource.Log.RequestFiltered();
                }
            }
        }

        private bool IsFirstRequest(HttpApplication application)
        {
            var firstRequest = true;
            try
            {
                if (application.Context != null)
                {
                    firstRequest = application.Context.Items[RequestTrackingConstants.EndRequestCallFlag] == null;
                    if (firstRequest)
                    {
                        application.Context.Items.Add(RequestTrackingConstants.EndRequestCallFlag, true);
                    }
                }
            }
            catch (Exception exc)
            {
                WebEventSource.Log.FlagCheckFailure(exc.ToInvariantString());
            }

            return firstRequest;
        }

        private void TraceCallback(string callback, HttpApplication application)
        {
            if (WebEventSource.Log.IsVerboseEnabled)
            {
                try
                {
                    if (application.Context != null)
                    {
                        // Url.ToString internally builds local member once and then always returns it
                        // During serialization we will anyway call same ToString() so we do not force unnesesary formatting just for tracing 
                        var url = application.Context.Request.UnvalidatedGetUrl();
                        string logUrl = (url != null) ? url.ToString() : string.Empty;

                        WebEventSource.Log.WebModuleCallback(callback, logUrl);
                    }
                }
                catch (Exception exc)
                {
                    WebEventSource.Log.TraceCallbackFailure(callback, exc.ToInvariantString());
                }
            }
        }
    }
}
