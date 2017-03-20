using System;

namespace Microsoft.ApplicationInsights.Web
{
    using System.Collections.Generic;
    using System.Web;
    using Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ApplicationInsights.TestFramework;

    [TestClass]
    public class OperationCorrelationTelemetryInitializerTests
    {
        [TestMethod]
        public void InitializeDoesNotThrowWhenHttpContextIsNull()
        {
            var source = new OperationCorrelationTelemetryInitializer();
            source.Initialize(new RequestTelemetry());
        }

        [TestMethod]
        public void InitializeSetsParentIdForTelemetryUsingIdFromRequestTelemetry()
        {
            var exceptionTelemetry = new ExceptionTelemetry();
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeDoesNotOverrideCustomerParentOperationId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);

            var customerTelemetry = new TraceTelemetry("Text");
            customerTelemetry.Context.Operation.ParentId = "CustomId";

            source.Initialize(customerTelemetry);

            Assert.AreEqual("CustomId", customerTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeSetsRootOperationIdForTelemetryUsingIdFromRequestTelemetry()
        {
            var exceptionTelemetry = new ExceptionTelemetry();
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;
            requestTelemetry.Context.Operation.Id = "RootId";

            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Context.Operation.Id, exceptionTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeDoesNotOverrideCustomerRootOperationId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;
            requestTelemetry.Context.Operation.Id = "RootId";

            var customerTelemetry = new TraceTelemetry("Text");
            customerTelemetry.Context.Operation.Id = "CustomId";

            source.Initialize(customerTelemetry);

            Assert.AreEqual("CustomId", customerTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeSetsRequestTelemetryRootOperaitonIdToOperaitonId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);

            Assert.AreEqual(AppInsightsActivity.GetRootId(requestTelemetry.Id), requestTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeReadsParentIdFromCustomHeader()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>() { { "headerName", "ParentId" } });
            source.ParentOperationIdHeaderName = "headerName";
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);

            Assert.AreEqual("ParentId", requestTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeReadsRootIdFromCustomHeader()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>() { { "headerName", "RootId" } });
            source.RootOperationIdHeaderName = "headerName";
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);
            Assert.AreEqual("RootId", customerTelemetry.Context.Operation.Id);

            Assert.AreEqual("RootId", requestTelemetry.Context.Operation.Id);
            Assert.AreNotEqual("RootId", requestTelemetry.Id);
            Assert.AreEqual("RootId",AppInsightsActivity.GetRootId(requestTelemetry.Id));
        }

        [TestMethod]
        public void InitializeDoNotMakeRequestAParentOfItself()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual(null, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(AppInsightsActivity.GetRootId(requestTelemetry.Id), requestTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeFromStandardHeadersAlwaysWinsCustomHeaders()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>()
            {
                ["Request-Id"] = "standard-id",
                ["x-ms-request-id"] = "legacy-id",
                ["x-ms-request-rooit-id"] = "legacy-root-id"
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual("standard-id", requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual("standard-id", requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(AppInsightsActivity.GetRootId(requestTelemetry.Id), requestTelemetry.Context.Operation.Id);
            Assert.AreNotEqual(requestTelemetry.Context.Operation.Id, requestTelemetry.Id);
            Assert.AreEqual(0, requestTelemetry.Context.GetCorrelationContext().Count);
        }

        [TestMethod]
        public void InitializeFromStandardHeaderWithCorrelationContext()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "|guid.",
                ["Correlation-Context"] = "k1=v1,k2=v2"
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual("|guid.", requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual("guid", requestTelemetry.Context.Operation.Id);
            Assert.AreEqual("guid", AppInsightsActivity.GetRootId(requestTelemetry.Id));
            Assert.AreNotEqual(requestTelemetry.Context.Operation.Id, requestTelemetry.Id);

            var correationContext = requestTelemetry.Context.GetCorrelationContext();
            Assert.AreEqual("v1", correationContext["k1"]);
            Assert.AreEqual("v2", correationContext["k2"]);
            Assert.AreEqual(2, correationContext.Count);
        }

        [TestMethod]
        public void InitializeFromStandardHeaderWithHierarchicalIdAndCorrelationContextId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "|guid1.",
                ["Correlation-Context"] = "Id=guid2"
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual("|guid1.", requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual("guid2", requestTelemetry.Context.Operation.Id);
            Assert.AreEqual("guid2", AppInsightsActivity.GetRootId(requestTelemetry.Id));

            Assert.AreEqual("guid2", requestTelemetry.Context.GetCorrelationContext()["Id"]);
        }

        [TestMethod]
        public void InitializeFromStandardHeaderWithNonHierarchicalIdAndCorrelationContextId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "guid1",
                ["Correlation-Context"] = "Id=guid2"
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual("guid1", requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual("guid2", requestTelemetry.Context.Operation.Id);
            Assert.AreEqual("guid2", AppInsightsActivity.GetRootId(requestTelemetry.Id));
            Assert.AreNotEqual("guid2", requestTelemetry.Id);

            var correlationContext = requestTelemetry.Context.GetCorrelationContext();
            Assert.AreEqual("guid2", correlationContext["Id"]);
            Assert.AreEqual(1, correlationContext.Count);
        }

        [TestMethod]
        public void InitializeWithoutHeaders()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>());
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.IsNull(requestTelemetry.Context.Operation.ParentId);
            Assert.IsNotNull(requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(requestTelemetry.Context.Operation.Id, AppInsightsActivity.GetRootId(requestTelemetry.Id));
            Assert.AreNotEqual(requestTelemetry.Context.Operation.Id, requestTelemetry.Id);
            Assert.AreEqual(0, requestTelemetry.Context.GetCorrelationContext().Count);
        }

        [TestMethod]
        public void InitializeWithInvalidRequestId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = ""
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.IsNull(requestTelemetry.Context.Operation.ParentId);
            Assert.IsNotNull(requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(requestTelemetry.Context.Operation.Id, AppInsightsActivity.GetRootId(requestTelemetry.Id));
        }

        [TestMethod]
        public void InitializeWithInvalidCorrelationContext()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>
            {
                ["Request-Id"] = "|guid.",
                ["Correlation-Context"] = $"123,k1=v1,k12345678910111213=v2,k3={Guid.NewGuid()}{Guid.NewGuid()}" 
                //key length must be less than 16, value length < 42
            });
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);

            var correationContext = requestTelemetry.Context.GetCorrelationContext();
            Assert.AreEqual(1, correationContext.Count);
            Assert.AreEqual("v1", correationContext["k1"]);
        }

        private class TestableOperationCorrelationTelemetryInitializer : OperationCorrelationTelemetryInitializer
        {
            private readonly HttpContext fakeContext;
            private readonly RequestTelemetry telemetry;

            public TestableOperationCorrelationTelemetryInitializer(IDictionary<string, string> headers)
            {
                 this.fakeContext = HttpModuleHelper.GetFakeHttpContext(headers);
                telemetry = fakeContext.SetOperationHolder().Telemetry;
            }

            public HttpContext FakeContext
            {
                get { return this.fakeContext; }
            }

            public RequestTelemetry Telemetry
            {
                get { return this.telemetry; }
            }

            protected override HttpContext ResolvePlatformContext()
            {
                return this.fakeContext;
            }
        }
    }
}