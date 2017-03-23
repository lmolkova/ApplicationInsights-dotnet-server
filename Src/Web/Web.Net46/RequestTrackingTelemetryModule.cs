using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal sealed class RequestTrackingTelemetryModule : IObserver<KeyValuePair<string, object>>, IDisposable, ITelemetryModule
    {
        public const string DiagnosticListenerName = "Microsoft.AspNet.Correlation";
        private TelemetryClient telemetryClient;
        private IDisposable subscription;
        private DiagnosticListenerObserver listenerObserver;
        private readonly PropertyFetcher restoredFetcher = new PropertyFetcher("isRestored");

        public void Initialize(TelemetryConfiguration configuration)
        {
            this.telemetryClient = new TelemetryClient(configuration);
            this.telemetryClient.Context.GetInternalContext().SdkVersion =
                Implementation.SdkVersionUtils.GetSdkVersion("web:");

            //TODO: cross component correlation

            listenerObserver = new DiagnosticListenerObserver(this);
            subscription = DiagnosticListener.AllListeners.Subscribe(listenerObserver);
        }

        public void OnNext(KeyValuePair<string, object> evnt)
        {
            var context = HttpContext.Current;
            if (context == null)
            {
                WebEventSource.Log.NoHttpContextWarning();
                return;
            }

            object isRestoredObj = restoredFetcher.Fetch(evnt.Value);
            if (isRestoredObj != null && (bool)isRestoredObj)
            //if (Activity.Current.Parent != null)
            {
                // this notifies about the fact that context was lost between OnBegin and PreExecureRequestHanler.
                // New child Activity was created. Let's log it and ignore, we've got what we need to the request on first Start event
                // TODO: new log event
                // WebEventSource.Log.
                return;
            }

            if (evnt.Key.EndsWith("Start"))
            {
                //TODO: any logging?
                //TODO: why OnBegin could be called twice on WCF and can we avoid it here?
                var requestTelemetry = context.Items[RequestTrackingConstants.RequestTelemetryItemName] as RequestTelemetry;
                if (requestTelemetry != null)
                {
                    return;
                }

                requestTelemetry = new RequestTelemetry();
                requestTelemetry.Start();
                context.Items[RequestTrackingConstants.RequestTelemetryItemName] = requestTelemetry;
            }
            else if (evnt.Key.EndsWith("Exception"))
            {
                var errors = context.AllErrors;

                if (errors != null && errors.Length > 0)
                {
                    foreach (Exception exp in errors)
                    {
                        var exceptionTelemetry = new ExceptionTelemetry(exp);
                        if (context.Response.StatusCode >= 500)
                        {
                            exceptionTelemetry.SeverityLevel = SeverityLevel.Critical;
                        }

                        this.telemetryClient.TrackException(exceptionTelemetry);
                    }
                }
            }
            else if (evnt.Key.EndsWith("Stop"))
            {
                //TODO: any logging?
                if (this.telemetryClient == null)
                {
                    return;
                }

                var requestTelemetry = context.Items[RequestTrackingConstants.RequestTelemetryItemName] as RequestTelemetry;
                if (requestTelemetry == null)
                {
                    requestTelemetry = new RequestTelemetry();
                    requestTelemetry.Start();
                    context.Items[RequestTrackingConstants.RequestTelemetryItemName] = requestTelemetry;
                }

                telemetryClient.Initialize(requestTelemetry);

                // Success will be set in Sanitize on the base of ResponseCode 
                if (string.IsNullOrEmpty(requestTelemetry.ResponseCode))
                {
                    requestTelemetry.ResponseCode = context.Response.StatusCode.ToString(CultureInfo.InvariantCulture);
                }

                if (requestTelemetry.Url == null)
                {
                    requestTelemetry.Url = context.Request.UnvalidatedGetUrl();
                }

                if (string.IsNullOrEmpty(requestTelemetry.Source))
                {
                    //TODO: cross correlation
                }

                this.telemetryClient.TrackRequest(requestTelemetry);
            }
        }

        private class DiagnosticListenerObserver : IObserver<DiagnosticListener>, IDisposable
        {
            private readonly RequestTrackingTelemetryModule requestTrackingModule;
            private IDisposable subscription;

            public DiagnosticListenerObserver(RequestTrackingTelemetryModule requestTrackingModule)
            {
                this.requestTrackingModule = requestTrackingModule;
            }

            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == DiagnosticListenerName)
                {
                    subscription = value.Subscribe(requestTrackingModule);
                }
            }

            public void OnError(Exception error)
            {
            }

            public void OnCompleted()
            {
            }

            public void Dispose()
            {
                subscription?.Dispose();
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public void Dispose()
        {
            subscription?.Dispose();
            listenerObserver?.Dispose();
        }

        /// <summary>
        /// Implements efficient Reflection fetching for properties:
        /// see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs 
        /// </summary>
        private class PropertyFetcher
        {
            public PropertyFetcher(string propertyName)
            {
                this.propertyName = propertyName;
            }

            public object Fetch(object obj)
            {
                if (innerFetcher == null)
                {
                    innerFetcher =
                        PropertyFetch.FetcherForProperty(obj.GetType().GetTypeInfo().GetDeclaredProperty(propertyName));
                }

                return innerFetcher?.Fetch(obj);
            }

            private PropertyFetch innerFetcher;
            private readonly string propertyName;

            class PropertyFetch
            {
                /// <summary>
                /// Create a property fetcher from a .NET Reflection PropertyInfo class that
                /// represents a property of a particular type.  
                /// </summary>
                public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
                {
                    if (propertyInfo == null)
                        return new PropertyFetch(); // returns null on any fetch.

                    var typedPropertyFetcher = typeof(TypedFetchProperty<,>);
                    var instantiatedTypedPropertyFetcher = typedPropertyFetcher.GetTypeInfo().MakeGenericType(
                        propertyInfo.DeclaringType, propertyInfo.PropertyType);
                    return (PropertyFetch) Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
                }

                /// <summary>
                /// Given an object, fetch the property that this propertyFech represents. 
                /// </summary>
                public virtual object Fetch(object obj)
                {
                    return null;
                }

                private class TypedFetchProperty<TObject, TProperty> : PropertyFetch
                {
                    public TypedFetchProperty(PropertyInfo property)
                    {
                        _propertyFetch =
                            (Func<TObject, TProperty>)
                            property.GetMethod.CreateDelegate(typeof(Func<TObject, TProperty>));
                    }

                    public override object Fetch(object obj)
                    {
                        return _propertyFetch((TObject) obj);
                    }

                    private readonly Func<TObject, TProperty> _propertyFetch;
                }
            }
        }
    }
}