﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Net;
    using Microsoft.ApplicationInsights.DataContracts;
    using Common;

    internal static class ClientServerDependencyTracker
    {
        /// <summary>
        /// Gets or sets a value indicating whether pretending the profiler is attached or not.
        /// </summary>
        internal static bool PretendProfilerIsAttached { get; set; } 

        /// <summary>
        /// The function that needs to be called before sending a request to the server. Creates and initializes dependency telemetry item.
        /// </summary>
        /// <param name="telemetryClient">Telemetry client object to initialize the context of the telemetry item.</param>
        internal static DependencyTelemetry BeginTracking(TelemetryClient telemetryClient)
        {
            var telemetry = new DependencyTelemetry();
            telemetry.Start();
            telemetryClient.Initialize(telemetry);

            //TODO: move Id generation to Base SDK
            telemetry.Id = AppInsightsActivity.GenerateDependencyId(telemetry.Context.Operation.ParentId, telemetry.Id);

            Debug.WriteLine($"dependency id {telemetry.Id} parent { telemetry.Context.Operation.ParentId}, root {telemetry.Context.Operation.Id}");
            PretendProfilerIsAttached = false;
            return telemetry;
        }

        /// <summary>
        /// Function that needs to be invoked after the request call to the sever. Computes the duration of the request and tracks the dependency telemetry
        /// item.
        /// </summary>
        /// <param name="telemetryClient">Telemetry client object to track the telemetry item.</param>
        /// <param name="telemetry">Telemetry item to compute the duration and track.</param>
        internal static void EndTracking(TelemetryClient telemetryClient, DependencyTelemetry telemetry)
        {
            telemetry.Stop();
            telemetryClient.Track(telemetry);
        }

        /// <summary>
        /// Gets the tuple from either conditional weak table or cache (based on the framework for the input web request).
        /// </summary>
        /// <param name="webRequest">Target web request.</param>
        /// <returns>Tuple of dependency telemetry and a boolean that tells if the tuple is custom created or not.</returns>
        internal static Tuple<DependencyTelemetry, bool> GetTupleForWebDependencies(WebRequest webRequest)
        {
            if (webRequest == null)
            {
                throw new ArgumentNullException("webRequest");
            }

            Tuple<DependencyTelemetry, bool> telemetryTuple = null;

            if (DependencyTableStore.Instance.IsProfilerActivated || PretendProfilerIsAttached)
            {
                telemetryTuple = DependencyTableStore.Instance.WebRequestConditionalHolder.Get(webRequest);
            }
            else
            {
#if !NET40
                telemetryTuple = DependencyTableStore.Instance.WebRequestCacheHolder.Get(GetIdForRequestObject(webRequest));
#endif
            }

            return telemetryTuple;
        }

        /// <summary>
        /// Adds the tuple to either conditional weak table or cache (based on the framework for the input web request).
        /// </summary>
        /// <param name="webRequest">Target web request.</param>
        /// <param name="telemetry">Dependency telemetry item to add to the table for the corresponding web request.</param>
        /// <param name="isCustomCreated">Boolean value that tells if the current telemetry item is being added by the customer or not.</param>
        internal static void AddTupleForWebDependencies(WebRequest webRequest, DependencyTelemetry telemetry, bool isCustomCreated)
        {
            if (webRequest == null)
            {
                throw new ArgumentNullException("webRequest");
            }

            if (telemetry == null)
            {
                throw new ArgumentNullException("telemetry");
            }

            var telemetryTuple = new Tuple<DependencyTelemetry, bool>(telemetry, isCustomCreated);
            if (DependencyTableStore.Instance.IsProfilerActivated || PretendProfilerIsAttached)
            {
                DependencyTableStore.Instance.WebRequestConditionalHolder.Store(webRequest, telemetryTuple);
            }
            else
            {
#if !NET40
                DependencyTableStore.Instance.WebRequestCacheHolder.Store(GetIdForRequestObject(webRequest), telemetryTuple);
#endif
            }
        }

        /// <summary>
        /// Gets the tuple from either conditional weak table or cache (based on the framework for the input SQL request).
        /// </summary>
        /// <param name="sqlRequest">Target SQL request.</param>
        /// <returns>Tuple of dependency telemetry and a boolean that tells if the tuple is custom created or not.</returns>
        internal static Tuple<DependencyTelemetry, bool> GetTupleForSqlDependencies(SqlCommand sqlRequest)
        {
            if (sqlRequest == null)
            {
                throw new ArgumentNullException("webRequest");
            }

            Tuple<DependencyTelemetry, bool> telemetryTuple = null;

            if (DependencyTableStore.Instance.IsProfilerActivated || PretendProfilerIsAttached)
            {
                telemetryTuple = DependencyTableStore.Instance.SqlRequestConditionalHolder.Get(sqlRequest);
            }
            else
            {
#if !NET40
                telemetryTuple = DependencyTableStore.Instance.SqlRequestCacheHolder.Get(GetIdForRequestObject(sqlRequest));
#endif
            }

            return telemetryTuple;
        }

        /// <summary>
        /// Adds the tuple to either conditional weak table or cache (based on the framework for the input SQL request).
        /// </summary>
        /// <param name="sqlRequest">Target SQL request.</param>
        /// <param name="telemetry">Dependency telemetry item to add to the table for the corresponding SQL request.</param>
        /// <param name="isCustomCreated">Boolean value that tells if the current telemetry item is being added by the customer or not.</param>
        internal static void AddTupleForSqlDependencies(SqlCommand sqlRequest, DependencyTelemetry telemetry, bool isCustomCreated)
        {
            if (sqlRequest == null)
            {
                throw new ArgumentNullException("webRequest");
            }

            if (telemetry == null)
            {
                throw new ArgumentNullException("telemetry");
            }

            var telemetryTuple = new Tuple<DependencyTelemetry, bool>(telemetry, isCustomCreated);
            if (DependencyTableStore.Instance.IsProfilerActivated || PretendProfilerIsAttached)
            {
                DependencyTableStore.Instance.SqlRequestConditionalHolder.Store(sqlRequest, telemetryTuple);
            }
            else
            {
#if !NET40
                DependencyTableStore.Instance.SqlRequestCacheHolder.Store(GetIdForRequestObject(sqlRequest), telemetryTuple);
#endif
            }
        }

        private static long GetIdForRequestObject(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            return (long)obj.GetHashCode() + 9223372032559808512L;
        }
    }
}