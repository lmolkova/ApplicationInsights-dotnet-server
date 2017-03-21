namespace Microsoft.ApplicationInsights.Web.Helpers
{
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal static class HttpContextTestExtensions
    {
        internal static RequestTelemetry WithAuthCookie(this HttpContext context, string cookieString)
        {
            context.AddRequestCookie(
                new HttpCookie(
                    RequestTrackingConstants.WebAuthenticatedUserCookieName,
                                                    HttpUtility.UrlEncode(cookieString)));
            return context.GetRequestTelemetry();
        }

        internal static RequestTelemetry SetRequestTelemetry(this HttpContext context, RequestTelemetry requestTelemetry = null)
        {
            if (requestTelemetry == null)
            {
                requestTelemetry = new RequestTelemetry();
            }

            context.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, requestTelemetry);
            return requestTelemetry;
        }

        /*internal static IOperationHolder<RequestTelemetry> SetOperationHolder(this HttpContext context, RequestTelemetry requestTelemetry = null)
        {
            var operationHolder = new TestOperationHolder(requestTelemetry ?? new RequestTelemetry());
            
            context.Items.Add(RequestTrackingConstants.OperationItemName, operationHolder);
            return operationHolder;
        }*/
    }
}
