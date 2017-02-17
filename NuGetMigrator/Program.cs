using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Newtonsoft.Json.Linq;

namespace NuGetMigrator
{
    class LegacyToXplatMigrator
    {
        public string DotnetPath { get; set; }

        static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("MSBuildSdksPath", 
                @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\Sdks", 
                EnvironmentVariableTarget.Process);

            // For testing
            var dotnetPath = @"E:\cli\artifacts\win10-x64\stage2\dotnet.exe";
            var root = @"E:\migrate\NuGet.Client\src\NuGet.Clients";
            var projectFolderPaths = new List<DirectoryInfo>
            {
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade"),
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade\VisualStudio14.Packages"),
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade\VisualStudio15.Packages")
            };

            //var dotnetPath = args[0];
            //var root = args[1];

            var csProjFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
            //var projectFolderPaths = csProjFiles.Select(p => Directory.GetParent(p));

            var migrator = new LegacyToXplatMigrator(dotnetPath);
            foreach(var projectFolderPath in projectFolderPaths)
            {
                if(!projectFolderPath.FullName.Contains("NuGet.Tools"))
                {
                    migrator.Migrate(projectFolderPath.FullName);
                }
            }
            
        }

        public LegacyToXplatMigrator(string dotnetPath)
        {
            DotnetPath = dotnetPath ?? throw new ArgumentNullException(nameof(dotnetPath));
        }

