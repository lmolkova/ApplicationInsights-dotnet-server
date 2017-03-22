namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Net;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation;
    using Microsoft.ApplicationInsights.TestFramework;
    using Microsoft.ApplicationInsights.Web.TestFramework;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests .NET 4.6 specific dependency injection behavior
    /// </summary>
    [TestClass]
    public class ClientServerDependencyTrackerTests
    {
        private TelemetryClient telemetryClient;

        [TestInitialize]
        public void TestInitialize()
        {
            var configuration = new TelemetryConfiguration();
            configuration.TelemetryInitializers.Add(new ActivityTelemetryInitializer());
            configuration.InstrumentationKey = Guid.NewGuid().ToString();

            this.telemetryClient = new TelemetryClient(configuration);
            ClientServerDependencyTracker.PretendProfilerIsAttached = true;
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            ClientServerDependencyTracker.PretendProfilerIsAttached = false;
        }

        [TestMethod]
        public void BeginWebTrackingWithoutParentActivity()
        {
            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            Assert.IsNull(telemetry.Context.Operation.ParentId);
            Assert.IsNotNull(telemetry.Context.Operation.Id);
            Assert.AreEqual(0, telemetry.Context.GetCorrelationContext().Count);
        }

        [TestMethod]
        public void BeginWebTrackingWithParentActivity()
        {
            var parentActivity = new Activity("test");
            parentActivity.SetParentId("|guid.");
            parentActivity.AddBaggage("k", "v");

            parentActivity.Start();

            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            Assert.AreEqual("|guid.1", telemetry.Context.Operation.ParentId);
            Assert.AreEqual("guid", telemetry.Context.Operation.Id);

            var correlationContext = telemetry.Context.GetCorrelationContext();
            Assert.AreEqual(1, correlationContext.Count);
            Assert.AreEqual("v", correlationContext["k"]);
            parentActivity.Stop();
        }
    }
}
