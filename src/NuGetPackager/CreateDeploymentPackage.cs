using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGetPackager;

public class CreateDeploymentPackage : Task
{
    [Required]
    public string ProductName { get; set; }

    [Required]
    public string Version { get; set; }

    [Required]
    public ITaskItem PackagesFolder { get; set; }

    [Required]
    public ITaskItem NuGetsFolder { get; set; }

    [Required]
    public ITaskItem ChocosFolder { get; set; }

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

    void InnerExecute()
    {
        Directory.CreateDirectory(DeployFolder.FullPath());

        var packageCreator = new DeploymentPackageCreator(NuGetsFolder.FullPath(), ChocosFolder.FullPath(),  DeployFolder.FullPath(), PackagesFolder.FullPath(), ProductName, Version, Log);
        packageCreator.CreateDeploymentPackages();
    }
}