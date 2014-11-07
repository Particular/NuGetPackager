using System.Linq;

namespace NuGetPackager
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Build.Utilities;
    using NuGet;

    class DeploymentPackageCreator
    {
        readonly TaskLoggingHelper log;
        readonly string nugetsFolderFullPath;
        readonly string chocosFolderFullPath;
        readonly string deployFolderFullPath;
        readonly string packagesFolderFullPath;
        readonly string productName;
        readonly string version;
        readonly string branch;

        public DeploymentPackageCreator(string nugetsFolderFullPath, string chocosFolderFullPath, string deployFolderFullPath, string packagesFolderFullPath, string productName, string version, string branch, TaskLoggingHelper log)
        {
            this.log = log;
            this.chocosFolderFullPath = chocosFolderFullPath;
            this.nugetsFolderFullPath = nugetsFolderFullPath;
            this.deployFolderFullPath = deployFolderFullPath;
            this.packagesFolderFullPath = packagesFolderFullPath;
            this.productName = productName;
            this.version = version;
            this.branch = branch;
        }

        public void CreateDeploymentPackages()
        {
            foreach (var nupkg in Directory.GetFiles(nugetsFolderFullPath, "*.nupkg"))
            {
                File.Copy(nupkg, nupkg + ".nzip", true);
            }
            foreach (var nupkg in Directory.GetFiles(chocosFolderFullPath, "*.nupkg"))
            {
                File.Copy(nupkg, nupkg + ".czip", true);
            }

            try
            {
                CreateDeployPackage(productName + ".Deploy", "Octopus package for release " + productName + ".");
                
                if (!log.HasLoggedErrors)
                {
                    ExtractScriptFromResource(deployFolderFullPath, "create_update_octopus_project.ps1", ReplaceVersionControlValues);                    
                }
            }
            finally
            {
                // Clean up
                foreach (var nupkg in Directory.GetFiles(nugetsFolderFullPath, "*.nzip"))
                {
                    File.Delete(nupkg);
                }
                foreach (var nupkg in Directory.GetFiles(chocosFolderFullPath, "*.czip"))
                {
                    File.Delete(nupkg);
                }
            }
        }

        string ReplaceVersionControlValues(string script)
        {
            var versionParts = version.Split('.');
            var major = versionParts[0];
            var minor = versionParts[1];
            return script
                .Replace("{{Branch}}", branch)
                .Replace("{{Major}}", major)
                .Replace("{{Minor}}", minor)
                .Replace("{{Version}}", version)
                .Replace("{{Product}}", productName);
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
            foreach (var nupkg in Directory.GetFiles(chocosFolderFullPath, "*.czip"))
            {
                packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = nupkg, Target = "content" } });
            }
        }

        void AddTools(PackageBuilder packageBuilder)
        {
            var tools = new[]
            {
                "NuGet.exe",
                "ReleaseNotesCompiler.CLI.exe",
                "ConsoleTweet.exe"
            };

            var toolLocations = tools.Select(FindTool).ToList();

            if (toolLocations.All(loc => loc != null))
            {
                packageBuilder.PopulateFiles("", toolLocations.Select(loc => new ManifestFile
                {
                    Source = loc,
                    Target = "tools"
                }).ToArray());
            }
        }

        string FindTool(string name)
        {
            var nugetCLI = Directory.GetFiles(packagesFolderFullPath, name, SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(nugetCLI))
            {
                log.LogError("Could not find tool '{0}' in '{1}' for deployment script.", name, packagesFolderFullPath);
                return null;
            }
            return nugetCLI;
        }

        void AddDeployScript(PackageBuilder packageBuilder)
        {
            var tempPath = Path.GetTempPath();
            ExtractScriptFromResource(tempPath,"Deploy.ps1", ReplaceVersionControlValues);

            var deployFile = Path.Combine(Path.GetTempPath(), "Deploy.ps1");
            packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = deployFile, Target = "Deploy.ps1" } });
        }

        static void ExtractScriptFromResource(string destinationFolderFullPath, string fileName, Func<string, string> replace)
        {
            var destinationPath = Path.Combine(destinationFolderFullPath, fileName);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NuGetPackager.Scripts."+fileName))
            using (var reader = new StreamReader(resource))
            using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            {
                var script = reader.ReadToEnd();
                script = replace(script);
                writer.Write(script);
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
