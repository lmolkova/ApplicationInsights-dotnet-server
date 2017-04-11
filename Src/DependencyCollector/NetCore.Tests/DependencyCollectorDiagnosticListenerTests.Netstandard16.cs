namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Implementation;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    /// <summary>
    /// Unit tests for DependencyCollectorDiagnosticListener.
    /// </summary>
    [TestClass]
    public partial class DependencyCollectorDiagnosticListenerTests
    {
        private const string requestUrl = "www.example.com";
        private const string requestUrlWithScheme = "https://" + requestUrl;
        private const string okResultCode = "200";
        private const string notFoundResultCode = "404";
        private const string mockAppId = "MOCK_APP_ID";
        private const string mockAppId2 = "MOCK_APP_ID_2";

        private static string GetApplicationInsightsTarget(string targetApplicationId)
        {
            return $"{requestUrl} | {targetApplicationId}";
        }

        private string instrumentationKey;
        private StubTelemetryChannel telemetryChannel;
        private MockCorrelationIdLookupHelper mockCorrelationIdLookupHelper;
        private DependencyCollectorDiagnosticListener listener;

        private List<ITelemetry> sentTelemetry = new List<ITelemetry>();

        /// <summary>
        /// Initialize function that gets called once before any tests in this class are run.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            instrumentationKey = Guid.NewGuid().ToString();

            telemetryChannel = new StubTelemetryChannel()
            {
                EndpointAddress = "https://endpointaddress",
                OnSend = sentTelemetry.Add
            };

            mockCorrelationIdLookupHelper = new MockCorrelationIdLookupHelper(new Dictionary<string, string>()
            {
                [instrumentationKey] = mockAppId
            });

            listener = new DependencyCollectorDiagnosticListener(new TelemetryConfiguration()
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = instrumentationKey,
            },
            mockCorrelationIdLookupHelper);
        }

        /// <summary>
        /// Call OnRequest() with no uri in the HttpRequestMessage.
        /// </summary>
        [TestMethod]
        public void OnRequestWithRequestEventWithNoRequestUri()
        {
            listener.OnRequest(new HttpRequestMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        //TODODODODODODODODO
        /// <summary>
        /// Call OnRequest() with valid arguments.
        /// </summary>
        [TestMethod]
        public void OnRequestWithRequestEvent()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);

            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("POST /", telemetry.Name);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrlWithScheme, telemetry.Data);
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            Assert.AreEqual(mockAppId, GetRequestContextKeyValue(request, RequestResponseHeaders.RequestContextSourceKey));
            Assert.AreEqual(null, GetRequestContextKeyValue(request, RequestResponseHeaders.StandardRootIdHeader));
            Assert.IsFalse(string.IsNullOrEmpty(GetRequestHeaderValues(request, RequestResponseHeaders.StandardParentIdHeader).SingleOrDefault()));

            Assert.AreEqual(0, sentTelemetry.Count);
        }

        private static IEnumerable<string> GetRequestHeaderValues(HttpRequestMessage request, string headerName)
        {
            return HttpHeadersUtilities.GetHeaderValues(request.Headers, headerName);
        }

        private static string GetRequestContextKeyValue(HttpRequestMessage request, string keyName)
        {
            return HttpHeadersUtilities.GetRequestContextKeyValue(request.Headers, keyName);
        }

        /// <summary>
        /// Call OnResponse() when no matching OnRequest() call has been made.
        /// </summary>
        [TestMethod]
        public void OnResponseWithResponseEventButNoMatchingRequest()
        {
            listener.OnResponse(new HttpResponseMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        /// <summary>
        /// Call OnResponse() with a successful request but no target instrumentation key in the response headers.
        /// </summary>
        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result but no target instrumentation key in the response headers.
        /// </summary>
        [TestMethod]
        public void OnResponseWithNotFoundResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a successful request and same target instrumentation key in the response headers as the source instrumentation key.
        /// </summary>
        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add(RequestResponseHeaders.RequestContextTargetKey, mockAppId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result code and same target instrumentation key in the response headers as the source instrumentation key.
        /// </summary>
        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            response.Headers.Add(RequestResponseHeaders.RequestContextTargetKey, mockAppId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a successful request and different target instrumentation key in the response headers than the source instrumentation key.
        /// </summary>
        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            string targetApplicationId = mockAppId2;
            HttpHeadersUtilities.SetRequestContextKeyValue(response.Headers, RequestResponseHeaders.RequestContextTargetKey, targetApplicationId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.AI, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetApplicationId), telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result code and different target instrumentation key in the response headers than the source instrumentation key.
        /// </summary>
        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            string targetApplicationId = mockAppId2;
            HttpHeadersUtilities.SetRequestContextKeyValue(response.Headers, RequestResponseHeaders.RequestContextTargetKey, targetApplicationId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.AI, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetApplicationId), telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a successful request but no target instrumentation key in the response headers.
        /// </summary>
        [TestMethod]
        public void OnResponseWithParentActivity()
        {
            var parentActivity = new Activity("incoming_request");
            parentActivity.AddBaggage("k1", "v1");
            parentActivity.AddBaggage("k2", "v2");
            parentActivity.Start();

            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);
            Assert.IsNotNull(Activity.Current);

            var parentId = request.Headers.GetValues(RequestResponseHeaders.RequestIdHeader).Single();
            Assert.AreEqual(Activity.Current.Id, parentId);

            var correlationContextHeader = request.Headers.GetValues(RequestResponseHeaders.CorrelationContextHeader).ToArray();
            Assert.AreEqual(2, correlationContextHeader.Length);
            Assert.IsTrue(correlationContextHeader.Contains("k1=v1"));
            Assert.IsTrue(correlationContextHeader.Contains("k2=v2"));

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single().Telemetry;

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(parentActivity, Activity.Current);
            Assert.AreEqual(parentId, telemetry.Id);
            Assert.AreEqual(parentActivity.RootId, telemetry.Context.Operation.Id);
            Assert.AreEqual(parentActivity.Id, telemetry.Context.Operation.ParentId);

            parentActivity.Stop();
        }
    }
}