namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Implementation;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for DependencyCollectorDiagnosticListener.
    /// </summary>
    public partial class DependencyCollectorDiagnosticListenerTests
    {
        /// <summary>
        /// Tests that OnStartActivity injects headers.
        /// </summary>
        [TestMethod]
        public void OnActivityStartInjectsHeaders()
        {
            var activity = new Activity("System.Net.Http.HttpRequestOut");
            activity.AddBaggage("k", "v");
            activity.Start();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            this.listener.OnActivityStart(request);

            // Request-Id and Correlation-Context are injected by HttpClient
            // check only legacy headers here
            Assert.AreEqual(activity.RootId, request.Headers.GetValues(RequestResponseHeaders.StandardRootIdHeader).Single());
            Assert.AreEqual(activity.Id, request.Headers.GetValues(RequestResponseHeaders.StandardParentIdHeader).Single());
            Assert.AreEqual(mockAppId, GetRequestContextKeyValue(request, RequestResponseHeaders.RequestContextSourceKey));
        }

        /// <summary>
        /// Tests that OnStopActivity tracks telemetry
        /// </summary>
        [TestMethod]
        public void OnActivityStopTracksTelemetry()
        {
            var activity = new Activity("System.Net.Http.HttpRequestOut");
            activity.AddBaggage("k", "v");
            var startTime = DateTime.UtcNow.AddSeconds(-1);
            activity.SetStartTime(startTime);
            activity.Start();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            this.listener.OnActivityStart(request);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            activity.SetEndTime(startTime.AddSeconds(1));
            this.listener.OnActivityStop(response, request, TaskStatus.RanToCompletion);

            var telemetry = sentTelemetry.Single() as DependencyTelemetry;

            Assert.AreEqual("POST /", telemetry.Name);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrlWithScheme, telemetry.Data);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            Assert.AreEqual(1, telemetry.Duration.TotalSeconds);

            Assert.AreEqual(activity.RootId, telemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, telemetry.Context.Operation.ParentId);
            Assert.AreEqual(activity.Id, telemetry.Id);
            Assert.AreEqual("v", telemetry.Context.Properties["k"]);
        }

        /// <summary>
        /// Tests that OnStopActivity tracks cancelled request.
        /// </summary>
        [TestMethod]
        public void OnActivityStopTracksTelemetryForCanceledRequest()
        {
            var activity = new Activity("System.Net.Http.HttpRequestOut");
            activity.Start();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            this.listener.OnActivityStart(request);

            this.listener.OnActivityStop(null, request, TaskStatus.Canceled);

            var telemetry = sentTelemetry.Single() as DependencyTelemetry;

            Assert.AreEqual("Canceled", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Tests that OnStopActivity tracks faulted request.
        /// </summary>
        [TestMethod]
        public void OnActivityStopTracksTelemetryForFaultedRequest()
        {
            var activity = new Activity("System.Net.Http.HttpRequestOut");
            activity.Start();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            this.listener.OnActivityStart(request);

            this.listener.OnActivityStop(null, request, TaskStatus.Faulted);

            var telemetry = sentTelemetry.Single() as DependencyTelemetry;

            Assert.AreEqual("Faulted", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Tests that exception during request processing os tracked with correct context.
        /// </summary>
        [TestMethod]
        public void OnExceptionTracksException()
        {
            var activity = new Activity("System.Net.Http.HttpRequestOut");
            activity.Start();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            this.listener.OnActivityStart(request);

            var exception = new HttpRequestException("http exception");
            this.listener.OnException(exception, request);
            this.listener.OnActivityStop(null, request, TaskStatus.Faulted);

            var dependencyTelemetry = sentTelemetry.Single(t => t is DependencyTelemetry) as DependencyTelemetry;
            var exceptionTelemetry = sentTelemetry.Single(t => t is ExceptionTelemetry) as ExceptionTelemetry;

            Assert.AreEqual(2, sentTelemetry.Count);
            Assert.AreEqual(exception, exceptionTelemetry.Exception);
            Assert.AreEqual(exceptionTelemetry.Context.Operation.Id, dependencyTelemetry.Context.Operation.Id);
            Assert.AreEqual(exceptionTelemetry.Context.Operation.ParentId, dependencyTelemetry.Id);
        }
    }
}