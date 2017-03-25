namespace Microsoft.ApplicationInsights.Web
{
    using System.Web;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        public void DefaultHeadersOperationCorrelationTelemetryInitializerAreSet()
        {
            var initializer = new OperationCorrelationTelemetryInitializer();
            Assert.AreEqual(RequestResponseHeaders.StandardParentIdHeader, initializer.ParentOperationIdHeaderName);
            Assert.AreEqual(RequestResponseHeaders.StandardRootIdHeader, initializer.RootOperationIdHeaderName);
        }

        [TestMethod]
        public void CustomHeadersOperationCorrelationTelemetryInitializerAreSetProperly()
        {
            var initializer = new OperationCorrelationTelemetryInitializer();
            initializer.ParentOperationIdHeaderName = "myParentHeader";
            initializer.RootOperationIdHeaderName = "myRootHeader";

            Assert.AreEqual("myParentHeader", ActivityHelpers.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", ActivityHelpers.RootOperationIdHeaderName);

            Assert.AreEqual("myParentHeader", initializer.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", initializer.RootOperationIdHeaderName);
        }

        [TestMethod]
        public void OperationContextIsSetForNonRequestTelemetryWithoutCallContext()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer();
            
            // create telemetry and immediately clean call context
            var requestTelemetry = source.FakeContext.CreateRequestTelemetryPrivate();
            ActivityHelpers.StopActivity();

            var exceptionTelemetry = new ExceptionTelemetry();
            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Context.Operation.Id, exceptionTelemetry.Context.Operation.Id);
            Assert.AreEqual(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeDoesNotOverrideCustomOperationContext()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer();
            
            // create telemetry and immediately clean call context
            source.FakeContext.CreateRequestTelemetryPrivate();
            ActivityHelpers.StopActivity();

            var customerTelemetry = new TraceTelemetry();
            customerTelemetry.Context.Operation.ParentId = "CustomParentId";
            customerTelemetry.Context.Operation.Id = "CustomRootId";

            source.Initialize(customerTelemetry);

            Assert.AreEqual("CustomParentId", customerTelemetry.Context.Operation.ParentId);
            Assert.AreEqual("CustomRootId", customerTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void OperationContextIsNotSetForNonRequestTelemetryWithCallContext()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer();
            var requestTelemetry = source.FakeContext.CreateRequestTelemetryPrivate();

            var traceTelemetry = new TraceTelemetry("Text");
            source.Initialize(traceTelemetry);

            Assert.IsNull(traceTelemetry.Context.Operation.Id);
            Assert.IsNull(traceTelemetry.Context.Operation.ParentId);

            new Extensibility.OperationCorrelationTelemetryInitializer().Initialize(traceTelemetry);
            Assert.AreEqual(requestTelemetry.Context.Operation.Id, traceTelemetry.Context.Operation.Id);
            Assert.AreEqual(requestTelemetry.Id, traceTelemetry.Context.Operation.ParentId);
        }

        private class TestableOperationCorrelationTelemetryInitializer : OperationCorrelationTelemetryInitializer
        {
            private readonly HttpContext fakeContext;

            public TestableOperationCorrelationTelemetryInitializer()
            {
                this.fakeContext = HttpModuleHelper.GetFakeHttpContext();
            }

            public HttpContext FakeContext
            {
                get { return this.fakeContext; }
            }

            protected override HttpContext ResolvePlatformContext()
            {
                return this.fakeContext;
            }
        }
    }
}