using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace NuGetMigrator
{
    class ProjectJsonToLegacyMigrator
    {
        public void Migrate(string projectFolderPath)
        {
            if (LegacyProjectSummary.CanProjectBeMigrated(projectFolderPath))
            {
                Console.WriteLine($"Migrating {projectFolderPath}");
                var projectDetails = LegacyProjectSummary.ExtractDetails(projectFolderPath);
                var tempCSProjPath = CreateTempCSProjFile(projectDetails.CSProjPath);
                var xmlDoc = XDocument.Load(tempCSProjPath);
                var ns = xmlDoc.Root.GetDefaultNamespace();

                MigratePackageReferences(projectDetails.PackageReferences, xmlDoc, ns);

                xmlDoc.Save(tempCSProjPath);

                File.Copy(projectDetails.CSProjPath, projectDetails.CSProjPath + ".old", overwrite: true);
                File.Copy(tempCSProjPath, projectDetails.CSProjPath, overwrite: true);

                if (File.Exists(projectDetails.ProjectJsonPath + ".old"))
                {
                    File.Delete(projectDetails.ProjectJsonPath + ".old");
                }

                File.Move(projectDetails.ProjectJsonPath, projectDetails.ProjectJsonPath + ".old");
                if (projectDetails.AssemblyInfoPath != null && File.Exists(projectDetails.AssemblyInfoPath))
                {
                    File.Delete(projectDetails.AssemblyInfoPath);
                }

                var tempDir = Path.GetDirectoryName(tempCSProjPath);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        private void MigratePackageReferences(JToken packageReferences, XDocument xmlDoc, XNamespace ns)
        {     
            if (packageReferences.Any())
            {
                var itemGroupElement = new XElement(ns +LegacyProjectSummary.ITEM_GROUP_TAG);                
                foreach (var dependency in packageReferences)
                {
                    var id = (dependency as JProperty).Name;
                    var version = (dependency as JProperty).Value;
                    var packageReferenceElement = new XElement(ns + LegacyProjectSummary.PACKAGE_REFERNCE_TAG);
                    packageReferenceElement.SetAttributeValue(LegacyProjectSummary.VERSION_TAG, version);
                    packageReferenceElement.SetAttributeValue(LegacyProjectSummary.INCLUDE_TAG, id);
                    itemGroupElement.Add(packageReferenceElement);
                }

                xmlDoc.Root.Add(itemGroupElement);
            }
        }

        private string CreateTempCSProjFile(string originalCSProjFile)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempCSProjFile = Path.Combine(tempFolder, Path.GetFileName(originalCSProjFile));
            Directory.CreateDirectory(tempFolder);
            File.Copy(originalCSProjFile, tempCSProjFile);
            return tempCSProjFile;
        }       
    }
}