        public void Migrate(string projectFolderPath)
        {
            if (LegacyProjectDetails.CanProjectBeMigrated(projectFolderPath))
            {
                Console.WriteLine($"Migrating {projectFolderPath}");
                var tempCSProjPath = CreateTempCSProjFile();
                var projectDetails = LegacyProjectDetails.ExtractDetails(projectFolderPath);
                var xmlRoot = XElement.Load(tempCSProjPath);
                var ns = xmlRoot.GetDefaultNamespace();

                projectDetails.DisableGenerateAssemblyInfo();

                MigrateFrameworks(projectDetails.Frameworks, xmlRoot, ns);
                MigrateProperties(projectDetails, xmlRoot, ns);
                MigrateChooseElements(projectDetails.ChooseElements, xmlRoot, ns);
                MigrateDependencies(projectDetails, xmlRoot, ns);
                MigrateImports(projectDetails.Imports, xmlRoot, ns);
                MigrateTargets(projectDetails.Targets, xmlRoot, ns);
                MigrateResourceFiles(projectDetails.ResourceFileItemGroup, xmlRoot, ns);

                xmlRoot = RemoveAllNamespaces(xmlRoot);
                xmlRoot.Save(tempCSProjPath);

                File.Copy(projectDetails.CSProjPath, projectDetails.CSProjPath + ".old", overwrite: true);
                File.Copy(tempCSProjPath, projectDetails.CSProjPath, overwrite: true);

                if(File.Exists(projectDetails.ProjectJsonPath + ".old"))
                {
                    File.Delete(projectDetails.ProjectJsonPath + ".old");
                }
                File.Move(projectDetails.ProjectJsonPath, projectDetails.ProjectJsonPath + ".old");

                var tempDir = Path.GetDirectoryName(tempCSProjPath);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        private void MigrateResourceFiles(XElement resourceFileItemGroup, XElement xmlRoot, XNamespace ns)
        {
            if(resourceFileItemGroup != null)
            {
                xmlRoot.Add(resourceFileItemGroup, ns);
            }
        }

        // Picked part of this from http://stackoverflow.com/questions/987135/how-to-remove-all-namespaces-from-xml-with-c
        private static XElement RemoveAllNamespaces(XElement xmlRoot)
        {
            XElement element = null;
            if (!xmlRoot.HasElements)
            {
                element = new XElement(xmlRoot.Name.LocalName);
                if (!string.IsNullOrEmpty(xmlRoot.Value) && !string.IsNullOrWhiteSpace(xmlRoot.Value))
                {
                    element.Value = xmlRoot.Value;
                }
            }
            else
            {
                element = new XElement(xmlRoot.Name.LocalName, xmlRoot.Elements().Select(el => RemoveAllNamespaces(el)));
            }

            foreach (var attribute in xmlRoot.Attributes())
            {
                element.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
            }

            return element;
        }

        private void MigrateChooseElements(IEnumerable<XElement> chooseElements, XElement xmlRoot, XNamespace ns)
        {
            foreach(var chooseElement in chooseElements)
            {
                var whenElements = chooseElement.Descendants().Where(e => e.Name.LocalName == LegacyProjectDetails.WHEN_TAG);
                foreach (var whenElement in whenElements)
                {
                    var conditionedGroups = whenElement.Elements();
                    foreach(var conditionedGroup in conditionedGroups)
                    {
                        var condition = whenElement.Attribute(LegacyProjectDetails.CONDITION_TAG);
                        conditionedGroup.SetAttributeValue(condition.Name.LocalName, condition.Value);

                        xmlRoot.Add(conditionedGroup, ns);
                    }
                }
            }
        }

        private void MigrateFrameworks(JToken frameworks, Project project, string csprojPath)
        {
            var frameworksStringBuilder = new StringBuilder();
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
        }

        private void MigrateFrameworks(JToken frameworks, XElement xmlRoot, XNamespace ns)
        {
            var frameworksStringBuilder = new StringBuilder();
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
            var property = string.Empty;
            if (frameworks.Count() > 1)
            {
                property = LegacyProjectDetails.TARGET_FRAMEWORKS_TAG;
            }
            else
            {
                property = LegacyProjectDetails.TARGET_FRAMEWORK_TAG;
            }

            var frameworkElement = xmlRoot.Descendants().Where(e => e.Name.LocalName == LegacyProjectDetails.TARGET_FRAMEWORK_TAG).FirstOrDefault();
            if (frameworkElement == null)
            {
                xmlRoot.Add(new XElement(LegacyProjectDetails.PROPERTY_GROUP_TAG, new XElement(property, frameworksStringBuilder.ToString())), ns);
            }
            else
            {
                if (frameworks.Count() > 1)
                {
                    frameworkElement.Remove();
                    xmlRoot.Add(new XElement(LegacyProjectDetails.PROPERTY_GROUP_TAG, new XElement(property, frameworksStringBuilder.ToString())), ns);
                }
                else
                {
                    frameworkElement.Value = frameworksStringBuilder.ToString();
                }
            }
        }

        private void MigrateDependencies(LegacyProjectDetails projectDetails, XElement xmlRoot, XNamespace ns)
        {
            MigrateAssemblyReferences(projectDetails.AssemblyReferences, xmlRoot, ns);
            MigrateProjectReferences(projectDetails.ProjectReferences, xmlRoot, ns);
            MigratePackageReferences(projectDetails.PackageReferences, xmlRoot, ns);
        }

        private void MigrateAssemblyReferences(IEnumerable<XElement> assemblyReferences, XElement xmlRoot, XNamespace ns)
        {
            if (assemblyReferences.Any())
            {
                var itemGroupElement = new XElement(LegacyProjectDetails.ITEM_GROUP_TAG);
                foreach (var assemblyReference in assemblyReferences)
                {
                    if (!XmlContainsReference(xmlRoot, LegacyProjectDetails.ASSEMBLY_REFERNCE_TAG, assemblyReference))
                    {
                        itemGroupElement.Add(assemblyReference);
                    }
                }
                xmlRoot.Add(itemGroupElement);
            }
        }

        private void MigrateProjectReferences(IEnumerable<XElement> projectReferences, XElement xmlRoot, XNamespace ns)
        {
            if (projectReferences.Any())
            {
                var itemGroupElement = new XElement(LegacyProjectDetails.ITEM_GROUP_TAG);
                foreach (var projectReference in projectReferences)
                {
                    if (!XmlContainsReference(xmlRoot, LegacyProjectDetails.PROJECT_REFERNCE_TAG, projectReference))
                    {
                        itemGroupElement.Add(projectReference);
                    }
                }
                xmlRoot.Add(itemGroupElement);
            }
        }

        private void MigratePackageReferences(JToken packageReferences, XElement xmlRoot, XNamespace ns)
        {
            if (packageReferences.Any())
            {
                var itemGroupElement = new XElement(LegacyProjectDetails.ITEM_GROUP_TAG);
                foreach (var dependency in packageReferences)
                {
                    var id = (dependency as JProperty).Name;
                    var version = (dependency as JProperty).Value;
                    var packageReferenceElement = new XElement(LegacyProjectDetails.PACKAGE_REFERNCE_TAG);
                    packageReferenceElement.SetAttributeValue(LegacyProjectDetails.VERSION_TAG, version);
                    packageReferenceElement.SetAttributeValue(LegacyProjectDetails.INCLUDE_TAG, id);
                    itemGroupElement.Add(packageReferenceElement);
                }
                xmlRoot.Add(itemGroupElement);
            }
        }

        private void MigratePackageReferences(JToken dependencies, Project project, string projectFolder)
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
                        WorkingDirectory = project.DirectoryPath,
                        FileName = DotnetPath,
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

        private bool XmlContainsReference(XElement xmlRoot, string referenceType, XElement reference)
        {
            var includeValue = reference.Attribute(LegacyProjectDetails.INCLUDE_TAG).Value;
            return xmlRoot
                .Descendants()
                .Where(e => e.Name.LocalName == referenceType && e.Attribute(LegacyProjectDetails.INCLUDE_TAG).Value == includeValue)
                .Any();
        }

        private void MigrateProperties(LegacyProjectDetails projectDetails, XElement xmlRoot, XNamespace ns)
        {
            var propertyGroupElement = xmlRoot
                .Descendants()
                .Where(e => e.Name.LocalName == LegacyProjectDetails.PROPERTY_GROUP_TAG && 
                            e.Descendants().Where(f => f.Name.LocalName == LegacyProjectDetails.OUTPUT_TYPE_TAG).Any())
                 .FirstOrDefault();
                
            if(propertyGroupElement == null)
            {
                propertyGroupElement = new XElement(LegacyProjectDetails.PROPERTY_GROUP_TAG);
                
                //propertyGroupElement.Add(projectDetails.ProjectGuid);
                propertyGroupElement.Add(projectDetails.RootNameSpace);
                //propertyGroupElement.Add(projectDetails.AssemblyName);
                //propertyGroupElement.Add(projectDetails.CodeAnalysisRuleSet);

                if(projectDetails.GenerateAssemblyInfo != null)
                {
                    propertyGroupElement.Add(projectDetails.GenerateAssemblyInfo);
                }

                xmlRoot.Add(propertyGroupElement, ns);
            }
            else
            {
                //propertyGroupElement.Add(projectDetails.ProjectGuid);
                propertyGroupElement.Add(projectDetails.RootNameSpace);
                //propertyGroupElement.Add(projectDetails.AssemblyName);
                //propertyGroupElement.Add(projectDetails.CodeAnalysisRuleSet);

                if (projectDetails.GenerateAssemblyInfo != null)
                {
                    propertyGroupElement.Add(projectDetails.GenerateAssemblyInfo);
                }
            }
        }

        private void MigrateImports(IEnumerable<XElement> imports, XElement xmlRoot, XNamespace ns)
        {
            foreach(var import in imports)
            {
                xmlRoot.Add(import, ns);
            }
        }

        private void MigrateTargets(IEnumerable<XElement> targets, XElement xmlRoot, XNamespace ns)
        {
            foreach (var target in targets)
            {
                xmlRoot.Add(target, ns);
            }
        }

        private string CreateTempCSProjFile()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
                UseShellExecute = false,
                WorkingDirectory = tempFolder,
                FileName = DotnetPath,
                Arguments = $"new classlib"
            };
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            
            return Directory.GetFiles(tempFolder, "*.csproj")[0];
        }

        // Can be used if the original csproj file has to be edited in place
        private void PrepareCSprojFile(string csprojPath, string tempCSProjPath)
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


        // Can be used if msbuild needs to be used for inserting properties/references
        private Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new Exception($"Unable to open project: {projectCSProjPath}");
            }
            return new Project(projectRootElement);
        }

        // Can be used if msbuild needs to be used for inserting properties/references
        private ProjectRootElement TryOpenProjectRootElement(string filename)
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

        // Can be used if msbuild needs to be used for inserting properties/references
        private IEnumerable<ProjectItem> GetPackageReferences(Project project, string id)
        {
            return project.AllEvaluatedItems
                .Where(item => item.ItemType.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) &&
                               item.EvaluatedInclude.Equals(id, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}