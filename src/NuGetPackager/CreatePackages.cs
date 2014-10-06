using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet;

public class CreatePackages : Task, IPropertyProvider
{
    private bool deployToNuGet;
    private bool deployToChocolatey;

    public bool DeployContentInRelease { get; set; }

    [Required]
    public string ProjectName { get; set; }

    [Required]
    public string Version { get; set; }

    [Required]
    public ITaskItem PackagesFolder { get; set; }

    [Required]
    public ITaskItem PackagingFolder { get; set; }

    [Required]
    public ITaskItem NuGetsFolder { get; set; }

    [Required]
    public ITaskItem DeployFolder { get; set; }

    public override bool Execute()
    {
        try
        {
            InnerExecute();
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException vex)
        {
            foreach (var line in Regex.Split(vex.Message, Environment.NewLine))
                Log.LogError(line);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true, true, null);
        }

        return !Log.HasLoggedErrors;
    }

    private void InnerExecute()
    {
        Directory.CreateDirectory(NuGetsFolder.FullPath());
        Directory.CreateDirectory(DeployFolder.FullPath());

        CreatePackagesFromNuSpecs();

        CreateDeploymentPackages();
    }

    void CreatePackagesFromNuSpecs()
    {
        var nuSpec = Path.Combine(PackagingFolder.FullPath(), "nuget", ProjectName + ".nuspec");

        if (File.Exists(nuSpec))
        {
            deployToNuGet = true;

            CreatePackagesFromNuSpecFile(nuSpec);
        }

        var chocolateyFolder = Path.Combine(PackagingFolder.FullPath(), "chocolatey");
        if (Directory.Exists(chocolateyFolder))
        {
            var nuSpecs = Directory.GetFiles(chocolateyFolder, ProjectName + ".*.nuspec");

            if (nuSpecs.Any())
            {
                deployToChocolatey = true;

                foreach (var chocolateyNuSpec in nuSpecs)
                {
                    CreatePackagesFromNuSpecFile(chocolateyNuSpec);
                }
            }
        }

        if (!deployToNuGet && !deployToChocolatey)
        {
            Log.LogError("No nuspec files found at '{0}' or '{1}'.", Path.Combine(PackagingFolder.FullPath(), "nuget"), Path.Combine(PackagingFolder.FullPath(), "chocolatey"));
        }
    }

    void CreatePackagesFromNuSpecFile(string nuSpec)
    {
        var packageBuilder = new PackageBuilder();
        using (var stream = File.OpenRead(nuSpec))
        {
            var manifest = Manifest.ReadFrom(stream, this, false);
            packageBuilder.Populate(manifest.Metadata);
            packageBuilder.PopulateFiles("", manifest.Files);
        }

        SavePackage(packageBuilder, NuGetsFolder, ".nupkg", "Package created -> {0}");
    }

    void CreateDeploymentPackages()
    {
        foreach (var nupkg in Directory.GetFiles(NuGetsFolder.FullPath(), "*.nupkg"))
        {
            File.Copy(nupkg, nupkg + ".nzip", true);
        }

        try
        {
            // Build Staging
            var packageBuilder = new PackageBuilder
            {
                Id = ProjectName + ".Staging",
                Description = "Octopus package for staging " + ProjectName + ".",
                Version = SemanticVersion.Parse(Version)
            };
            packageBuilder.Authors.Add("Particular Software");

            if (deployToChocolatey && deployToNuGet)
                AddDeployScriptForStagingBoth(packageBuilder);
            else if (deployToNuGet)
                AddDeployScriptForStagingNuGet(packageBuilder);
            else if (deployToChocolatey)
                AddDeployScriptForStagingChocolatey(packageBuilder);

            AddTools(packageBuilder);
            AddContent(packageBuilder);

            SavePackage(packageBuilder, DeployFolder, ".nupkg", "Package created -> {0}");

            // Build Release
            packageBuilder = new PackageBuilder
            {
                Id = ProjectName + ".Release",
                Description = "Octopus package for release " + ProjectName + ".",
                Version = SemanticVersion.Parse(Version)
            };
            packageBuilder.Authors.Add("Particular Software");

            if (deployToChocolatey && deployToNuGet)
                AddDeployScriptForReleaseBoth(packageBuilder);
            else if (deployToNuGet)
                AddDeployScriptForReleaseNuGet(packageBuilder);
            else if (deployToChocolatey)
                AddDeployScriptForReleaseChocolatey(packageBuilder);

            AddTools(packageBuilder);

            if (DeployContentInRelease)
                AddContent(packageBuilder);

            SavePackage(packageBuilder, DeployFolder, ".nupkg", "Package created -> {0}");
        }
        finally
        {
            // Clean up
            foreach (var nupkg in Directory.GetFiles(NuGetsFolder.FullPath(), "*.nzip"))
            {
                File.Delete(nupkg);
            }
        }
    }

    void AddContent(PackageBuilder packageBuilder)
    {
        foreach (var nupkg in Directory.GetFiles(NuGetsFolder.FullPath(), "*.nzip"))
        {
            packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = nupkg, Target = "content" } });
        }
    }

    void AddTools(PackageBuilder packageBuilder)
    {
        var nugetCLI = Directory.GetFiles(PackagesFolder.FullPath(), "NuGet.exe", SearchOption.AllDirectories).First();
        var releaseNotesCompiler = Directory.GetFiles(PackagesFolder.FullPath(), "ReleaseNotesCompiler.CLI.exe", SearchOption.AllDirectories).First();

        packageBuilder.PopulateFiles("", new[] {
            new ManifestFile { Source = nugetCLI, Target = "tools" },
            new ManifestFile { Source = releaseNotesCompiler, Target = "tools" }
        });
    }

    void AddDeployScriptForStagingNuGet(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "mygetkey");
            outscript = outscript.Replace("{{nugetsource}}", "-Source https://www.myget.org/F/particular/api/v2/package");
            outscript = outscript.Replace("{{releasecommand}}", "create");
            outscript = outscript.Replace("{{logmessage}}", "Creating release for milestone " + Version + " ...");

            return outscript;
        });
    }

    void AddDeployScriptForStagingChocolatey(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "mygetkey");
            outscript = outscript.Replace("{{nugetsource}}", "-Source https://www.myget.org/F/particular-chocolatey/api/v2/package");
            outscript = outscript.Replace("{{releasecommand}}", "create");
            outscript = outscript.Replace("{{logmessage}}", "Creating release for milestone " + Version + " ...");

            return outscript;
        });
    }

    void AddDeployScriptForStagingBoth(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "mygetkey");
            outscript = outscript.Replace("{{nugetsource}}", "-Source https://www.myget.org/F/particular-chocolatey/api/v2/package");
            outscript = outscript.Replace("{{releasecommand}}", "create");
            outscript = outscript.Replace("{{logmessage}}", "Creating release for milestone " + Version + " ...");
            outscript = outscript.Replace("{{extrapush}}", "& \"..\\tools\\NuGet.exe\" push $fileName $mygetkey -Source https://www.myget.org/F/particular-chocolatey/api/v2/package");

            return outscript;
        });
    }

    void AddDeployScriptForReleaseNuGet(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "nugetkey");
            outscript = outscript.Replace("{{nugetsource}}", "");
            outscript = outscript.Replace("{{releasecommand}}", "publish");
            outscript = outscript.Replace("{{logmessage}}", "Publishing release for milestone " + Version + " ...");

            return outscript;
        });
    }

    void AddDeployScriptForReleaseChocolatey(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "chocolateykey");
            outscript = outscript.Replace("{{nugetsource}}", "");
            outscript = outscript.Replace("{{releasecommand}}", "publish");
            outscript = outscript.Replace("{{logmessage}}", "Publishing release for milestone " + Version + " ...");

            return outscript;
        });
    }

    void AddDeployScriptForReleaseBoth(PackageBuilder packageBuilder)
    {
        AddDeployScript(packageBuilder, script =>
        {
            var outscript = script.Replace("{{nugetkey}}", "nugetKey");
            outscript = outscript.Replace("{{nugetsource}}", "");
            outscript = outscript.Replace("{{releasecommand}}", "publish");
            outscript = outscript.Replace("{{logmessage}}", "Publishing release for milestone " + Version + " ...");
            outscript = outscript.Replace("{{extravariables}}", ", \"chocolateykey\"");
            outscript = outscript.Replace("{{extrapush}}", "& \"..\\tools\\NuGet.exe\" push $fileName $chocolateykey -Source http://chocolatey.org/api/v2/package");

            return outscript;
        });
    }

    void AddDeployScript(PackageBuilder packageBuilder, Func<string, string> scriptTransform)
    {
        var deployFile = Path.Combine(Path.GetTempPath(), "Deploy.ps1");

        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NuGetPackager.Scripts.Deploy.ps1"))
        using (var reader = new StreamReader(resource))
        using (var file = new FileStream(deployFile, FileMode.Create, FileAccess.Write))
        using (var writer = new StreamWriter(file))
        {
            var script = reader.ReadToEnd();

            script = script.Replace("{{version}}", Version);
            script = script.Replace("{{projectname}}", ProjectName);

            script = scriptTransform(script);

            script = script.Replace("{{extravariables}}", "");
            script = script.Replace("{{extrapush}}", "");

            writer.Write(script);
        }

        packageBuilder.PopulateFiles("", new[] { new ManifestFile { Source = deployFile, Target = "Deploy.ps1" } });
    }

    void SavePackage(PackageBuilder packageBuilder, ITaskItem destinationFolder, string filenameSuffix, string logMessage)
    {
        var dir = destinationFolder.FullPath();

        var filename = Path.Combine(dir, packageBuilder.GetFullName()) + filenameSuffix;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var file = new FileStream(filename, FileMode.Create))
        {
            packageBuilder.Save(file);
        }

        Log.LogMessage(logMessage, filename);
    }

    public dynamic GetPropertyValue(string propertyName)
    {
        if (propertyName == "version")
            return Version;

        return null;
    }
}