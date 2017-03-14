using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace Microsoft.ApplicationInsights.Common
{
    //this is a temporary solution that mimics System.DiagnosticSource,Activiyt and Correlation HTTP protocol:
    //https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md
    //It does not implement 
    // - Request-Id length limitation and overflow
    internal class AppInsightsActivity
    {
        private static readonly string RequestIdSlot = $"{AppDomain.CurrentDomain}_request_id";
        private static readonly string ParentRequestIdSlot = $"{AppDomain.CurrentDomain}_parent_request_id";
        private static readonly string CorrelationContextSlot = $"{AppDomain.CurrentDomain}_correlation_context";

        public static string RequestId
        {
            get { return CallContext.LogicalGetData(RequestIdSlot)?.ToString();}
            set {CallContext.LogicalSetData(RequestIdSlot, value); }
        }

        public static string ParentRequestId
        {
            get { return CallContext.LogicalGetData(ParentRequestIdSlot)?.ToString(); }
            set { CallContext.LogicalSetData(ParentRequestIdSlot, value); }
        }

        public static IEnumerable<KeyValuePair<string,string>> Context
        {
            get { return CallContext.LogicalGetData(CorrelationContextSlot) as IEnumerable<KeyValuePair<string,string>>; }
            set { CallContext.LogicalSetData(CorrelationContextSlot, value); }
        }

        internal static string GetRootId(string requestId)
        {
            Debug.Assert(!string.IsNullOrEmpty(requestId));
            if (requestId[0] == '|')
            {
                var rootEnd = requestId.IndexOf('.');
                var rootId = requestId.Substring(1, rootEnd - 1);
                return rootId;
            }
            return requestId;
        }


        internal static string GenerateNewId()
        {
            if (_machinePrefix == null)
                Interlocked.CompareExchange(ref _machinePrefix, Environment.MachineName + "-" + ((int)Stopwatch.GetTimestamp()).ToString("x"), null);
            return '|' + _machinePrefix + '-' + Interlocked.Increment(ref _currentOperationNum).ToString("x") + '.';
        }

        internal static string GenerateRequestId(string parentRequestId)
        {
            if (parentRequestId != null)
            {
                var childRequestId = parentRequestId[0] != '|' ? '|' + parentRequestId : parentRequestId;
                if (childRequestId[childRequestId.Length - 1] != '.')
                    childRequestId += '.';

                return GenerateChildTelemetryId(childRequestId, '_');
            }
            return GenerateNewId();
        }

        internal static string GenerateDependencyId()
        {
            string parentRequestId = RequestId;

            if (parentRequestId != null)
                return GenerateChildTelemetryId(parentRequestId, '.');

            return GenerateNewId();
        }


        private static string _machinePrefix;
        private static long _currentOperationNum = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 12);

        private static string GenerateChildTelemetryId(string telemetryId, char delimiter)
        {
            Debug.Assert(!string.IsNullOrEmpty(telemetryId));
            uint random = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 12);
            return telemetryId + random.ToString("x") + delimiter;
        }
    }
}
