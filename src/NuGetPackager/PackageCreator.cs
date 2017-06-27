namespace NuGetPackager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Utilities;
    using NuGet.Packaging;

    class PackageCreator
    {
        readonly Dictionary<string, Func<string>> propertyAssignments;
        readonly string packagingFolderFullPath;
        readonly string nugetsFolderFullPath;
        readonly string projectName;
        readonly TaskLoggingHelper log;

        public PackageCreator(string packagingFolderFullPath, string nugetsFolderFullPath, string projectName, string version, TaskLoggingHelper log)
        {
            this.packagingFolderFullPath = packagingFolderFullPath;
            this.nugetsFolderFullPath = nugetsFolderFullPath;
            this.projectName = projectName;
            this.log = log;

            propertyAssignments = new Dictionary<string, Func<string>>
            {
                { "version", () => version },
                { "authors", () => "NServiceBus Ltd" },
                { "owners", () => "NServiceBus Ltd" },
                { "projectUrl", () => "TBD" },
                { "licenseUrl", () => "http://particular.net/LicenseAgreement" },
                { "iconUrl", () => "http://s3.amazonaws.com/nuget.images/NServiceBus_32.png" },
                { "requireLicenseAcceptance", () => "true" },
                { "copyright", () => $"Copyright 2010-{DateTime.UtcNow.Year} NServiceBus. All rights reserved"},
                { "tags", () => "nservicebus servicebus cqrs publish subscribe" },
            };
        }

        public void CreatePackagesFromNuSpecs()
        {
            EnsurePackageFolderCreated(nugetsFolderFullPath);

            var nuSpec = Path.Combine(packagingFolderFullPath, "nuget", projectName + ".nuspec");
            if (File.Exists(nuSpec))
            {
                CreatePackagesFromNuSpecFile(nuSpec, nugetsFolderFullPath);
            }
            else
            {
                log.LogError("No nuspec file found in '{0}'.", Path.Combine(packagingFolderFullPath, "nuget"));
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
                var manifest = Manifest.ReadFrom(stream, p => GetPropertyValue(p), false);
                manifest.Metadata.SetProjectUrl($"https://docs.particular.net/nuget/{manifest.Metadata.Id}/");
                packageBuilder.Populate(manifest.Metadata);

                if (manifest.Files != null)
                    packageBuilder.PopulateFiles("", manifest.Files);
            }

            SavePackage(packageBuilder, destinationFolderFullPath, "Package created -> {0}");
        }

        void SavePackage(PackageBuilder packageBuilder, string destinationFolder, string logMessage)
        {
            var filename = Path.Combine(destinationFolder, $"{packageBuilder.Id}.{packageBuilder.Version}.nupkg");

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
