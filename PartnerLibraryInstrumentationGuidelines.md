# Guidelines for instrumenting partner libraries with Diagnostic Source 

This document provides guidelines for adding Diagnostic Source instrumentation to external libraries in a way that will allow to automatically collect high quality telemetry in any tracing system of user choice that follows this guidance

## Diagnostic Source and Activities

[Diagnostic Source][DiagnosticSourceGuide] is a simple module that allows code to be instrumented for production-time logging of rich data payloads for consumption within the process that was instrumented. At runtime consumers can dynamically discover data sources and subscribe to the ones of interest.

[Activity][ActivityGuide] is a class that allows storing and accessing diagnostics context and consuming it with logging system.

Both Diagnostic Source and Activity have been used to instrument [System.Net.Http][SystemNetHttp] and [Microsoft.AspNetCore.Hosting][MicrosoftAspNetCoreHosting], although that instrumentation is not fully complaiant with this guidance.

[This document][DiagnosticSourceActivityHowto] can help to a better understanding on how to efficiently use Diagnostic Source.

### Instrumentation code

The following code sample shows how to instrument the operation logic enclosed in ```ProcessOperationImplAsync()``` method, in the most efficient way which will ensure no performance overhead is added if there are no listeners for that particular Activity.

```C#
private const string DiagnosticSourceName = "Microsoft.ApplicationInsights.Samples";
private const string ActivityName = DiagnosticSourceName + ".ProcessOperation";
private const string ActivityStartName = ActivityName + ".Start";
private const string ActivityExceptionName = ActivityName + ".Exception";

private static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener(DiagnosticSourceName); 

private async Task<OperationOutput> ProcessOperationImplAsync(OperationInput input)
{
    // original code to instrument
}

public Task<OperationOutput> ProcessOperationAsync(OperationInput input)
{
    // this Diagnostic Source has any listeners?
    if (DiagnosticListener.IsEnabled())
    {
        // is any listener interested in this kind of activity and this particular input?
        bool isActivityEnabled = DiagnosticListener.IsEnabled(ActivityName, input);

        // some listeners may only want to receive exceptions
        bool isExceptionEnabled = DiagnosticListener.IsEnabled(ActivityExceptionName);

        if (isActivityEnabled || isExceptionEnabled)
        {
            return this.ProcessOperationInstrumentedAsync(input, isActivityEnabled, isExceptionEnabled);
        }
    }

    // no one listens - run without instrumentation
    return this.ProcessOperationImplAsync(input);
}

private async Task<OperationOutput> ProcessOperationInstrumentedAsync(OperationInput input)
{
    Activity activity = null;

    // create and start activity if enabled
    
    /// isActivityEnabled DOES NOT exists in this scope!
    if (isActivityEnabled)
    {
        activity = new Activity(ActivityName);

        activity.AddTag("component", "Microsoft.ApplicationInsights.Samples");          //Can we eliminate it even further and use DiagSource name as component name?
        
        // in general, it's important for tracing system to know kind of poeration being traced:
        // 'incoming' operations (i.e. incoming HTTP request, or task is received from the queue) must have "server" kind
        // 'outgoing' operations (i.e. call to external process over the wire to continue operation processing) must have "client" kind
        activity.AddTag("span.kind", "client");
        // TODO extract activity tags from input

        // most of the times activity start event is not interesting, 
        // in such case start activity without firing event
        if (DiagnosticListener.IsEnabled(ActivityStartName))
        {
            DiagnosticListener.StartActivity(activity, new {Input = input});
        }
        else
        {
            activity.Start();
        }
    }

    Task<OperationOutput> outputTask = null;
    OperationOutput output = null;

    try
    {
        outputTask = this.ProcessOperationImplAsync(input);;
        output = await outputTask;

        if (activity != null)
        {
            // TODO extract activity tags from output
        }
    }
    catch (Exception ex)
    {
        if (isExceptionEnabled)
        {
            // Exception object must be inlcuded into the payload with the 'Exception' property name. 
            // It is useful to include Input object (it's up to the lib to use any name for this payload):
            // if listener only wanted to trace exceptions, it can use parse Input to get all important properties that are important for tracing
            DiagnosticListener.Write(ActivityExceptionName, new { Input = input, Exception = ex });
        }
    }
    finally
    {
        if (activity != null)
        {
            // stop activity
            // 
            // activity.AddTag("error", (outputTask?.Status == TaskStatus.RanToCompletion).ToString()); // Can we eliminate it and use TaskStatus from the payload? It makes sense to standartize the payload name and type in this case
            
            // Stop event is the most important for the majority of listeners
            // It should provide all the necessary context about the operation to listener
            //  - Input and Output objects (i.e. request and response). The lib owner could chose any appropriate names. If inout/output consist of more than one object, all of them should be added into the payload
            //  - TaskStatus - Indicates success or failure of the operation. TaskStatus name must be used. It should only give high-level success/error info and should not attempt to check particular response (e.g. HTTP status code).
            DiagnosticListener.StopActivity(activity,
                new
                {
                    Output = outputTask?.Status == TaskStatus.RanToCompletion ? output : null,
                    Input = input,
                    TaskStatus = outputTask?.Status
                });
        }
    }

    return output;
}
```

