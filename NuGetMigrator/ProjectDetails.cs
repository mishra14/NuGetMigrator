using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public string CSProjPath { get; set; }
        public string ProjectJsonPath { get; set; }
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


        public static LegacyProjectDetails ExtractDetails(string projectFolderPath)
        {
            var projectJsonPath = Path.Combine(projectFolderPath, "project.json");
            var csprojPath = Directory.GetFiles(projectFolderPath, "*.csproj", SearchOption.TopDirectoryOnly)[0];
            var projectDetails = new LegacyProjectDetails()
            {
                CSProjPath = csprojPath,
                ProjectJsonPath = projectJsonPath
            };

            projectDetails.ExtractProjectJsonDetails();
            projectDetails.ExtractCSProjDetails();

            return projectDetails;
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
    }
}
