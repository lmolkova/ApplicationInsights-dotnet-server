
namespace Microsoft.ApplicationInsights.Web.Helpers
{
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    public class TestOperationHolder : IOperationHolder<RequestTelemetry>
    {
        public TestOperationHolder(RequestTelemetry telemetry)
        {
            Telemetry = telemetry;
        }
        public TestOperationHolder()
        {
            Telemetry = new RequestTelemetry();
        }
        public void Dispose()
        {
        }

        public RequestTelemetry Telemetry { get; private set; }
    }
}
