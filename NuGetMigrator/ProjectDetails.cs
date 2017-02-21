using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace NuGetMigrator
{
    class LegacyProjectDetails
    {
        public const string PROPERTY_GROUP_TAG = "PropertyGroup";
        public const string ITEM_GROUP_TAG = "ItemGroup";
        public const string PACKAGE_REFERNCE_TAG = "PackageReference";
        public const string PROJECT_REFERNCE_TAG = "ProjectReference";
        public const string ASSEMBLY_REFERNCE_TAG = "Reference";
        public const string IMPORT_TAG = "Import";
        public const string TARGET_TAG = "Target";
        public const string PROJECT_TAG = "Project";
        public const string PROJECT_GUID_TAG = "ProjectGuid";
        public const string ROOT_NAMESPACE_TAG = "RootNamespace";
        public const string ASSEMBLY_NAME_TAG = "AssemblyName";
        public const string CODE_ANALYSIS_RULE_SET_TAG = "CodeAnalysisRuleSet";
        public const string TARGET_FRAMEWORK_TAG = "TargetFramework";
        public const string TARGET_FRAMEWORKS_TAG = "TargetFrameworks";
        public const string INCLUDE_TAG = "Include";
        public const string VERSION_TAG = "Version";
        public const string OUTPUT_TYPE_TAG = "OutputType";
        public const string CHOOSE_TAG = "Choose";
        public const string WHEN_TAG = "When";
        public const string CONDITION_TAG = "Condition";
        public const string GENERATE_ASSEMBLY_INFO_TAG = "GenerateAssemblyInfo";
        public const string EMBEDDED_RESOURCE_TAG = "EmbeddedResource";
        public const string COMPILE_TAG = "Compile";
        public const string UPDATE_TAG = "Update";
        public const string GENERATOR_TAG = "Generator";
        public const string LAST_GEN_OUTPUT_TAG = "LastGenOutput";
        public const string DESIGN_TIME_TAG = "DesignTime";
        public const string AUTO_GEN_TAG = "AutoGen";
        public const string DEPENDENT_UPON_TAG = "DependentUpon";

        public string ProjectFolderPath { get; set; }
        public string CSProjPath { get; set; }
        public string ProjectJsonPath { get; set; }
        public string AssemblyInfoPath { get; set; }
        public JToken PackageReferences { get; set; }
        public JToken Frameworks { get; set; }
        public JToken RunTimes { get; set; }
        public IEnumerable<XElement> Imports { get; set; }
        public IEnumerable<XElement> Targets { get; set; }
        public IEnumerable<XElement> ProjectReferences { get; set; }
        public IEnumerable<XElement> AssemblyReferences { get; set; }
        public IEnumerable<XElement> ChooseElements { get; set; }
        public XElement ProjectGuid { get; set; }
        public XElement RootNameSpace { get; set; }
        public XElement AssemblyName { get; set; }
        public XElement CodeAnalysisRuleSet { get; set; }
        public XElement GenerateAssemblyInfo { get; set; }
        public XElement ResourceFileItemGroup { get; set; }
        public IEnumerable<XElement> AssemblyInfo { get; set; }

        public static LegacyProjectDetails ExtractDetails(string projectFolderPath)
        {
            var projectJsonPath = Path.Combine(projectFolderPath, "project.json");
            var csprojPath = Directory.GetFiles(projectFolderPath, "*.csproj", SearchOption.TopDirectoryOnly)[0];
            var projectDetails = new LegacyProjectDetails()
            {
                ProjectFolderPath = projectFolderPath,
                CSProjPath = csprojPath,
                ProjectJsonPath = projectJsonPath
            };

            projectDetails.ExtractProjectJsonDetails();
            projectDetails.ExtractCSProjDetails();
            projectDetails.ExtractResourceFileDetails();
            projectDetails.ExtractAssemblyInfo();

            return projectDetails;
        }

        public static bool CanProjectBeMigrated(string projectFolderPath)
        {
            var projectJsonPath = Path.Combine(projectFolderPath, "project.json");
            var csprojPath = Directory.GetFiles(projectFolderPath, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (!File.Exists(csprojPath))
            {
                Console.WriteLine($"Project {Path.GetFileName(projectFolderPath)} cannot be migrated. csproj file {csprojPath} not found.");
                return false;
            }

            if (!File.Exists(projectJsonPath))
            {
                Console.WriteLine($"Project {Path.GetFileName(projectFolderPath)} cannot be migrated. Project.Json file {projectJsonPath} not found.");
                return false;
            }
            return true;
        }

        private void ExtractCSProjDetails()
        {
            var xmlRoot = XElement.Load(CSProjPath);
            Imports = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == IMPORT_TAG && e.Attribute(XName.Get(PROJECT_TAG)).Value != @"$(MSBuildToolsPath)\Microsoft.CSharp.targets")
                .ToList();
            Targets = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == TARGET_TAG)
                .ToList();
            ChooseElements = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == CHOOSE_TAG)
                .ToList();
            ProjectReferences = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == PROJECT_REFERNCE_TAG)
                .ToList();
            AssemblyReferences = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == ASSEMBLY_REFERNCE_TAG)
                .ToList();
            ProjectGuid = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == PROJECT_GUID_TAG)
                .FirstOrDefault();
            RootNameSpace = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == ROOT_NAMESPACE_TAG)
                .FirstOrDefault();
            AssemblyName = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == ASSEMBLY_NAME_TAG)
                .FirstOrDefault();
            CodeAnalysisRuleSet = xmlRoot.Descendants()
                .Where(e => e.Name.LocalName == CODE_ANALYSIS_RULE_SET_TAG)
                .FirstOrDefault();
        }

        public void DisableGenerateAssemblyInfo()
        {
            var element = new XElement(GENERATE_ASSEMBLY_INFO_TAG);
            element.SetAttributeValue(CONDITION_TAG, $"'$({GENERATE_ASSEMBLY_INFO_TAG})' == ''");
            element.Value = "false";
            GenerateAssemblyInfo = element;
        }

        public void ExtractProjectJsonDetails()
        {
            using (StreamReader r = File.OpenText(ProjectJsonPath))
            {
                var jsonString = r.ReadToEnd();
                var jsonObj = JObject.Parse(jsonString);
                PackageReferences = jsonObj["dependencies"];
                Frameworks = jsonObj["frameworks"];
                RunTimes = jsonObj["runtimes"];
            }
        }

        /*
         * <ItemGroup>
            <Compile Update="Resource1.Designer.cs">
                <DesignTime>True</DesignTime>
                <AutoGen>True</AutoGen>
                <DependentUpon>Resource1.resx</DependentUpon>
            </Compile>
            </ItemGroup>

            <ItemGroup>
            <EmbeddedResource Update="Resource1.resx">
                <Generator>ResXFileCodeGenerator</Generator>
                <LastGenOutput>Resource1.Designer.cs</LastGenOutput>
            </EmbeddedResource>
            </ItemGroup> 
         * */
        public void ExtractResourceFileDetails()
        {
            var resourceFiles = Directory.GetFiles(ProjectFolderPath, "*.resx", SearchOption.TopDirectoryOnly);
            if (resourceFiles.Any())
            {
                ResourceFileItemGroup = new XElement(ITEM_GROUP_TAG);
                foreach(var resourceFile in resourceFiles)
                {
                    var resourceFileName = Path.GetFileName(resourceFile);
                    var designerFileName = Path.GetFileNameWithoutExtension(resourceFile) + ".Designer.cs";

                    var embeddedResourceElement = new XElement(EMBEDDED_RESOURCE_TAG);
                    embeddedResourceElement.SetAttributeValue(UPDATE_TAG, resourceFileName);
                    embeddedResourceElement.Add(new XElement(GENERATOR_TAG, "ResXFileCodeGenerator"));
                    embeddedResourceElement.Add(new XElement(LAST_GEN_OUTPUT_TAG, designerFileName));
                    ResourceFileItemGroup.Add(embeddedResourceElement);

                    //var compileElement = new XElement(COMPILE_TAG);
                    //compileElement.SetAttributeValue(UPDATE_TAG, designerFileName);
                    //compileElement.Add(new XElement(DESIGN_TIME_TAG, "True"));
                    //compileElement.Add(new XElement(AUTO_GEN_TAG, "True"));
                    //compileElement.Add(new XElement(DEPENDENT_UPON_TAG, resourceFileName));
                    //ResourceFileItemGroup.Add(compileElement);
                }
            }
        }

        private void ExtractAssemblyInfo()
        {
            if(Directory.Exists(Path.Combine(ProjectFolderPath, "Properties")))
            {
                AssemblyInfoPath = Directory.GetFiles(Path.Combine(ProjectFolderPath, "Properties"), "AssemblyInfo.cs", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

                var lines = File.ReadAllLines(AssemblyInfoPath)
                    .Where(l => l.Contains("[assembly: "));

                if (lines.Any())
                {
                    var infoList = new List<XElement>();
                    foreach (var line in lines)
                    {
                        var assemblyInfo = GetAssemblyInfoFromLine(line);
                        if (assemblyInfo != null)
                        {
                            var assemblyInfoElement = new XElement(assemblyInfo.Item1)
                            {
                                Value = assemblyInfo.Item2
                            };
                            infoList.Add(assemblyInfoElement);
                        }
                    }
                    AssemblyInfo = infoList;
                }
            }
        }

        public Tuple<string, string> GetAssemblyInfoFromLine(string line)
        {
            if (line.Contains(":") && line.Contains("(") && line.Contains(")"))
            {
                var data = line.Split(':')[1];
                var attribute = data.Split('(')[0].Trim();
                var value = data.Split('(')[1].Trim();
                var containsQuotes = value.StartsWith("\"");
                value = value.Substring(containsQuotes ? 1 : 0, value.Length - (containsQuotes ? 4 : 2));
                return new Tuple<string, string>(attribute, value);
            }

            return null;
        }
    }
}
