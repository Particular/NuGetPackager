#ensure a powershell error sends exitcode to Octopus
trap { 
    Write-Host $_
    Exit 1
}

# Passed from Octopus  
$requiredvariables = "ghusername", "ghpassword", "{{nugetkey}}" {{extravariables}}
$requiredvariables | % {
    if (!(Test-Path "variable:$_")) {
        throw "Variable $_ has not been set in Octopus config" 
    }
}

#content folder in nuget package contains files to upload
push-location .\content

#rename .zip files back to .nupkg
Get-ChildItem -Path ".\*" -Include "*.nzip" | Rename-Item -NewName { $_.BaseName }

Write-Host "{{logmessage}}"

& "..\tools\ReleaseNotesCompiler.CLI.exe" {{releasecommand}} -u $ghusername -p $ghpassword -o "Particular" -r "{{projectname}}" -m "{{version}}"

$files = Get-ChildItem -Path ".\*" -Include "*.nupkg"
foreach ($file in $files) { 
    $fileName =  $file.Name

    & "..\tools\NuGet.exe" push $fileName ${{nugetkey}} {{nugetsource}}
	{{extrapush}}
}
