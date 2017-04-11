using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using System.Threading;
using Microsoft.Build.Utilities;

namespace Builder
{
    class Builder
    {
        private BuildManager _buildManager = BuildManager.DefaultBuildManager;
        private Dictionary<string, string> _buildProperties = new Dictionary<string, string>();
        private Logger _logger = new SimpleConsoleLogger {Verbosity = LoggerVerbosity.Normal};
        private string _toolsVersion = "15.0";
        private string _resourcesAbsolutePath;

        public Builder(string resourcesAbsolutePath)
        {
            _resourcesAbsolutePath = resourcesAbsolutePath;
        }

        private BuildRequestData CreateRequest(string projectPath, string target)
        {
            // The current directory is modified by MSBuild when building a project, so the absolute path must be used.
            string projectFullPath = Path.Combine(_resourcesAbsolutePath, projectPath);
            ProjectInstance projectInstance = new ProjectInstance(projectFullPath, _buildProperties, _toolsVersion);
            return new BuildRequestData(projectInstance, new[] { target });
        }

        public void Build(string[] projectPaths, string target, bool parallelBuild, int maxNodeCount = 1)
        {
            Console.WriteLine("========================================");

            BuildParameters buildParameters = new BuildParameters(ProjectCollection.GlobalProjectCollection)
            {
                Loggers = new[] { _logger },
                MaxNodeCount = maxNodeCount
            };
            if (!parallelBuild)
            {
                foreach (string projectPath in projectPaths)
                {
                    Console.WriteLine("Building {0}...", projectPath);
                    BuildResult buildResult = _buildManager.Build(buildParameters, CreateRequest(projectPath, target));
                    Console.WriteLine("=====> [{0}] {1}", buildResult.OverallResult, projectPath);
                }
            }
            else
            {
                _buildManager.BeginBuild(buildParameters);
                using (CountdownEvent countdownEvent = new CountdownEvent(projectPaths.Length))
                {
                    foreach (string projectPath in projectPaths)
                    {
                        Console.WriteLine("Building {0} in parallel...", projectPath);
                        BuildSubmission submission = _buildManager.PendBuildRequest(CreateRequest(projectPath, target));
                        submission.ExecuteAsync(o => {
                            Console.WriteLine("=====> [{0}] {1}", o.BuildResult.OverallResult, projectPath);
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
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                var msBuildFolder = Environment.ExpandEnvironmentVariables(@"%VSINSTALLDIR%\MSBuild\15.0\Bin");
                var targetAssembly = Path.Combine(msBuildFolder, new AssemblyName(eventArgs.Name).Name + ".dll");
                return File.Exists(targetAssembly) ? Assembly.LoadFrom(targetAssembly) : null;
            };

            Builder builder = new Builder(Directory.GetCurrentDirectory());

            builder.Build(new[] {
                "resources/repo3/A/A.csproj",
                "resources/repo3/A.UTest/A.UTest.csproj",
                "resources/repo3/B/B.csproj",
            }, "Clean", true, 1);

            builder.Build(new[] {
                "resources/repo3/A/A.csproj",
                "resources/repo3/A.UTest/A.UTest.csproj",
            }, "Clean", true, Environment.ProcessorCount);

            // Failing with:
            //   Exception non gérée: Microsoft.Build.Exceptions.BuildAbortedException: Build was canceled.Failed to successfully launch or connect to a child MSBuild.exe process.Verify that the MSBuild.exe "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" launches successfully, and that it is loading the same microsoft.build.dll that the launching process loaded. If the location seems incorrect, try specifying the correct location in the BuildParameters object, or with the MSBUILD_EXE_PATH environment variable.
            //     à Microsoft.Build.Execution.BuildManager.EndBuild()
            // Whereas it works if
            //    <ProjectReference Include="..\A\A.csproj">
            // is deleted in 'A.UTest.csproj'.
            builder.Build(new[] {
                "resources/repo3/A/A.csproj",
                "resources/repo3/A.UTest/A.UTest.csproj",
                "resources/repo3/B/B.csproj",
            }, "Clean", true, Environment.ProcessorCount);
        }
    }
}
