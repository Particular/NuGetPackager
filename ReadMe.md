# NuGetPackager
---------------

A tool to generate the NuGet package based on conventions of our projects.

Installed via NuGet.

    PM> Install-Package NuGetPackager

## Properties which are supported
* `version`
* `authors` - Returns the authors which apply to all packages
* `owners` - Returns the owners which apply to all packages
* `licenseUrl` - Returns the licenseUrl which applies to all packages
* `projectUrl`- Returns the projectUrl which applies to all packages
* `iconUrl` - Returns the iconUrl to the particular icon on S3
* `requireLicenseAcceptance` - Returns always `true`
* `copyright` - Returns an always up to date copyright

### Sample nuspec
```
<package>
	...
    <version>$version$</version>
    <authors>$authors$</authors>
    <owners>$owners$</owners>
    <licenseUrl>$licenseUrl$</licenseUrl>
    <projectUrl>$projectUrl$</projectUrl>
    <iconUrl>$iconUrl$</iconUrl>
    <requireLicenseAcceptance>$requireLicenseAcceptance$</requireLicenseAcceptance>
    <copyright>$copyright$</copyright>
  </metadata>
	...
</package>
```
	
## Conventions

The following conventions are used to build the package.

- GitVersion is used to determine the version
- Solution is in the path `<project>\src\solution.sln`
- NuSpec file is located at `<project>\packaging\nuget\<ProjectName>.nuspec`
- Creates package file at `<project>\nugets\`

You can specify a custom NuSpec filename, by adding a `<NuSpecFileName>` property inside your .csproj file, e.g.:     `<NuSpecFileName>MyCustomPackageName</NuSpecFileName>`