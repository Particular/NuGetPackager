using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGetPackager;

public class CreatePackages : Task
{
    [Required]
    public string ProjectName { get; set; }

    [Required]
    public string Version { get; set; }

    [Required]
    public ITaskItem PackagingFolder { get; set; }

    [Required]
    public ITaskItem NuGetsFolder { get; set; }

    [Required]
    public ITaskItem ChocosFolder { get; set; }

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
        Directory.CreateDirectory(NuGetsFolder.FullPath());
        Directory.CreateDirectory(ChocosFolder.FullPath());

        var packageCreator = new PackageCreator(PackagingFolder.FullPath(), NuGetsFolder.FullPath(), ChocosFolder.FullPath(), ProjectName, Version, Log);
        packageCreator.CreatePackagesFromNuSpecs();
    }
}