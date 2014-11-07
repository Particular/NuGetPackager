# ensure a powershell error sends exitcode to Octopus
trap { 
    Write-Host $_
    Exit 1
}

$Branch = "{{Branch}}"
$Version = "{{Version}}"
$Product = "{{Product}}"

# memorize the list of nuget and choco packages
$nugets = Get-ChildItem -Path ".\content\*" -Include "*.nzip"
$chocos = Get-ChildItem -Path ".\content\*" -Include "*.czip"

$tweet = (Test-Path "variable:enabletweets") -and ($enabletweets -eq "true")

# passed from Octopus  
$requiredvariables = @("ghusername", "ghpassword", "releasecommand")

if ($nugets -ne $null) {
	$requiredvariables += "nugetkey"
	$requiredvariables += "nugetsource"
}

if ($chocos -ne $null) {
	$requiredvariables += "chocolateykey"
	$requiredvariables += "chocolateysource"
}

if ($tweet) {
    $requiredvariables += @("twitter_key","twitter_secret","twitter_token","twitter_tokensecret")
}

# ensure all required variables are present
$requiredvariables | % {
    if (!(Test-Path "variable:$_")) {
        throw "Variable $_ has not been set in Octopus config" 
    }
}

Write-Host "About to $releasecommand GitHub release for version $Version"


if ($releasecommand -eq "create") {
    if ( -not (Test-Path '.\asset' -PathType Container) ) {
		& ".\tools\ReleaseNotesCompiler.CLI.exe" create -u $ghusername -p $ghpassword -o "Particular" -r $Product -m $Version -t $Branch
    } else {
        & ".\tools\ReleaseNotesCompiler.CLI.exe" create -u $ghusername -p $ghpassword -o "Particular" -r $Product -m $Version -t $Branch -a $assets[0].FullName
    }
} else {
    if ( -not (Test-Path '.\asset' -PathType Container) ) {
		& ".\tools\ReleaseNotesCompiler.CLI.exe" publish -u $ghusername -p $ghpassword -o "Particular" -r $Product -m $Version
    } else {
        & ".\tools\ReleaseNotesCompiler.CLI.exe" publish -u $ghusername -p $ghpassword -o "Particular" -r $Product -m $Version -a $assets[0].FullName
    }
    if ($tweet) {
	    & ".\tools\ConsoleTweet.exe" update -m "We've just released $Product v${Version}. For more details: https://github.com/Particular/{$Product}/releases/${Version}" --key $twitter_key --secret $twitter_secret --token $twitter_token --token_secret $twitter_tokensecret 
    }
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
    $fileName =  $file.BaseName
    & "..\tools\NuGet.exe" push $fileName $nugetkey -Source $nugetsource
}

# push choco packages if any
foreach ($file in $chocos) { 
    $fileName =  $file.BaseName
    & "..\tools\NuGet.exe" push $fileName $chocolateykey -Source $chocolateysource
}