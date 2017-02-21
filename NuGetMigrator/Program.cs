using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGetMigrator
{
    class Program
    {
        static void Main(string[] args)
        {
            MigrateProjectJsonToLegacy();
            //MigrateLegacyToPackageRef();
        }


        static void MigrateProjectJsonToLegacy()
        {

            // For testing
            var root = @"E:\migrate\NuGet.Client\src\NuGet.Clients";
            var projectFolderPaths = new List<DirectoryInfo>
            {
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade"),
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade\VisualStudio14.Packages"),
                new DirectoryInfo(@"E:\migrate\NuGet.Client\src\NuGet.Clients\VisualStudio.Facade\VisualStudio15.Packages")
            };

            var csProjFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
            //var projectFolderPaths = csProjFiles.Select(p => Directory.GetParent(p));

            var migrator = new ProjectJsonToLegacyMigrator();
            foreach (var projectFolderPath in projectFolderPaths)
            {
                //if (!projectFolderPath.FullName.Contains("NuGet.Tools"))
                {
                    migrator.Migrate(projectFolderPath.FullName);
                }
            }
        }

        static void MigrateLegacyToPackageRef()
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

            var csProjFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
            //var projectFolderPaths = csProjFiles.Select(p => Directory.GetParent(p));

            var migrator = new LegacyToPackageRefMigrator(dotnetPath);
            foreach (var projectFolderPath in projectFolderPaths)
            {
                if (!projectFolderPath.FullName.Contains("NuGet.Tools"))
                {
                    migrator.Migrate(projectFolderPath.FullName);
                }
            }

        }
    }
}