### Performance considerations

In the sample code above different flavors of ```IsEnabled()``` method are called in this particular order: 

* ```DiagnosticListener.IsEnabled()``` - checks if there is any listener for this diagnostic source. This is a very efficient preliminary check for listeners.
* ```DiagnosticListener.IsEnabled(ActivityName, input1, ...)``` - checks if there is any listener for this activity and allows the listener to inspect the input parameters to make the decision. The input parameters passed in this method should be useful for the listeners to determine whether the activity would be interesting or not
* ```DiagnosticListener.IsEnabled(ActivityStartName)``` - checks if there is any listener for activity `Start` event. Typically only the activity `Stop` event is interesting   
* ```DiagnosticListener.IsEnabled(ActivityExceptionName)``` - checks if there is any listener for activity `Exception`. The code sample above supports a scenario where there is an active listener for the exception but none for the activity itself

It is also worth to note that in case when there is no listener for given activity the asynchronous operation task is not being awaited and is directly returned to the caller.  

All of these checks are made to ensure that no performance overhead is added in case when there are no active listeners for the given activity.  

## Activity tags

When populating activity tags it is recommended to follow the [OpenTracing naming convention][OpenTracingNamingConvention].

A couple of tags defined by that convention have significant meaning and should be present in all activities:

| Tag | Notes and examples |
|:--------------|:-------------------|
| `span.kind` | Indicates the role in the processing flow that activity is representing - e.g. it can be `client` vs `server` which corresondingly denote performing outgoing operation or processing incoming request.  |
| `error` | Indicates whether activity completed successfully or not. |
| `component`  | Indicates the source component from which the activities originate. This can be the library or service name. The difference between this tag and Diagnostic Source name is that a single library may use more than one Diagnostic Sources (in fact this is recommended in certain scenarios), however it should consistently use the same `component` tag  |


[DiagnosticSourceGuide]: https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md
[ActivityGuide]: https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md
[DiagnosticSourceActivityHowto]: https://github.com/lmolkova/correlation/wiki/How-to-instrument-library-with-Activity-and-DiagnosticSource
[OpenTracingNamingConvention]: https://github.com/opentracing/specification/blob/master/semantic_conventions.md#span-tags-table
[AIDataModelRdd]: https://docs.microsoft.com/en-us/azure/application-insights/application-insights-data-model-dependency-telemetry
[SystemNetHttp]: https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
[MicrosoftAspNetCoreHosting]: https://github.com/aspnet/Hosting/blob/dev/src/Microsoft.AspNetCore.Hosting/Internal/HostingApplicationDiagnostics.cs
