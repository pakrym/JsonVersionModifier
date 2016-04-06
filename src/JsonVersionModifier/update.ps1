git checkout .

if (test-path test)
{
    $projects = (Get-ChildItem -Path test -Filter project.json -Recurse | %{ $_.FullName })
}
if (test-path samples)
{
    $projects = $projects + (Get-ChildItem -Path samples -Filter project.json -Recurse | %{ $_.FullName })
}
if (test-path tools)
{
    $projects = $projects + (Get-ChildItem -Path tools -Filter project.json -Recurse | %{ $_.FullName })
}

$projects| Write-Host
dotnet run -p $PSScriptRoot -- $projects

.\build