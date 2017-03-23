using System;
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
using Microsoft.ApplicationInsights.Web.Implementation;
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

            TelemetryConfiguration.Active.TelemetryInitializers.Add(new ActivityTelemetryInitializer());

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

            TelemetryConfiguration.Active.TelemetryInitializers.Add(new ActivityTelemetryInitializer());

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
        public void StopDoesNotUpdateUrlAndStatusCodeIfSetBefore()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();
            TelemetryConfiguration.Active.TelemetryInitializers.Add(new ActivityTelemetryInitializer());

            this.source.StartActivity();
            var requestTelemetry = HttpContext.Current.GetRequestTelemetry();

            requestTelemetry.ResponseCode = "response_code";
            requestTelemetry.Url = new Uri("http://bing.com");
            this.source.StopActivity();

            Assert.AreEqual(1, items.Count);
            var trackedRequestTelemetry = items[0] as RequestTelemetry;
            Assert.AreEqual(requestTelemetry.ResponseCode, trackedRequestTelemetry.ResponseCode);
            Assert.AreEqual(requestTelemetry.Url, trackedRequestTelemetry.Url);
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

        [TestMethod]
        public void RestoredActivityIsNotTracked()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();

            TelemetryConfiguration.Active.TelemetryInitializers.Add(new ActivityTelemetryInitializer());
            this.source.StartActivity();
            var activity = Activity.Current;
            this.source.StartActivity(isRestored:true);
            this.source.StopActivity(true);
            this.source.StopActivity();
            Assert.AreEqual(1, items.Count);
            var requestTelemetry = items[0] as RequestTelemetry;
            Assert.IsNotNull(requestTelemetry);

            Assert.AreEqual(activity.RootId, requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(activity.Id, requestTelemetry.Id);
        }

        [TestMethod]
        public void RequestAndExceptionAreTrackedOnError()
        {
            HttpContext.Current = HttpModuleHelper.GetFakeHttpContext();
            TelemetryConfiguration.Active.TelemetryInitializers.Add(new ActivityTelemetryInitializer());

            this.source.StartActivity();

            var activity = Activity.Current;

            var exception = new Exception("123");
            this.source.SendException(exception);
            this.source.StopActivity();
            Assert.AreEqual(2, items.Count);
            var exceptionTelemetry = items[0] as ExceptionTelemetry;
            var requestTelemetry = items[1] as RequestTelemetry;
            Assert.IsNotNull(exceptionTelemetry);
            Assert.IsNotNull(requestTelemetry);

            Assert.AreEqual(activity.RootId, exceptionTelemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, exceptionTelemetry.Context.Operation.ParentId);
            Assert.AreSame(exception, exceptionTelemetry.Exception);

            Assert.AreEqual(activity.RootId, requestTelemetry.Context.Operation.Id);
            Assert.AreEqual(activity.ParentId, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(activity.Id, requestTelemetry.Id);
        }

        private const string ActivityName = "Microsoft.AspNet.HttpReqIn";
        private class TestDiagnosticSource
        {
            private readonly DiagnosticSource testSource = new DiagnosticListener("Microsoft.AspNet.Correlation");

            public void StartActivity(Activity activity = null, bool isRestored = false)
            {
                if (activity == null)
                {
                    activity = new Activity(ActivityName);
                }

                object payload = new {};
                if (isRestored)
                {
                    payload = new {IsRestored = true};
                }

                testSource.StartActivity(activity, payload);
            }

            public void StopActivity(bool isRestored = false)
            {
                Debug.Assert(Activity.Current != null);

                object payload = new { };
                if (isRestored)
                {
                    payload = new { IsRestored = true };
                }

                testSource.StopActivity(Activity.Current, payload);
            }

            public void SendException(Exception ex)
            {
                Debug.Assert(Activity.Current != null);

                testSource.Write("Microsoft.AspNet.Exception", new {Exception = ex});
            }
        }
    }
}
