﻿namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Linq;
    using System.Reflection;

    internal class SdkVersionUtils
    {
        internal static string GetSdkVersion(string versionPrefix)
        {
            string versionStr = typeof(SdkVersionUtils).Assembly.GetCustomAttributes(false)
                    .OfType<AssemblyFileVersionAttribute>()
                    .First()
                    .Version;

            Version version = new Version(versionStr);
            return (versionPrefix ?? string.Empty) + version.ToString(3) + "-" + version.Revision;
        }
    }
}
