﻿namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation.Operation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    /// <summary>
    /// Base class with all processing logic to generate dependencies from the callbacks received from Profiler instrumentation for SQL.    
    /// </summary>
    internal abstract class ProfilerSqlProcessingBase
    {
        internal ObjectInstanceBasedOperationHolder TelemetryTable;
        private readonly TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilerSqlProcessingBase"/> class.
        /// </summary>
        internal ProfilerSqlProcessingBase(TelemetryConfiguration configuration, string agentVersion, ObjectInstanceBasedOperationHolder telemetryTupleHolder)
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

            // Since dependencySource is no longer set, sdk version is prepended with information which can identify whether dependency was collected by profiler/framework

            // For directly using TrackDependency(), version will be simply what is set by core
            string prefix = "rdd" + RddSource.Profiler + ":";
            this.telemetryClient.Context.GetInternalContext().SdkVersion = Web.Implementation.SdkVersionUtils.GetSdkVersion(prefix);
            if (!string.IsNullOrEmpty(agentVersion))
            {
                this.telemetryClient.Context.GetInternalContext().AgentVersion = agentVersion;
            }
        }

        #region Sql callbacks

        /// <summary>
        /// On begin callback for methods with 1 parameter.
        /// </summary>
        public object OnBeginForOneParameter(object thisObj)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On begin callback for methods with 2 parameter.
        /// </summary>
        public object OnBeginForTwoParameters(object thisObj, object parameter1)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On begin callback for methods with 3 parameters.
        /// </summary>
        public object OnBeginForThreeParameters(object thisObj, object parameter1, object parameter2)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On begin callback for methods with 4 parameter.
        /// </summary>
        public object OnBeginForFourParameters(object thisObj, object parameter1, object parameter2, object parameter3)
        {
            return this.OnBegin(thisObj);
        }

        /// <summary>
        /// On end callback for methods with 1 parameter.
        /// </summary>
        public object OnEndForOneParameter(object context, object returnValue, object thisObj)
        {
            this.OnEnd(null, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end async callback for methods with 1 parameter.
        /// </summary>
        public object OnEndAsyncForOneParameter(object context, object returnValue, object thisObj)
        {
            this.OnEndAsync(returnValue, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end async callback for methods with 1 parameter. Sends data only if returned task (returnValue) is faulted.
        /// </summary>
        public object OnEndExceptionAsyncForOneParameter(object context, object returnValue, object thisObj)
        {
            this.OnEndExceptionAsync(returnValue, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end callback for methods with 2 parameter.
        /// </summary>
        public object OnEndForTwoParameters(object context, object returnValue, object thisObj, object parameter1)
        {
            this.OnEnd(null, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end async callback for methods with 2 parameter.
        /// </summary>
        public object OnEndAsyncForTwoParameters(object context, object returnValue, object thisObj, object parameter1)
        {
            this.OnEndAsync(returnValue, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end async callback for methods with 2 parameter. Sends data only if returned task (returnValue) is faulted.
        /// </summary>
        public object OnEndExceptionAsyncForTwoParameters(object context, object returnValue, object thisObj, object parameter1)
        {
            this.OnEndExceptionAsync(returnValue, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On end callback for methods with 3 parameter.
        /// </summary>
        public object OnEndForThreeParameters(object context, object returnValue, object thisObj, object parameter1, object parameter2)
        {
            this.OnEnd(null, thisObj);
            return returnValue;
        }

        /// <summary>
        /// On exception callback for methods with 1 parameter.
        /// </summary>
        public void OnExceptionForOneParameter(object context, object exception, object thisObj)
        {
            this.OnEnd(exception, thisObj);
        }

        /// <summary>
        /// On exception callback for methods with 2 parameter.
        /// </summary>
        public void OnExceptionForTwoParameters(object context, object exception, object thisObj, object parameter1)
        {
            this.OnEnd(exception, thisObj);
        }

        /// <summary>
        /// On exception callback for methods with 3 parameter.
        /// </summary>
        public void OnExceptionForThreeParameters(object context, object exception, object thisObj, object parameter1, object parameter2)
        {
            this.OnEnd(exception, thisObj);
        }

        #endregion //Sql callbacks

        /// <summary>
        /// Gets SQL resource name.
        /// </summary>
        /// <param name="thisObj">The SQL object.</param>
        /// <returns>The resource name if possible otherwise empty string.</returns>
        internal abstract string GetDependencyName(object thisObj);

        /// <summary>
        /// Gets SQL resource target name.
        /// </summary>
        /// <param name="thisObj">The SQL object.</param>
        /// <returns>The resource target name if possible otherwise empty string.</returns>
        internal abstract string GetDependencyTarget(object thisObj);

        /// <summary>
        /// Gets SQL resource command text.
        /// </summary>
        /// <param name="thisObj">The SQL object.</param>
        /// <returns>Returns the command text or empty.</returns>
        internal abstract string GetCommandName(object thisObj);
     
        /// <summary>
        ///  Common helper for all Begin Callbacks.
        /// </summary>
        /// <param name="thisObj">This object.</param>
        /// <returns>The context for end callback.</returns>
        private object OnBegin(object thisObj)
        {
            try
            {
                if (thisObj == null)
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnBeginSql", "thisObj == null");
                    return null;
                }

                string resourceName = this.GetDependencyName(thisObj);
                DependencyCollectorEventSource.Log.BeginCallbackCalled(thisObj.GetHashCode(), resourceName);

                if (string.IsNullOrEmpty(resourceName))
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(thisObj.GetHashCode(), "OnBeginSql", "resourceName is empty");
                    return null;
                }

                var telemetryTuple = this.TelemetryTable.Get(thisObj);
                if (telemetryTuple != null)
                {
                    // We are already tracking this item
                    if (telemetryTuple.Item1 != null)
                    {
                        DependencyCollectorEventSource.Log.TrackingAnExistingTelemetryItemVerbose();
                        return null;
                    }
                }

                string commandText = this.GetCommandName(thisObj);

                // Try to begin if sampling this operation
                bool isCustomCreated = false;
                var telemetry = ClientServerDependencyTracker.BeginTracking(this.telemetryClient);

                telemetry.Name = resourceName;
                telemetry.Type = RemoteDependencyConstants.SQL;
                telemetry.Target = this.GetDependencyTarget(thisObj);
                telemetry.Data = commandText;

                // We use weaktables to store the thisObj for correlating begin with end call.
                this.TelemetryTable.Store(thisObj, new Tuple<DependencyTelemetry, bool>(telemetry, isCustomCreated));
                return null;
            }
            catch (Exception exception)
            {
                DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnBeginSql", exception);
            }

            return null;
        }

        /// <summary>
        ///  Common helper for all EndAsync Callbacks.
        /// </summary>
        /// <param name="taskObj">Returned task by the async method.</param>
        /// <param name="thisObj">This object.</param>
        private void OnEndAsync(object taskObj, object thisObj)
        {
            try
            {
                if (thisObj == null)
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnEndAsyncSql", "thisObj == null");
                    return;
                }

                Task task = taskObj as Task;
                if (task == null)
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnEndAsyncSql", "task == null");
                    return;
                }

                DependencyCollectorEventSource.Log.EndAsyncCallbackCalled(thisObj.GetHashCode().ToString(CultureInfo.InvariantCulture));

                task.ContinueWith(t =>
                {
                    try
                    {
                        Exception exceptionObj = null;
                        if (t.IsFaulted)
                        {
                            exceptionObj = t.Exception.InnerException != null ? t.Exception.InnerException : t.Exception;
                        }

                        this.OnEndInternal(exceptionObj, thisObj);
                    }
                    catch (Exception ex)
                    {
                        DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnEndAsyncSql", ex);
                    }
                });
            }            
            catch (Exception ex)
            {
                DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnEndAsyncSql", ex);
            }
        }

        /// <summary>
        ///  Common helper for all EndAsync Callbacks that should send data only in the case of exception happened.
        /// </summary>
        /// <param name="taskObj">Returned task by the async method.</param>
        /// <param name="thisObj">This object.</param>
        private void OnEndExceptionAsync(object taskObj, object thisObj)
        {
            try
            {
                if (thisObj == null)
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnEndExceptionAsyncSql", "thisObj == null");
                    return;
                }

                Task task = taskObj as Task;
                if (task == null)
                {
                    DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnEndExceptionAsyncSql", "task == null");
                    return;
                }

                DependencyCollectorEventSource.Log.EndAsyncExceptionCallbackCalled(thisObj.GetHashCode().ToString(CultureInfo.InvariantCulture));

                task.ContinueWith(t =>
                {
                    try
                    {
                        Exception exceptionObj = null;
                        if (t.IsFaulted)
                        {
                            // track item only in case of failure
                            exceptionObj = t.Exception.InnerException != null ? t.Exception.InnerException : t.Exception;
                            this.OnEndInternal(exceptionObj, thisObj);
                        }
                        else
                        {
                            this.TelemetryTable.Remove(thisObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnEndExceptionAsyncSql", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnEndExceptionAsyncSql", ex);
            }
        }

        /// <summary>
        ///  Common helper for all End Callbacks.
        /// </summary>
        /// <param name="exceptionObj">The exception object if any.</param>
        /// <param name="thisObj">This object.</param>
        private void OnEnd(object exceptionObj, object thisObj)
        {
            try
            {
                this.OnEndInternal(exceptionObj, thisObj);            
            }
            catch (Exception ex)
            {
                DependencyCollectorEventSource.Log.CallbackError(thisObj == null ? 0 : thisObj.GetHashCode(), "OnEndSql", ex);
            }
        }

        /// <summary>
        ///  Common helper for all End Callbacks.
        /// </summary>
        /// <param name="exceptionObj">The exception object if any.</param>
        /// <param name="thisObj">This object.</param>
        private void OnEndInternal(object exceptionObj, object thisObj)
        {
            if (thisObj == null)
            {
                DependencyCollectorEventSource.Log.NotExpectedCallback(0, "OnEndSql", "thisObj == null");
                return;
            }

            DependencyCollectorEventSource.Log.EndCallbackCalled(thisObj.GetHashCode().ToString(CultureInfo.InvariantCulture));

            DependencyTelemetry telemetry = null;
            Tuple<DependencyTelemetry, bool> telemetryTuple = null;
            bool isCustomGenerated = false;

            telemetryTuple = this.TelemetryTable.Get(thisObj);
            if (telemetryTuple != null)
            {
                telemetry = telemetryTuple.Item1;
                isCustomGenerated = telemetryTuple.Item2;
            }

            if (telemetry == null)
            {
                DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(thisObj.GetHashCode().ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (!isCustomGenerated)
            {
                this.TelemetryTable.Remove(thisObj);

                var exception = exceptionObj as Exception;
                if (exception != null)
                {
                    telemetry.Success = false;
                    telemetry.Properties.Add("ErrorMessage", exception.Message);

                    var sqlEx = exception as SqlException;
                    telemetry.ResultCode = sqlEx != null ? sqlEx.Number.ToString(CultureInfo.InvariantCulture) : "0";
                }
                else
                {
                    telemetry.Success = true;
                    telemetry.ResultCode = "0";
                }

                ClientServerDependencyTracker.EndTracking(this.telemetryClient, telemetry);
            }
        }
    }
}