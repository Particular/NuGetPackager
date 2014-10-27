# ensure a powershell error sends exitcode to Octopus
trap { 
    Write-Host $_
    Exit 1
}

# memorize the list of nuget and choco packages
$nugets = Get-ChildItem -Path ".\content\*" -Include "*.nzip"
$chocos = Get-ChildItem -Path ".\content\*" -Include "*.czip"

# passed from Octopus  
$requiredvariables = @("ghusername", "ghpassword")

if ($nugets -ne $null) {
	$requiredvariables += "nugetkey"
	$requiredvariables += "nugetsource"
}

if ($chocos -ne $null) {
	$requiredvariables += "chocolateykey"
	$requiredvariables += "chocolateysource"
}

# ensure all required variables are present
$requiredvariables | % {
    if (!(Test-Path "variable:$_")) {
        throw "Variable $_ has not been set in Octopus config" 
    }
}

Write-Host "About to $releasecommand GitHub release for version "

if ( -not (Test-Path '.\asset' -PathType Container) ) {
    & ".\tools\ReleaseNotesCompiler.CLI.exe" $releasecommand -u $ghusername -p $ghpassword -o "Particular" -r $OctopusParameters['Octopus.Project.Name'] -m $OctopusParameters['Octopus.Release.Number']
} else {
    $assets = Get-ChildItem .\asset

    & ".\tools\ReleaseNotesCompiler.CLI.exe" $releasecommand -u $ghusername -p $ghpassword -o "Particular" -r $OctopusParameters['Octopus.Project.Name'] -m $OctopusParameters['Octopus.Release.Number'] -a $assets[0].FullName
}

if ($LASTEXITCODE -ne 0) {
    throw "ReleaseNotesCompiler returned $LASTEXITCODE"
}

if ( -not (Test-Path '.\content' -PathType Container) ) {
    Exit 0
}

# content folder in nuget package contains files to upload
push-location .\content

# rename .nzip and .czip files back to .nupkg 
Get-ChildItem -Path ".\*" -Include @("*.nzip","*.czip") | Rename-Item -NewName { $_.BaseName }

# push nuget packages if any
foreach ($file in $nugets) { 
    $fileName =  $file.Name
    & "..\tools\NuGet.exe" push $fileName $nugetkey $nugetsource
}

# push choco packages if any
foreach ($file in $chocos) { 
    $fileName =  $file.Name
    & "..\tools\NuGet.exe" push $fileName $chocolateykey $chocolateysource
}