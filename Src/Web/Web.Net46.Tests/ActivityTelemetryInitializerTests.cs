using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Common;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Web.Helpers;
using Microsoft.ApplicationInsights.Web.Implementation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ApplicationInsights
{
    [TestClass]
    class ActivityTelemetryInitializerTests
    {
        TestDiagnosticSource diagnosticSource;

        [TestInitialize]
        public void Initialize()
        {
            this.diagnosticSource = new TestDiagnosticSource();
            this.diagnosticSource.StartActivity();
        }

        [TestCleanup]
        public void Cleanup()
        {
            var rootActivity = Activity.Current;
            Debug.Assert(rootActivity != null);

            while (rootActivity.Parent != null)
            {
                rootActivity = rootActivity.Parent;
            }
            this.diagnosticSource.StopActivity(rootActivity);
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

        private const string ActivityName = "Microsoft.AspNet.HttpReqIn";
        private class TestDiagnosticSource
        {
            private readonly DiagnosticSource testSource = new DiagnosticListener("Microsoft.AspNet.Correlation");

            public void StartActivity(Activity activity = null)
            {
                testSource.StartActivity(activity ?? new Activity(ActivityName), new { });
            }

            public void StopActivity(Activity activity = null)
            {
                if (activity == null)
                {
                    activity = Activity.Current;
                }
                Debug.Assert(activity != null);

                testSource.StopActivity(activity, new { });
            }
        }
    }
}
