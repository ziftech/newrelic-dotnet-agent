############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
  [Parameter(Mandatory=$True)]
  [string]$configuration,
  [string]$MyGetApiKey = "",
  [switch]$IncludeDownloadSite
)

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
$nugetPath = (Resolve-Path "$rootDirectory\build\Tools\nuget.exe").Path

& "$rootDirectory\build\generateBuildProperties.ps1" -outputPath "$rootDirectory\build\BuildArtifacts\_buildProperties"
$artifactBuilderCsproj = "$rootDirectory\build\ArtifactBuilder\ArtifactBuilder.csproj"

$packagesToBuild = @(
    "dotnet run --project '$artifactBuilderCsproj' AzureSiteExtension",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x64",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureWebSites $configuration x86",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentApi $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgentExtensions $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAzureCloudServices $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAgent $configuration",
    "dotnet run --project '$artifactBuilderCsproj' NugetAwsLambdaOpenTracer $configuration",
    "dotnet run --project '$artifactBuilderCsproj' ZipArchives $configuration",
    "dotnet run --project '$artifactBuilderCsproj' CoreInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' ScriptableInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' MsiInstaller $configuration",
    "dotnet run --project '$artifactBuilderCsproj' LinuxPackages $configuration"
)

foreach ($pkg in $packagesToBuild) {
    Write-Output "EXECUTING: $pkg"
    Invoke-Expression $pkg
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
    Write-Output "----------------------"
}

if ($IncludeDownloadSite) {
    #The download site should be built after the other artifacts are built, because it depends on the other artifacts
    dotnet run --project "$artifactBuilderCsproj" DownloadSite $configuration
}

if ($MyGetApiKey -ne "") {
    # Currently the only packages that are pushed to MyGet are the AWS Lambda SDK
    & $nugetPath push $rootDirectory\build\BuildArtifacts\NugetAwsLambdaOpenTracer\*.nupkg -ApiKey $MyGetApiKey -Source "https://www.myget.org/F/newrelic/api/v3/index.json"
}
