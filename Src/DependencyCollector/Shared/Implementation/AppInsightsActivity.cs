namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;

    //this is a temporary solution that mimics System.Diagnostics.Activity and Correlation HTTP protocol:
    //https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md
    internal class AppInsightsActivity
    {
        internal static bool IsHierarchicalRequestId(string requestId)
        {
            return requestId[0] == '|';
        }

        internal static string GetRootId(string requestId)
        {
            Debug.Assert(!string.IsNullOrEmpty(requestId));

            int rootEnd = requestId.IndexOf('.');
            if (rootEnd < 0)
            {
                rootEnd = requestId.Length;
            }
            int rootStart = requestId[0] == '|' ? 1 : 0;
            return requestId.Substring(rootStart, rootEnd - rootStart);
        }

        internal static string GenerateNewId()
        {
            return '|' + GenerateRootId() + '.';
        }

        internal static string GenerateRequestId(string parentRequestId)
        {
            if (!string.IsNullOrEmpty(parentRequestId))
            {
                parentRequestId = parentRequestId[0] != '|' ? '|' + parentRequestId : parentRequestId;
                if (parentRequestId[parentRequestId.Length - 1] != '.')
                {
                    parentRequestId += '.';
                }

                return AppendSuffix(parentRequestId, ((uint)WeakConcurrentRandom.Instance.Next()).ToString("x"), '_');
            }
            return GenerateNewId();
        }

        internal static string GenerateDependencyId(string parentRequestId)
        {
            if (!string.IsNullOrEmpty(parentRequestId))
            {
                return AppendSuffix(parentRequestId, ((uint)WeakConcurrentRandom.Instance.Next()).ToString("x"), '.');
            }

            return GenerateNewId();
        }

        private static string GenerateRootId()
        {
            if (_machinePrefix == null)
            {
                Interlocked.CompareExchange(ref _machinePrefix,
                    Environment.MachineName + "-" + ((int)Stopwatch.GetTimestamp()).ToString("x"), null);
            }

            return _machinePrefix + '-' + Interlocked.Increment(ref _currentOperationNum).ToString("x");
        }

        private static string AppendSuffix(string parentId, string suffix, char delimiter)
        {
            if (parentId.Length + suffix.Length < RequestIdMaxLength)
                return parentId + suffix + delimiter;

            //Id overflow:
            //find position in RequestId to trim
            int trimPosition = RequestIdMaxLength - 9; // overflow suffix + delimiter length is 9
            while (trimPosition > 1)
            {
                if (parentId[trimPosition - 1] == '.' || parentId[trimPosition - 1] == '_')
                    break;
                trimPosition--;
            }

            //ParentId is not valid Request-Id, let's generate proper one.
            if (trimPosition == 0)
                return GenerateRootId();

            //generate overflow suffix
            string overflowSuffix = ((int)WeakConcurrentRandom.Instance.Next()).ToString("x8");
            return parentId.Substring(0, trimPosition) + overflowSuffix + '#';
        }

        private static string _machinePrefix;
        private const int RequestIdMaxLength = 1024;
        private static long _currentOperationNum = (uint)WeakConcurrentRandom.Instance.Next();
    }
}
