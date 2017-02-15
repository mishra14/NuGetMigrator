using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Newtonsoft.Json.Linq;

namespace NuGetMigrator
{
    class Program
    {
        private static string _dotnetPath = @"E:\cli\artifacts\win10-x64\stage2\dotnet.exe";

        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("MSBuildSdksPath", 
                @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\Sdks", 
                EnvironmentVariableTarget.Process);

            var projectFolder = @"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade\VisualStudio15.Packages"; //args[0];
            var projectJsonPath = Path.Combine(projectFolder, "project.json");
            var csprojPath = Directory.GetFiles(projectFolder, "*.csproj")[0];
            var tempCSProjPath = CreateTempCSProjFile();
            PrepareCSprojFile(csprojPath, tempCSProjPath);
            var project = GetProject(csprojPath);
            using (StreamReader r = File.OpenText(projectJsonPath))
            {
                var jsonString = r.ReadToEnd();
                var jsonObj = JObject.Parse(jsonString);
                var dependencies = jsonObj["dependencies"];
                var frameworks = jsonObj["frameworks"];
                var runtimes = jsonObj["runtimes"];
                MigrateFrameworks(frameworks, project, csprojPath);
                MigrateDependencies(dependencies, project, projectFolder);

                
            }
            project.Save();

        }

        private static void MigrateDependencies(JToken dependencies, Project project, string projectFolder)
        {
            foreach (var dependency in dependencies)
            {
                var id = (dependency as JProperty).Name;
                var version = (dependency as JProperty).Value;
                if (!GetPackageReferences(project, id).Any())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        RedirectStandardOutput = false,
                        RedirectStandardInput = false,
                        UseShellExecute = false,
                        WorkingDirectory = projectFolder,
                        FileName = _dotnetPath,
                        Arguments = $"add package {id} -v {version}"
                    };
                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.Start();
                        process.WaitForExit();
                    }
                }
            }
        }

        private static void MigrateFrameworks(JToken frameworks, Project project, string csprojPath)
        {
            var frameworksStringBuilder = new StringBuilder();
            var xml = XElement.Load(csprojPath);
            var first = true;
            foreach (var framework in frameworks)
            {
                if (!first)
                {
                    frameworksStringBuilder.Append(';');
                }
                else
                {
                    first = false;
                }
                var id = (framework as JProperty).Name;
                frameworksStringBuilder.Append(id);
            }
            XElement frameworkPropertyGroup = null;
            var property = string.Empty;
            if (frameworks.Count() > 1)
            {
                property = "TargetFrameworks";
            }
            else
            {
                property = "TargetFramework";
            }

            project.SetProperty(property, frameworksStringBuilder.ToString());

            xml.Add(frameworkPropertyGroup);
            xml.Save(csprojPath);
        }
        private static string CreateTempCSProjFile()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
                UseShellExecute = false,
                WorkingDirectory = tempFolder,
                FileName = _dotnetPath,
                Arguments = $"new console"
            };
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            
            return Directory.GetFiles(tempFolder, "*.csproj")[0];
        }
        private static void PrepareCSprojFile(string csprojPath, string tempCSProjPath)
        {
            var xmlRoot = XElement.Load(tempCSProjPath);
            xmlRoot.SetAttributeValue(XName.Get("ToolsVersion"), null);
            xmlRoot.SetAttributeValue(XName.Get("DefaultTargets"), null);
            xmlRoot.SetAttributeValue(XName.Get("xmlns"), null);
            xmlRoot.SetAttributeValue(XName.Get("Sdk"), "Microsoft.NET.Sdk");

            xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == "Import" && e.Attribute(XName.Get("Project")).Value == @"$(MSBuildToolsPath)\Microsoft.CSharp.targets")
                .ToList()
                .ForEach(e => e.Remove());

            xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == "Target" && e.Attribute(XName.Get("Name")).Value == "BeforeBuild")
                .ToList()
                .ForEach(e => e.Remove());

            xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == "ItemGroup")
                .ToList()
                .ForEach(e => e.Remove());

            xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == "PropertyGroup")
                .ToList()
                .ForEach(e => e.Remove());

            xmlRoot.Save(csprojPath, SaveOptions.None);
        }


        private static Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new Exception($"Unable to open project: {projectCSProjPath}");
            }
            return new Project(projectRootElement);
        }
        private static ProjectRootElement TryOpenProjectRootElement(string filename)
        {
            try
            {
                // There is ProjectRootElement.TryOpen but it does not work as expected
                // I.e. it returns null for some valid projects
                return ProjectRootElement.Open(filename);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }
        private static IEnumerable<ProjectItem> GetPackageReferences(Project project, string id)
        {
            return project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}