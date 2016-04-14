Function Work($dir, $match, $find, $replace)
{
    foreach ($sc in dir $dir -recurse -include $match | where { test-path $_.fullname -pathtype leaf} ) {
        select-string -path $sc -pattern $find -CaseSensitive
        if ((select-string -path $sc -pattern $find -CaseSensitive)) {
            $fileContent = get-content -Raw $sc -Encoding UTF8
            $fileContent = $fileContent -creplace $find, $replace
            $Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding($False)
            [IO.File]::WriteAllText($sc, $fileContent, $Utf8NoBomEncoding);
        }
    }
}

$HorizontalBoxChar = [string][char]9552
$VerticalBoxChar = [string][char]9553
$TopLeftBoxChar = [string][char]9556
$TopRightBoxChar = [string][char]9559
$BottomLeftBoxChar = [string][char]9562
$BottomRightBoxChar = [string][char]9565

Function CreateBoxText() {
    Begin {
        $lines = @()
    }
    Process {
        $maxLength = 0
        $lineCount = 0
        $_ -split "`r`n" | ForEach-Object {
            $lines += $_
            If ($lines[$lineCount].Length -gt $maxLength) {
                $maxLength = $lines[$lineCount].Length
            }
            $lineCount++
        }
        $maxLength += 10;
    }
    End {
        $TopLeftBoxChar + ($HorizontalBoxChar * ($maxLength + 2)) + $TopRightBoxChar
        For ($i = 0; $i -lt $lineCount; $i++) {
            $VerticalBoxChar + " " + $lines[$i] + (" " * ($maxLength - $lines[$i].Length + 1)) + $VerticalBoxChar
        }
        $BottomLeftBoxChar + ($HorizontalBoxChar * ($maxLength + 2)) + $BottomRightBoxChar
    }   
}

Function Write-Host-Loud($string)
{
    $string | CreateBoxText | Write-Host -ForegroundColor Green;
    #Write-Host -ForegroundColor Green "*************************************************"
    #Write-Host -ForegroundColor Green "*        $string"
    #Write-Host -ForegroundColor Green "*************************************************"
}

function ProcessRepo()
{
    if ($NOCHANGE -ne 1)
    {
        git checkout release
        git checkout .

        git clean -xdf .

        if (test-path test)
        {
            $projects = @(Get-ChildItem -Path test -Filter project.json -Recurse | %{ $_.FullName })
        }
        if (test-path samples)
        {
            $projects = $projects + @(Get-ChildItem -Path samples -Filter project.json -Recurse | %{ $_.FullName })
        }
        if (test-path tools)
        {
            $projects = $projects + @(Get-ChildItem -Path tools -Filter project.json -Recurse | %{ $_.FullName })
        }


        $projects| Write-Host
        foreach($p in $projects)
        {
            $dir = Split-path $p -Parent;
            Work $dir "*.cs" "NETSTANDARD\w+\d_\d" "NETCOREAPP1_0"
        }
        dotnet run -p $PSScriptRoot -- $projects/
    }
    Write-Host-Loud "Building"
    .\build
    if ($LastExitCode -eq 0)
    {
        Write-Host-Loud "SUCESS!"
        git diff
        Write-Host-Loud "PR?"
        $yes = Read-Host;
        if ($yes -eq "y")
        {
            git add .
            git-branch-chages pakrym/portable "Migrate tests, tools and samples to portable"
            C:\Users\pakrym\AppData\Local\GitHubCLI\bin\hub pull-request -F D:\portable.txt -b release
            Write-Host-Loud "Merge it to release?"
            $yes = Read-Host;
            if ($yes -eq "y")
            {
                git checkout release
                git rebase pakrym/portable
                git push

                Write-Host-Loud "Merge it to dev?"
                $yes = Read-Host;
                if ($yes -eq "y")
                {
                    git checkout dev
                    git merge release
                    git push
                }
            }
        }
    }
}

$repos = @(
                #"PlatformAbstractions",
                #"Common",
                #"JsonPatch",
                #"FileSystem",
                #"Configuration",
                #"DependencyInjection",
                #"EventNotification",
                #"Options",
                #"Logging",
                #"dotnet-watch", #<<< NPA
                #"HtmlAbstractions",
                "UserSecrets",
                #"DataProtection", #<<< NPA
                "HttpAbstractions",
                "Testing",
                "Microsoft.Data.Sqlite",
                #"Caching", #<<< NPA
                "Razor",
                #"RazorTooling", #<<< NPA
                "Hosting",
                #"EntityFramework",#<<< NPA
                "WebListener",
                "KestrelHttpServer",
                #"IISIntegration",#<<< NPA
                #"ServerTests", #<<< neds manual fix in HelloWorldTests:PublishTargetFramework and has issue with web config for portable/non-portable cases 
                "Session",
                "CORS",
                "Routing",
                "StaticFiles",
                "Diagnostics",
                "Security",
                "Antiforgery",
                "WebSockets",
                "Localization",
                "BasicMiddleware",
                "Proxy",
                "Mvc",
                "Identity",
                #"Scaffolding", #<<< NPA
                #"SignalR-Server", #<<< more issues
                "SignalR-SQLServer",
                "SignalR-Redis",
                "SignalR-ServiceBus",
                "BrowserLink",
                #"Entropy", #<<< Lots of samples issues needs to be manualy fixed
                "MusicStore"
);

foreach ($repo in $repos)
{
    if (-Not(test-path $repo))
    {
        Write-Host-Loud "Could not locate repo '$repo'."
        $clone = "y"
        #$clone = Read-Host "Clone?"
        if ($clone -eq "y")
        {
            git clone "git@github.com:aspnet/$repo"
        }
        else
        {
            $allReposPresent = $FALSE
        }
    }
    Write-Host-Loud "PROCESSING $repo"
    pushd $repo
    ProcessRepo;
    popd
    Read-Host
}

