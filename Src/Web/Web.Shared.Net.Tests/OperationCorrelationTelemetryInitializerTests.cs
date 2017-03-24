using Microsoft.ApplicationInsights.Common;

namespace Microsoft.ApplicationInsights.Web
{
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // OperationCorrelationTelemetryInitializer is now just a stub to set custom headers
    // De facto these are tests for RequestTracingExtensions.CreateRequestTelemetryPrivate
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
        public void InitializeDoesNotOverrideCustomerParentOperationId()
        {
            var customerTelemetry = new TraceTelemetry("Text");
            customerTelemetry.Context.Operation.ParentId = "CustomId";

            new Extensibility.OperationCorrelationTelemetryInitializer().Initialize(customerTelemetry);

            Assert.AreEqual("CustomId", customerTelemetry.Context.Operation.ParentId);
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

            Assert.AreEqual("myParentHeader", RequestTrackingExtensions.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", RequestTrackingExtensions.RootOperationIdHeaderName);

            Assert.AreEqual("myParentHeader", initializer.ParentOperationIdHeaderName);
            Assert.AreEqual("myRootHeader", initializer.RootOperationIdHeaderName);
        }

    }
}