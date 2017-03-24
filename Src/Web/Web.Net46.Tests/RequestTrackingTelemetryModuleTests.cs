/*using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Common;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Web;
using Microsoft.ApplicationInsights.Web.Helpers;
using Microsoft.ApplicationInsights.Web.TestFramework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ApplicationInsights
{
    [TestClass]
    public class RequestTrackingTelemetryModuleTests
    {
        TestDiagnosticSource source;
        RequestTrackingTelemetryModule module;
        private StubTelemetryChannel moduleChannel;
        private IList<ITelemetry> items;

        [TestInitialize]
        public void Initialize()
        {
            this.source = new TestDiagnosticSource();
            this.items = new List<ITelemetry>();
            this.moduleChannel = new StubTelemetryChannel { OnSend = telemetry => this.items.Add(telemetry) };
            var config = new TelemetryConfiguration
            {
                InstrumentationKey = Guid.NewGuid().ToString(),
                TelemetryChannel = moduleChannel
            };
            config.TelemetryInitializers.Add(new ActivityTelemetryInitializer());
            this.module = new RequestTrackingTelemetryModule();
            this.module.Initialize(config);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.moduleChannel = null;
            this.items.Clear();
        }

        [TestMethod]
        public void StartAndStopActivityTracksRequest()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();

            this.source.StartActivity();

            var activity = Activity.Current;

            this.source.StopActivity();
            Assert.AreEqual(1, items.Count);
            var requestTelemetry = items[0] as RequestTelemetry;
            Assert.IsNotNull(requestTelemetry);

            Assert.AreEqual(activity.RootId, requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(activity.Id, requestTelemetry.Id);

            var context = HttpContext.Current;
            Assert.AreEqual(context.Request.Url, requestTelemetry.Url);
            Assert.AreEqual(context.Response.StatusCode.ToString(CultureInfo.InvariantCulture), requestTelemetry.ResponseCode);
        }

        [TestMethod]
        public void StopWithoutStartTracksRequest()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();

            var activity = new Activity(ActivityName);
            activity.Start();

            this.source.StopActivity();

            Assert.AreEqual(1, items.Count);
            var requestTelemetry = items[0] as RequestTelemetry;
            Assert.IsNotNull(requestTelemetry);

            Assert.AreEqual(activity.RootId, requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(activity.Id, requestTelemetry.Id);
        }

        [TestMethod]
        public void StartActivityStoresAndStartsRequestTelemetry()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();

            this.source.StartActivity();
            var requestTelemetry = HttpContext.Current.GetRequestTelemetry();
            Assert.IsNotNull(requestTelemetry);
            Assert.IsTrue(DateTimeOffset.UtcNow >= requestTelemetry.Timestamp);
        }

        private const string ActivityName = "Microsoft.AspNet.HttpReqIn";
        private class TestDiagnosticSource
        {
            private readonly DiagnosticSource testSource = new DiagnosticListener("Microsoft.AspNet.Correlation");

            public void StartActivity(Activity activity = null)
            {
                 testSource.StartActivity(activity ?? new Activity(ActivityName), new { });
            }

            public void StopActivity()
            {
                Debug.Assert(Activity.Current != null);

                testSource.StopActivity(Activity.Current, new {});
            }
        }
    }
}
*/