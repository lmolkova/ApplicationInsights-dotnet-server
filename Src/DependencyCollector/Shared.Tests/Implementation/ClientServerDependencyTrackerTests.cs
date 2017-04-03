﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Net;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
#if NET40
    using Microsoft.ApplicationInsights.Net40;
#endif
    using Microsoft.ApplicationInsights.Web.TestFramework;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable 618

    /// <summary>
    /// Tests for client server dependency tracker.
    /// </summary>
    [TestClass]
    public class ClientServerDependencyTrackerTests : IDisposable
    {
        private List<ITelemetry> sendItems;
        private TelemetryClient telemetryClient;
        private WebRequest webRequest;
        private SqlCommand sqlRequest;

        [TestInitialize]
        public void TestInitialize()
        {
            var configuration = new TelemetryConfiguration();
            this.sendItems = new List<ITelemetry>();
            configuration.TelemetryChannel = new StubTelemetryChannel { OnSend = item => this.sendItems.Add(item) };
            configuration.InstrumentationKey = Guid.NewGuid().ToString();
            configuration.TelemetryInitializers.Add(new MockTelemetryInitializer());
            configuration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            this.telemetryClient = new TelemetryClient(configuration);
            this.webRequest = WebRequest.Create(new Uri("http://bing.com"));
            this.sqlRequest = new SqlCommand("select * from table;");
            ClientServerDependencyTracker.PretendProfilerIsAttached = true;
        }

        [TestCleanup]
        public void TestCleanUp()
        {
#if NET45
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
#else
            CorrelationHelper.CleanOperationContext();
#endif
            ClientServerDependencyTracker.PretendProfilerIsAttached = false;
        }

        /// <summary>
        /// Tests if BeginWebTracking() returns operation with associated telemetry item (with start time and time stamp).
        /// </summary>
        [TestMethod]
        public void BeginWebTrackingReturnsOperationItemWithTelemetryItem()
        {
            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            Assert.AreEqual(telemetry.Timestamp, telemetry.Timestamp);

            Assert.IsNull(telemetry.Context.Operation.ParentId);
            Assert.IsNotNull(telemetry.Context.Operation.Id);
            Assert.IsTrue(telemetry.Id.StartsWith('|' + telemetry.Context.Operation.Id));
            Assert.AreEqual(0, telemetry.Context.Properties.Count);
        }

#if NET45
        /// <summary>
        /// Tests if BeginWebTracking() returns operation with associated telemetry item (with operation context).
        /// </summary>
        [TestMethod]
        public void BeginWebTrackingWithParentActivityReturnsOperationItemWithTelemetryItem()
        {
            var parentActivity = new Activity("test");
            parentActivity.SetParentId("|guid.1234_");
            parentActivity.AddBaggage("k", "v");

            parentActivity.Start();

            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            Assert.AreEqual(parentActivity.Id, telemetry.Context.Operation.ParentId);
            Assert.AreEqual(parentActivity.RootId, telemetry.Context.Operation.Id);

            var properties = telemetry.Context.Properties;
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual("v", properties["k"]);
            parentActivity.Stop();
        }
#else
        /// <summary>
        /// Tests if BeginWebTracking() returns operation with associated telemetry item (with operation context).
        /// </summary>
        [TestMethod]
        public void BeginWebTrackingWithParentCallContextReturnsOperationItemWithTelemetryItem()
        {
            var requestTelemetry = new RequestTelemetry { Id = "|guid.1234_" };
            var correlationContext = new Dictionary<string, string> { ["k"] = "v" };
            CorrelationHelper.SetOperationContext(requestTelemetry, correlationContext);

            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            Assert.AreEqual("|guid.1234_", telemetry.Context.Operation.ParentId);
            Assert.AreEqual("guid", telemetry.Context.Operation.Id);

            var properties = telemetry.Context.Properties;
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual("v", properties["k"]);
            CorrelationHelper.CleanOperationContext();
        }
#endif

        /// <summary>
        /// Tests if EndTracking() sends telemetry item on success for web and SQL requests.
        /// </summary>
        [TestMethod]
        public void EndTrackingSendsTelemetryItemOnSuccess()
        {
            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            Assert.AreEqual(1, this.sendItems.Count);

            telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            Assert.AreEqual(2, this.sendItems.Count);
        }

        /// <summary>
        /// Tests if EndTracking() computes the Duration of the telemetry item.
        /// </summary>
        [TestMethod]
        public void EndTrackingComputesTheDurationOfTelemetryItem()
        {
            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            var telemetryItem = this.sendItems[0] as DependencyTelemetry;
            this.ValidateSentTelemetry(telemetryItem);
        }

        /// <summary>
        /// Tests if EndTracking() sends telemetry item with initialized content.
        /// </summary>
        [TestMethod]
        public void EndTrackingTracksTelemetryItemWithInitializedContent()
        {
            var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
            ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            var telemetryItem = this.sendItems[0] as DependencyTelemetry;
            Assert.IsNotNull(telemetryItem.Context.User.Id);
            Assert.IsNotNull(telemetryItem.Context.Session.Id);
            Assert.AreEqual(telemetryItem.Context.User.Id, "UserID");
            Assert.AreEqual(telemetryItem.Context.Session.Id, "SessionID");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetTupleForWebDependenciesThrowsArgumentNullExceptionForNullWebRequest()
        {
            ClientServerDependencyTracker.GetTupleForWebDependencies(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GetTupleForSqlDependenciesThrowsArgumentNullExceptionForNullSqlRequest()
        {
            ClientServerDependencyTracker.GetTupleForSqlDependencies(null);
        }

        [TestMethod]
        public void GetTupleForSqlDependenciesReturnsNullIfEntryDoesNotExistInTables()
        {
             Assert.IsNull(ClientServerDependencyTracker.GetTupleForSqlDependencies(new SqlCommand("select * from table;")));
        }

        [TestMethod]
        public void GetTupleForWebDependenciesReturnsNullIfEntryDoesNotExistInTables()
        {
            Assert.IsNull(ClientServerDependencyTracker.GetTupleForWebDependencies(WebRequest.Create(new Uri("http://bing.com"))));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddTupleForSqlDependenciesThrowsArgumentNullExceptionForNullSqlRequest()
        {
            ClientServerDependencyTracker.AddTupleForSqlDependencies(null, new DependencyTelemetry(), false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddTupleForWebDependenciesThrowsArgumentNullExceptionForNullWebRequest()
        {
            ClientServerDependencyTracker.AddTupleForWebDependencies(null, new DependencyTelemetry(), false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddTupleForSqlDependenciesThrowsArgumentNullExceptionForNullTelemetry()
        {
            ClientServerDependencyTracker.AddTupleForSqlDependencies(new SqlCommand("select * from table;"), null, false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddTupleForWebDependenciesThrowsArgumentNullExceptionForNullTelemetry()
        {
            ClientServerDependencyTracker.AddTupleForWebDependencies(WebRequest.Create(new Uri("http://bing.com")), null, false);
        }

        [TestMethod]
        public void AddTupleForWebDependenciesAddsTelemteryTupleToTheTable()
        {
            var telemetry = new DependencyTelemetry();
            ClientServerDependencyTracker.AddTupleForWebDependencies(this.webRequest, telemetry, false);
            var tuple = ClientServerDependencyTracker.GetTupleForWebDependencies(this.webRequest);
            Assert.IsNotNull(tuple);
            Assert.IsNotNull(tuple.Item1);
            Assert.AreEqual(telemetry, tuple.Item1);
        }

        [TestMethod]
        public void GetTupleForWebDependenciesReturnsNullIfTheItemDoesNotExistInTheTable()
        {
            var tuple = ClientServerDependencyTracker.GetTupleForWebDependencies(this.webRequest);
            Assert.IsNull(tuple);
        }

        [TestMethod]
        public void AddTupleForSqlDependenciesAddsTelemteryTupleToTheTable()
        {
            var telemetry = new DependencyTelemetry();
            ClientServerDependencyTracker.AddTupleForSqlDependencies(this.sqlRequest, telemetry, false);
            var tuple = ClientServerDependencyTracker.GetTupleForSqlDependencies(this.sqlRequest);
            Assert.IsNotNull(tuple);
            Assert.IsNotNull(tuple.Item1);
            Assert.AreEqual(telemetry, tuple.Item1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddTupleForSqlDependenciesThrowsExceptionIfExists()
        {
            var telemetry = new DependencyTelemetry();
            var falseTelemetry = new DependencyTelemetry();
            ClientServerDependencyTracker.AddTupleForSqlDependencies(this.sqlRequest, falseTelemetry, false);
            ClientServerDependencyTracker.AddTupleForSqlDependencies(this.sqlRequest, telemetry, false);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddTupleForWebDependenciesThrowsExceptionIfExists()
        {
            var telemetry = new DependencyTelemetry();
            var falseTelemetry = new DependencyTelemetry();
            ClientServerDependencyTracker.AddTupleForWebDependencies(this.webRequest, falseTelemetry, false);
            ClientServerDependencyTracker.AddTupleForWebDependencies(this.webRequest, telemetry, false);
        }

        [TestMethod]
        public void GetTupleForSqlDependenciesReturnsNullIfTheItemDoesNotExistInTheTable()
        {
            var tuple = ClientServerDependencyTracker.GetTupleForSqlDependencies(this.sqlRequest);
            Assert.IsNull(tuple);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this); 
        }

        private void Dispose(bool dispose)
        {
            if (dispose)
            {
                this.sqlRequest.Dispose();
            }
        }
        
        private void ValidateSentTelemetry(DependencyTelemetry telemetry)
        {
            Assert.AreEqual(telemetry.Timestamp, telemetry.Timestamp);
            Assert.IsTrue(telemetry.Duration.Milliseconds >= 0);
        }
    }
}
