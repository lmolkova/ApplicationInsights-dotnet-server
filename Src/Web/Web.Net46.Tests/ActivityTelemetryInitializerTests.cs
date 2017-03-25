namespace Microsoft.ApplicationInsights
{
    using System.Diagnostics;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ActivityTelemetryInitializerTests
    {
        private TestDiagnosticSource diagnosticSource;

        [TestInitialize]
        public void Initialize()
        {
            this.diagnosticSource = new TestDiagnosticSource();
            this.diagnosticSource.StartActivity();
        }

        [TestCleanup]
        public void Cleanup()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        [TestMethod]
        public void InitializeDoesNotThrowWhenHttpContextIsNull()
        {
            var source = new ActivityTelemetryInitializer();
            source.Initialize(new RequestTelemetry());
        }

        [TestMethod]
        public void InitializeSetsParentIdForTelemetryActivityParentId()
        {
            var exceptionTelemetry = new ExceptionTelemetry();
            var source = new ActivityTelemetryInitializer();

            var context = HttpModuleHelper.GetFakeHttpContext();
            var requestTelemetry = context.CreateRequestTelemetryPrivate();

            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);
        }

        private class TestDiagnosticSource
        {
            private const string ActivityName = "Microsoft.AspNet.HttpReqIn";
            private readonly DiagnosticSource testSource = new DiagnosticListener("Microsoft.AspNet.Correlation");

            public void StartActivity(Activity activity = null)
            {
                this.testSource.StartActivity(activity ?? new Activity(ActivityName), new { });
            }

            public void StopActivity(Activity activity = null)
            {
                if (activity == null)
                {
                    activity = Activity.Current;
                }

                Debug.Assert(activity != null, "activity nust not be null");

                this.testSource.StopActivity(activity, new { });
            }
        }
    }
}
