# NuGetPackager
---------------

A tool to generate the NuGet package based on conventions of our projects.

Installed via NuGet.

    PM> Install-Package NuGetPackager

## Conventions

The following conventions are used to build the package.

- GitVersion is used to determine the version
- Solution is in the path `<project>\src\solution.sln`
- NuSpec file is located at `<project>\packaging\nuget\<ProjectName>.nuspec`
- Creates package file at `<project>\nugets\`