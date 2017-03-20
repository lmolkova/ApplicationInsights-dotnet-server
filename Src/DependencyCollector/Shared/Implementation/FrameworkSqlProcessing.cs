﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Globalization;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation.Operation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal sealed class FrameworkSqlProcessing
    {
        internal CacheBasedOperationHolder TelemetryTable;
        private TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameworkSqlProcessing"/> class.
        /// </summary>
        internal FrameworkSqlProcessing(TelemetryConfiguration configuration, CacheBasedOperationHolder telemetryTupleHolder)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            if (telemetryTupleHolder == null)
            {
                throw new ArgumentNullException("telemetryHolder");
            }

            this.TelemetryTable = telemetryTupleHolder;
            this.telemetryClient = new TelemetryClient(configuration);

            // Since dependencySource is no longer set, sdk version is prepended with information which can identify whether RDD was collected by profiler/framework

            // For directly using TrackDependency(), version will be simply what is set by core
            string prefix = "rdd" + RddSource.Framework + ":";
            this.telemetryClient.Context.GetInternalContext().SdkVersion = Web.Implementation.SdkVersionUtils.GetSdkVersion(prefix);
        }

        #region Sql callbacks

        /// <summary>
        /// On begin callback from Framework event source.
        /// </summary>
        /// <param name="id">Identifier of SQL connection object.</param>
        /// <param name="dataSource">Data source name.</param>
        /// <param name="database">Database name.</param>
        /// <param name="commandText">Command text.</param>
        public void OnBeginExecuteCallback(long id, string dataSource, string database, string commandText)
        {
            try
            {
                var resourceName = this.GetResourceName(dataSource, database, commandText);

                DependencyCollectorEventSource.Log.BeginCallbackCalled(id, resourceName);

                if (string.IsNullOrEmpty(resourceName))
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(id, "OnBeginSql", "resourceName is empty");
                    return;
                }

                var telemetryTuple = this.TelemetryTable.Get(id);
                if (telemetryTuple == null)
                {
                    bool isCustomCreated = false;
                    var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);
                    telemetry.Name = resourceName;
                    telemetry.Target = string.Join(" | ", dataSource, database);
                    telemetry.Type = RemoteDependencyConstants.SQL;
                    telemetry.Data = commandText;
                    this.TelemetryTable.Store(id, new Tuple<DependencyTelemetry, bool>(telemetry, isCustomCreated));
                }
            }
            catch (Exception exception)
            {
                DependencyCollectorEventSource.Log.CallbackError(id, "OnBeginSql", exception);
            }
        }

        /// <summary>
        /// On end callback from Framework event source.
        /// </summary>        
        /// <param name="id">Identifier of SQL connection object.</param>
        /// <param name="success">Indicate whether operation completed successfully.</param>
        /// <param name="synchronous">Indicates whether operation was called synchronously or asynchronously.</param>
        /// <param name="sqlExceptionNumber">SQL exception number.</param>
        public void OnEndExecuteCallback(long id, bool success, bool synchronous, int sqlExceptionNumber)
        {
            DependencyCollectorEventSource.Log.EndCallbackCalled(id.ToString(CultureInfo.InvariantCulture));

            var telemetryTuple = this.TelemetryTable.Get(id);

            if (telemetryTuple == null)
            {
                DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(id.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (!telemetryTuple.Item2)
            {
                this.TelemetryTable.Remove(id);
                var telemetry = telemetryTuple.Item1 as DependencyTelemetry;
                telemetry.Success = success;
                telemetry.ResultCode = sqlExceptionNumber.ToString(CultureInfo.InvariantCulture);

                ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            }
        }

        #endregion
        
        /// <summary>
        /// Gets SQL command resource name.
        /// </summary>
        /// <param name="dataSource">DataSource name.</param>
        /// <param name="database">Database name.</param>
        /// <param name="commandText">CommandText name.</param>        
        /// <returns>The resource name if possible otherwise empty string.</returns>
        private string GetResourceName(string dataSource, string database, string commandText)
        {
            string resource = string.IsNullOrEmpty(commandText)
                ? string.Join(" | ", dataSource, database)
                : commandText;
            return resource;
        }
    }
}
