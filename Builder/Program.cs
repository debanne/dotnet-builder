using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Builder
{
    class Program
    {
        private static void MSBuild(string projectFullPath, string target)
        {
            Console.WriteLine("Building {0}...", projectFullPath);

            String toolsVersion = "15.0";
            Dictionary<string, string> buildProperties = new Dictionary<string, string>();
            ProjectInstance projectInstance = new ProjectInstance(projectFullPath, buildProperties, toolsVersion);
            BuildRequestData request = new BuildRequestData(projectInstance, new[] { target });
            var buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection)
            {
                Loggers = new[] { new SimpleConsoleLogger {Verbosity = LoggerVerbosity.Normal} },
            };
            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult buildResult = buildManager.Build(buildParameters, request);

            Console.WriteLine("==> {0}", buildResult.OverallResult);
        }

        static void Main(string[] args)
        {
            MSBuild("resources/project2/FrameworkApp.csproj", "Build");

            Environment.SetEnvironmentVariable("MSBuildSDKsPath", @"C:\Program Files\dotnet\sdk\1.0.0-rc4-004771\Sdks");
            MSBuild("resources/project1/CoreApp.csproj", "Restore");
            MSBuild("resources/project1/CoreApp.csproj", "Build");
        }
    }
}
