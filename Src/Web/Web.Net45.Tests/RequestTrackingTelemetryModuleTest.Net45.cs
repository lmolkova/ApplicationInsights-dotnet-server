namespace Microsoft.ApplicationInsights.Web
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Web;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Assert = Xunit.Assert;

    public partial class RequestTrackingTelemetryModuleTest
    {
        private DiagnosticListener diagnosticListener = new DiagnosticListener("Microsoft.AspNet.Correlation");

        [TestCleanup]
        public void Cleanup()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        [TestMethod]
        public void OnBeginSetsOperationContextWithStandardHeaders()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("|guid1.1").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new {});

            module.OnBeginRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();

            // initialize telemetry
            module.OnEndRequest(context);

            Assert.Equal("guid1", requestTelemetry.Context.Operation.Id);
            Assert.Equal("|guid1.1", requestTelemetry.Context.Operation.ParentId);

            Assert.True(requestTelemetry.Id.StartsWith("|guid1.1."));
            Assert.NotEqual("|guid1.1", requestTelemetry.Id);
            Assert.Equal("guid1", this.GetActivityRootId(requestTelemetry.Id));
            Assert.Equal("v", requestTelemetry.Properties["k"]);
        }

        [TestMethod]
        public void OnBeginSetsOperationContextWithStandardHeadersWithNonHierarchialId()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("guid1").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();
            module.OnEndRequest(context);

            Assert.Equal("guid1", requestTelemetry.Context.Operation.Id);
            Assert.Equal("guid1", requestTelemetry.Context.Operation.ParentId);

            Assert.True(requestTelemetry.Id.StartsWith("|guid1."));
            Assert.NotEqual("|guid1.1.", requestTelemetry.Id);
            Assert.Equal("guid1", this.GetActivityRootId(requestTelemetry.Id));

            // will initialize telemetry
            module.OnEndRequest(context);
            Assert.Equal("v", requestTelemetry.Properties["k"]);
        }

        [TestMethod]
        public void OnBeginSetsOperationContextWithoutHeaders()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();
            module.OnEndRequest(context);

            var operationId = requestTelemetry.Context.Operation.Id;
            Assert.NotNull(operationId);
            Assert.Null(requestTelemetry.Context.Operation.ParentId);
            Assert.True(requestTelemetry.Id.StartsWith('|' + operationId + '.'));
            Assert.NotEqual(operationId, requestTelemetry.Id);
        }

        [TestMethod]
        public void InitializeFromStandardHeadersAlwaysWinsCustomHeaders()
        {
            var context = HttpModuleHelper.GetFakeHttpContext(new Dictionary<string, string>
            {
                ["x-ms-request-id"] = "legacy-id",
                ["x-ms-request-rooit-id"] = "legacy-root-id"
            });

            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it. It will read Request-Id header and we will not update parent, since it's already set
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("standard-id").AddBaggage("k", "v");
            diagnosticListener.IsEnabled("Microsoft.AspNet.HttpReqIn", activity);
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);

            var requestTelemetry = context.GetRequestTelemetry();

            // initialize telemetry
            module.OnEndRequest(context);
            Assert.Equal("standard-id", requestTelemetry.Context.Operation.ParentId);
            Assert.Equal("standard-id", requestTelemetry.Context.Operation.Id);
            Assert.Equal("standard-id", this.GetActivityRootId(requestTelemetry.Id));
            Assert.NotEqual(requestTelemetry.Context.Operation.Id, requestTelemetry.Id);
        }

        [TestMethod]
        public void OnBeginSetsOperationContextWithLegacyHeaders()
        {
            var context = HttpModuleHelper.GetFakeHttpContext(new Dictionary<string, string>
            {
                ["x-ms-request-id"] = "guid1",
                ["x-ms-request-root-id"] = "guid2"
            });

            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it. It will no find Request-Id header and we will update parent
            var activity = new Activity("Microsoft.AspNet.HttpReqIn");
            diagnosticListener.IsEnabled("Microsoft.AspNet.HttpReqIn", activity);
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);
            var requestTelemetry = context.GetRequestTelemetry();
            module.OnEndRequest(context);

            Assert.Equal("guid2", requestTelemetry.Context.Operation.Id);
            Assert.Equal("guid1", requestTelemetry.Context.Operation.ParentId);

            Assert.True(requestTelemetry.Id.StartsWith("|guid2."));
        }


        [TestMethod]
        public void OnBeginReadsRootAndParentIdFromCustomHeader()
        {
            var context = HttpModuleHelper.GetFakeHttpContext(new Dictionary<string, string>
            {
                ["parentHeaderName"] = "ParentId",
                ["rootHeaderName"] = "RootId"
            });

            var config = this.CreateDefaultConfig(context, rootIdHeaderName: "rootHeaderName", parentIdHeaderName: "parentHeaderName");
            var module = this.RequestTrackingTelemetryModuleFactory(config);
            
            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it. It will no find Request-Id header and we will update parent
            var activity = new Activity("Microsoft.AspNet.HttpReqIn");
            diagnosticListener.IsEnabled("Microsoft.AspNet.HttpReqIn", activity);
            diagnosticListener.StartActivity(activity, new { });
                        
            module.OnBeginRequest(context);

            var requestTelemetry = context.GetRequestTelemetry();

            Assert.Equal("ParentId", requestTelemetry.Context.Operation.ParentId);

            Assert.Equal("RootId", requestTelemetry.Context.Operation.Id);
            Assert.NotEqual("RootId", requestTelemetry.Id);
            Assert.Equal("RootId", this.GetActivityRootId(requestTelemetry.Id));
        }

        [TestMethod]
        public void OnBeginTelemetryCreatedWithinRequestScopeIsRequestChild()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var config = this.CreateDefaultConfig(context);
            var module = this.RequestTrackingTelemetryModuleFactory(this.CreateDefaultConfig(context));

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("|guid1.1").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);

            var requestTelemetry = context.GetRequestTelemetry();
            var telemetryClient = new TelemetryClient(config);
            var exceptionTelemetry = new ExceptionTelemetry();
            telemetryClient.Initialize(exceptionTelemetry);

            module.OnEndRequest(context);

            Assert.Equal("guid1", exceptionTelemetry.Context.Operation.Id);
            Assert.Equal(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);
            Assert.Equal("v", exceptionTelemetry.Context.Properties["k"]);
        }

        [TestMethod]
        public void OnPreHandlerTelemetryCreatedWithinRequestScopeIsRequestChild()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var config = this.CreateDefaultConfig(context);
            var module = this.RequestTrackingTelemetryModuleFactory(config);
            var telemetryClient = new TelemetryClient(config);

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("|guid1.1").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);

            // simulate losing call context by cleaning up activity
            activity.Stop();
            Assert.Null(Activity.Current);

            // CallContext was lost after OnBegin, so Asp.NET Http Module will restore it in OnPreRequestHandlerExecute
            new Activity("restored").SetParentId(activity.Id).AddBaggage("k", "v").Start();

            // if OnPreRequestHandlerExecute set a CallContext, child telemetry will be properly filled
            var trace = new TraceTelemetry();
            telemetryClient.TrackTrace(trace);
            var requestTelemetry = context.GetRequestTelemetry();

            Assert.Equal(requestTelemetry.Context.Operation.Id, trace.Context.Operation.Id);
            // we created Activity for request and assigned Id for the request like guid1.1.12345_
            // then we lost it and restored (started a new child activity), so the Id is guid1.1.12345_abc_
            // so the request is grand parent to the trace
            Assert.Equal(Activity.Current.ParentId, requestTelemetry.Id);
            Assert.True(trace.Context.Operation.ParentId.StartsWith(requestTelemetry.Id));
            Assert.Equal(Activity.Current.Id, trace.Context.Operation.ParentId);
            Assert.Equal("v", trace.Context.Properties["k"]);
        }

        [TestMethod]
        public void TelemetryCreatedWithinRequestScopeIsRequestChildWhenActivityIsLost()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var config = this.CreateDefaultConfig(context);
            var module = this.RequestTrackingTelemetryModuleFactory(config);
            var telemetryClient = new TelemetryClient(config);

            // ASP.NET HttpModule is responsible to parse Activity from incoming request and start it
            // let's simulate it
            var activity = new Activity("Microsoft.AspNet.HttpReqIn").SetParentId("|guid1.1").AddBaggage("k", "v");
            diagnosticListener.StartActivity(activity, new { });

            module.OnBeginRequest(context);

            // simulate losing call context by cleaning up activity
            activity.Stop();
            Assert.Null(Activity.Current);

            var trace = new TraceTelemetry();
            telemetryClient.TrackTrace(trace);
            var requestTelemetry = context.GetRequestTelemetry();

            Assert.Equal(requestTelemetry.Context.Operation.Id, trace.Context.Operation.Id);
            // we created Activity for request and assigned Id for the request like guid1.1.12345
            // then we created Activity for request children and assigned it Id like guid1.1.12345_1
            // then we lost it and restored (started a new child activity), so the Id is guid1.1.123_1.abc
            // so the request is grand parent to the trace
            Assert.True(trace.Context.Operation.ParentId.StartsWith(requestTelemetry.Id));
            Assert.Equal("v", trace.Context.Properties["k"]);
        }
    }
}
