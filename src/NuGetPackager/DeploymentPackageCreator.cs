using System.Linq;

namespace NuGetPackager
{
    using System.IO;
    using System.Reflection;
    using Microsoft.Build.Utilities;
    using NuGet;

    class DeploymentPackageCreator
    {
        readonly TaskLoggingHelper log;
        readonly string nugetsFolderFullPath;
        readonly string deployFolderFullPath;
        readonly string packagesFolderFullPath;
        readonly string productName;
        readonly string version;

        public DeploymentPackageCreator(string nugetsFolderFullPath, string deployFolderFullPath, string packagesFolderFullPath, string productName, string version, TaskLoggingHelper log)
        {
            this.log = log;
            this.nugetsFolderFullPath = nugetsFolderFullPath;
            this.deployFolderFullPath = deployFolderFullPath;
            this.packagesFolderFullPath = packagesFolderFullPath;
            this.productName = productName;
            this.version = version;
        }

        public void CreateDeploymentPackages()
        {
            foreach (var nupkg in Directory.GetFiles(nugetsFolderFullPath, "*.nupkg"))
            {
                File.Copy(nupkg, nupkg + ".nzip", true);
            }

            try
            {
                CreateDeployPackage(productName + ".Deploy", "Octopus package for release " + productName + ".");
                
                if (!log.HasLoggedErrors)
                {
                    ExtractScriptFromResource(deployFolderFullPath, "create_update_octopus_project.ps1");                    
                }
            }
            finally
            {
                // Clean up
                foreach (var nupkg in Directory.GetFiles(nugetsFolderFullPath, "*.?zip"))
                {
                    File.Delete(nupkg);
                }
            }
        }

        void CreateDeployPackage(string id, string description)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = id,
                Description = description,
                Version = SemanticVersion.Parse(version)
            };
            packageBuilder.Authors.Add("Particular Software");
            AddDeployScript(packageBuilder);
            AddTools(packageBuilder);
            AddContent(packageBuilder);

            if (!log.HasLoggedErrors)
            {
                SavePackage(packageBuilder, deployFolderFullPath, ".nupkg", "Package created -> {0}");
            }
        }

        void AddContent(PackageBuilder packageBuilder)
        {
            foreach (var nupkg in Directory.GetFiles(nugetsFolderFullPath, "*.nzip"))
            {
                packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = nupkg, Target = "content" } });
            }
        }

        void AddTools(PackageBuilder packageBuilder)
        {
            var nugetCLI = Directory.GetFiles(packagesFolderFullPath, "NuGet.exe", SearchOption.AllDirectories).FirstOrDefault();
            var releaseNotesCompiler = Directory.GetFiles(packagesFolderFullPath, "ReleaseNotesCompiler.CLI.exe", SearchOption.AllDirectories).FirstOrDefault();

            var error = false;

            if (string.IsNullOrEmpty(nugetCLI))
            {
                log.LogError("Could not find tool 'NuGet.exe' in '{0}' for deployment script.", packagesFolderFullPath);
                error = true;
            }

            if (string.IsNullOrEmpty(releaseNotesCompiler))
            {
                log.LogError("Could not find tool 'ReleaseNotesCompiler.CLI.exe' in '{0}' for deployment script.", packagesFolderFullPath);
                error = true;
            }

            if (!error)
            {
                packageBuilder.PopulateFiles("", new[] {
                new ManifestFile { Source = nugetCLI, Target = "tools" },
                new ManifestFile { Source = releaseNotesCompiler, Target = "tools" }
            });
            }
        }

        static void AddDeployScript(PackageBuilder packageBuilder)
        {
            var tempPath = Path.GetTempPath();
            ExtractScriptFromResource(tempPath,"Deploy.ps1");

            var deployFile = Path.Combine(Path.GetTempPath(), "Deploy.ps1");
            packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = deployFile, Target = "Deploy.ps1" } });
        }

        static void ExtractScriptFromResource(string destinationFolderFullPath, string fileName)
        {
            var destinationPath = Path.Combine(destinationFolderFullPath, fileName);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NuGetPackager.Scripts."+fileName))
            using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                resource.CopyTo(file);
                file.Flush();
            }
        }

        void SavePackage(PackageBuilder packageBuilder, string destinationFolder, string filenameSuffix, string logMessage)
        {
            var filename = Path.Combine(destinationFolder, packageBuilder.GetFullName()) + filenameSuffix;

            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            using (var file = new FileStream(filename, FileMode.Create))
            {
                packageBuilder.Save(file);
            }

            log.LogMessage(logMessage, filename);
        }
    }
}
