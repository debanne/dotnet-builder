using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using System.Threading;

namespace Builder
{
    class Builder
    {
        private BuildManager _buildManager = BuildManager.DefaultBuildManager;
        private Dictionary<string, string> _buildProperties = new Dictionary<string, string>();

        BuildParameters _buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection)
        {
            Loggers = new[] { new SimpleConsoleLogger { Verbosity = LoggerVerbosity.Normal } },
            MaxNodeCount = Environment.ProcessorCount
        };
        string _toolsVersion = "15.0";

        private BuildRequestData CreateRequest(string projectFullPath, string target)
        {
            ProjectInstance projectInstance = new ProjectInstance(projectFullPath, _buildProperties, _toolsVersion);
            return new BuildRequestData(projectInstance, new[] { target });
        }

        public void Build(string[] projectsFullPath, string target, bool parallelBuild)
        {
            if (!parallelBuild)
            {
                foreach (string projectFullPath in projectsFullPath)
                {
                    Console.WriteLine("Building {0}...", projectFullPath);
                    BuildResult buildResult = _buildManager.Build(_buildParameters, CreateRequest(projectFullPath, target));
                    Console.WriteLine("==> [{0}] {1}", buildResult.OverallResult, projectFullPath);
                }
            }
            else
            {
                _buildManager.BeginBuild(_buildParameters);
                using (CountdownEvent countdownEvent = new CountdownEvent(projectsFullPath.Length))
                {
                    foreach (string projectFullPath in projectsFullPath)
                    {
                        Console.WriteLine("Building {0} in parallel...", projectFullPath);
                        BuildSubmission submission = _buildManager.PendBuildRequest(CreateRequest(projectFullPath, target));
                        submission.ExecuteAsync(o => {
                            Console.WriteLine("=========================> [{0}] {1}", o.BuildResult.OverallResult, projectFullPath);
                            countdownEvent.Signal();
                        }, null);
                    }
                    countdownEvent.Wait();
                }
                _buildManager.EndBuild();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Builder builder = new Builder();

            builder.Build(new[] { "resources/project2/FrameworkApp.csproj" }, "Build", false);

            builder.Build(new[] { "resources/project1/CoreApp.csproj" }, "Restore", false);
            builder.Build(new[] { "resources/project1/CoreApp.csproj" }, "Build", false);

            Console.WriteLine("========================================");

            builder.Build(new[] {
                "resources/project1/CoreApp.csproj",
                "resources/project2/FrameworkApp.csproj",
            }, "Build", true);
        }
    }
}
