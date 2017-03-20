﻿namespace Microsoft.ApplicationInsights.Web.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Web;
    using System.Web.Hosting;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using VisualStudio.TestTools.UnitTesting;

    internal static class HttpModuleHelper
    {
        public const string UrlHost = "http://test.microsoft.com";
        public const string UrlPath = "/SeLog.svc/EventData";
        public const string UrlQueryString = "eventDetail=2";

        public static PrivateObject CreateTestModule(RequestStatus requestStatus = RequestStatus.Success)
        {
            InitializeTelemetryConfiguration();

            switch (requestStatus)
            {
                case RequestStatus.Success:
                    {
                        HttpContext.Current = GetFakeHttpContext();
                        break;
                    }

                case RequestStatus.RequestFailed:
                    {
                        HttpContext.Current = GetFakeHttpContextForFailedRequest();
                        break;
                    }

                case RequestStatus.ApplicationFailed:
                    {
                        HttpContext.Current = GetFakeHttpContextForFailedApplication();
                        break;
                    }
            }

            PrivateObject moduleWrapper = new PrivateObject(typeof(ApplicationInsightsHttpModule));

            return moduleWrapper;
        }

        public static HttpApplication GetFakeHttpApplication()
        {
            var httpContext = GetFakeHttpContext();
            var httpApplicationWrapper = new PrivateObject(typeof(HttpApplication), null);

            httpApplicationWrapper.SetField("_context", httpContext);

            return (HttpApplication)httpApplicationWrapper.Target;
        }

        public static HttpContext GetFakeHttpContext(IDictionary<string, string> headers = null)
        {
            Thread.GetDomain().SetData(".appPath", string.Empty);
            Thread.GetDomain().SetData(".appVPath", string.Empty);

            var workerRequest = new SimpleWorkerRequestWithHeaders(UrlPath, UrlQueryString, new StringWriter(CultureInfo.InvariantCulture), headers);
            
            return new HttpContext(workerRequest);
        }

        public static HttpContext GetFakeHttpContextForFailedRequest()
        {
            var httpContext = GetFakeHttpContext();
            httpContext.Response.StatusCode = 500;
            return httpContext;
        }

        public static HttpContext GetFakeHttpContextForFailedApplication()
        {
            var httpContext = GetFakeHttpContextForFailedRequest();
            httpContext.AddError(new WebException("Exception1", new ApplicationException("Exception1")));
            httpContext.AddError(new ApplicationException("Exception2"));

            return httpContext;
        }

        /*public static HttpContext AddRequestTelemetry(this HttpContext context, RequestTelemetry requestTelemetry)
        {
            context.Items["Microsoft.ApplicationInsights.RequestTelemetry"] = new TestOperationHolder(requestTelemetry);
            return context;
        }*/

        public static HttpContext AddRequestCookie(this HttpContext context, HttpCookie cookie)
        {
            context.Request.Cookies.Add(cookie);
            return context;
        }

        private static void InitializeTelemetryConfiguration()
        {
            TelemetryModules.Instance.Modules.Clear();
            TelemetryModules.Instance.Modules.Add(new RequestTrackingTelemetryModule());
        }

        private class SimpleWorkerRequestWithHeaders : SimpleWorkerRequest
        {
            private readonly IDictionary<string, string> headers;

            public SimpleWorkerRequestWithHeaders(string page, string query, TextWriter output, IDictionary<string, string> headers)
                : base(page, query, output)
            {
                if (headers != null)
                {
                    this.headers = headers;
                }
                else
                {
                    this.headers = new Dictionary<string, string>();
                }
            }

            public override string[][] GetUnknownRequestHeaders()
            {
                List<string[]> result = new List<string[]>();

                foreach (var header in this.headers)
                {
                    result.Add(new string[] { header.Key, header.Value }); 
                }

                var baseResult = base.GetUnknownRequestHeaders();
                if (baseResult != null)
                {
                    result.AddRange(baseResult);
                }

                return result.ToArray();
            }

            public override string GetUnknownRequestHeader(string name)
            {
                if (this.headers.ContainsKey(name))
                {
                    return this.headers[name];
                }

                return base.GetUnknownRequestHeader(name);
            }

            public override string GetKnownRequestHeader(int index)
            {
                var name = HttpWorkerRequest.GetKnownRequestHeaderName(index);

                if (this.headers.ContainsKey(name))
                {
                    return this.headers[name];
                }

                return base.GetKnownRequestHeader(index);
            }
        }
    }
}