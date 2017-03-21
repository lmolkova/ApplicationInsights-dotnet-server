namespace Microsoft.ApplicationInsights.Web.Helpers
{
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    public class TestOperationHolder : IOperationHolder<RequestTelemetry>
    {
        public TestOperationHolder(RequestTelemetry telemetry)
        {
            this.Telemetry = telemetry;
        }

        public TestOperationHolder()
        {
            this.Telemetry = new RequestTelemetry();
        }

        public RequestTelemetry Telemetry { get; }

        public void Dispose()
        {
        }
    }
}
