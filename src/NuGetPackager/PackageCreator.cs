namespace NuGetPackager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Build.Utilities;
    using NuGet;

    class PackageCreator : IPropertyProvider
    {
        readonly Dictionary<string, Func<string>> propertyAssignments; 
        readonly string packagingFolderFullPath;
        readonly string nugetsFolderFullPath;
        readonly string chocosFolderFullPath;
        readonly string projectName;
        readonly TaskLoggingHelper log;
        readonly string version;

        public PackageCreator(string packagingFolderFullPath, string nugetsFolderFullPath, string chocosFolderFullPath, string projectName, string version, TaskLoggingHelper log)
        {
            this.packagingFolderFullPath = packagingFolderFullPath;
            this.nugetsFolderFullPath = nugetsFolderFullPath;
            this.chocosFolderFullPath = chocosFolderFullPath;
            this.projectName = projectName;
            this.log = log;
            this.version = version;

            propertyAssignments = new Dictionary<string, Func<string>>
            {
                { "version", () => this.version },
                { "authors", () => "NServiceBus Ltd" },
                { "owners", () => "NServiceBus Ltd" },
                { "licenseUrl", () => "http://particular.net/LicenseAgreement" },
                { "projectUrl", () => "http://particular.net/" },
                { "iconUrl", () => "http://s3.amazonaws.com/nuget.images/NServiceBus_32.png" },
                { "requireLicenseAcceptance", () => "true" },
                { "copyright", () => string.Format("Copyright 2010-{0} NServiceBus. All rights reserved", DateTime.UtcNow.Year) },
            };
        }

        public void CreatePackagesFromNuSpecs()
        {
            EnsurePackageFolderCreated(nugetsFolderFullPath);
            EnsurePackageFolderCreated(chocosFolderFullPath);

            var nuSpec = Path.Combine(packagingFolderFullPath, "nuget", projectName + ".nuspec");
            var deployToNuGet = false;
            var deployToChocolatey = false;
            if (File.Exists(nuSpec))
            {
                deployToNuGet = true;

                CreatePackagesFromNuSpecFile(nuSpec, nugetsFolderFullPath);
            }

            var chocolateyFolder = Path.Combine(packagingFolderFullPath, "chocolatey");
            if (Directory.Exists(chocolateyFolder))
            {
                var nuSpecs = Directory.GetFiles(chocolateyFolder, projectName + ".*.nuspec");

                if (nuSpecs.Any())
                {
                    deployToChocolatey = true;

                    foreach (var chocolateyNuSpec in nuSpecs)
                    {
                        CreatePackagesFromNuSpecFile(chocolateyNuSpec, chocosFolderFullPath);
                    }
                }
            }

            if (!deployToNuGet && !deployToChocolatey)
            {
                log.LogError("No nuspec files found at '{0}' or '{1}'.", Path.Combine(packagingFolderFullPath, "nuget"), Path.Combine(packagingFolderFullPath, "chocolatey"));
            }
        }

        static void EnsurePackageFolderCreated(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        void CreatePackagesFromNuSpecFile(string nuSpec, string destinationFolderFullPath)
        {
            var packageBuilder = new PackageBuilder();
            using (var stream = File.OpenRead(nuSpec))
            {
                var manifest = Manifest.ReadFrom(stream, this, false);
                packageBuilder.Populate(manifest.Metadata);

                if (manifest.Files != null)
                    packageBuilder.PopulateFiles("", manifest.Files);
            }

            SavePackage(packageBuilder, destinationFolderFullPath, ".nupkg", "Package created -> {0}");
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

        public dynamic GetPropertyValue(string propertyName)
        {
            Func<string> assigner;
            if(!propertyAssignments.TryGetValue(propertyName, out assigner))
                assigner = () => null;
            return assigner();
        }
    }
}
