# Smoke test for stage 11 (loans and returns).
# Works in Windows PowerShell 5.1 and PowerShell 7. Uses HttpClient directly, so 4xx/5xx never throw.
# Kept ASCII-only on purpose: Windows PowerShell 5.1 reads a .ps1 without BOM as ANSI.
#
# WARNING: drops the development database first.

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BaseUrl = 'http://localhost:5150'

# Demo users and password from src/Library.Infrastructure/Persistence/DbInitializer.cs
$Password = 'Demo123!'
$LibrarianEmail = 'librarian@library.local'
$MemberAEmail = 'member1@library.local'
$MemberBEmail = 'member2@library.local'

Add-Type -AssemblyName System.Net.Http
$Client = New-Object System.Net.Http.HttpClient
$Client.Timeout = [TimeSpan]::FromSeconds(30)

$Results = New-Object System.Collections.Generic.List[object]
$Failures = New-Object System.Collections.Generic.List[object]

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body
    )

    $uri = if ($Path -match '^https?://') { $Path } else { $BaseUrl + $Path }
    $request = New-Object System.Net.Http.HttpRequestMessage((New-Object System.Net.Http.HttpMethod($Method)), $uri)
    if ($Token) {
        $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $Token)
    }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $request.Content = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, 'application/json')
    }

    $response = $Client.SendAsync($request).GetAwaiter().GetResult()
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    $parsed = $null
    if ($text) {
        try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null }
    }

    $location = $null
    if ($response.Headers.Location) { $location = $response.Headers.Location.ToString() }

    [pscustomobject]@{
        Status   = [int]$response.StatusCode
        Text     = $text
        Json     = $parsed
        Location = $location
    }
}

function Add-Check {
    param(
        [string]$Name,
        $Expected,
        $Actual,
        [string]$Detail
    )

    $pass = "$Expected" -eq "$Actual"
    $Results.Add([pscustomobject]@{
        Check    = $Name
        Expected = "$Expected"
        Actual   = "$Actual"
        Result   = if ($pass) { 'PASS' } else { 'FAIL' }
    })
    if (-not $pass) {
        $Failures.Add([pscustomobject]@{ Check = $Name; Detail = $Detail })
    }
}

function Add-StatusCheck {
    param([string]$Name, [int]$Expected, $Response)
    Add-Check -Name $Name -Expected $Expected -Actual $Response.Status -Detail $Response.Text
}

function Get-Token {
    param([string]$Email)
    $r = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $Email; password = $Password }
    if ($r.Status -ne 200) { throw "Login failed for $Email ($($r.Status)): $($r.Text)" }
    $r.Json.token
}

function Get-AvailableCopyIds {
    param([int]$BookId, [string]$Token)
    $r = Invoke-Api -Method GET -Path "/api/books/$BookId/copies" -Token $Token
    @($r.Json | Where-Object { $_.status -eq 'Available' } | ForEach-Object { [int]$_.id })
}

# Callers wrap the result in @(): PowerShell unrolls a one-item array, and in 5.1 a single
# PSCustomObject has no .Count.
function Find-Loan {
    param($Paged, [int]$LoanId)
    @($Paged.items | Where-Object { [int]$_.id -eq $LoanId })
}

function Test-ServerUp {
    try {
        $r = $Client.GetAsync("$BaseUrl/api/membership-plans").GetAwaiter().GetResult()
        return ([int]$r.StatusCode -eq 200)
    } catch {
        return $false
    }
}

$server = $null
$serverOut = Join-Path $env:TEMP 'smoke-stage11-server.out.log'
$serverErr = Join-Path $env:TEMP 'smoke-stage11-server.err.log'

Push-Location $RepoRoot
try {
    if (Test-ServerUp) { throw "Something is already listening on $BaseUrl. Stop it first." }

    Write-Host '== Dropping the database'
    & dotnet ef database drop --force -p src/Library.Infrastructure -s src/Library.API
    if ($LASTEXITCODE -ne 0) { throw 'dotnet ef database drop failed.' }

    Write-Host '== Building'
    & dotnet build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    Write-Host '== Starting the API'
    $server = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run', '--project', 'src/Library.API', '--launch-profile', 'http', '--no-build' `
        -RedirectStandardOutput $serverOut -RedirectStandardError $serverErr `
        -PassThru -WindowStyle Hidden

    # Startup applies migrations and seeds users, so give it time.
    $deadline = (Get-Date).AddSeconds(120)
    while (-not (Test-ServerUp)) {
        if ($server.HasExited) { throw "The API exited during startup. See $serverOut and $serverErr." }
        if ((Get-Date) -gt $deadline) { throw "The API did not respond within 120 seconds. See $serverOut." }
        Start-Sleep -Milliseconds 500
    }
    Write-Host '== API is up'

    # 1) Logins
    $r = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $LibrarianEmail; password = $Password }
    Add-StatusCheck '1. Librarian login' 200 $r
    $libToken = $r.Json.token
    $aToken = Get-Token $MemberAEmail
    $bToken = Get-Token $MemberBEmail

    # 2) Librarian creates a book and a copy of it
    $r = Invoke-Api -Method POST -Path '/api/books' -Token $libToken -Body @{
        title = 'Smoke test book'; author = 'Smoke tester'; publicationYear = 2020; categoryIds = @(1)
    }
    Add-StatusCheck '2a. Librarian POST /api/books' 201 $r
    $newBookId = [int]$r.Json.id

    $r = Invoke-Api -Method POST -Path "/api/books/$newBookId/copies" -Token $libToken
    Add-StatusCheck '2b. Librarian POST copy for the new book' 201 $r
    $newCopyId = [int]$r.Json.id

    # 3) Member A borrows an available copy of seeded book 1
    $copyId = (Get-AvailableCopyIds -BookId 1 -Token $aToken)[0]
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $aToken -Body @{ copyId = $copyId }
    Add-StatusCheck "3a. Member A borrows copy $copyId" 201 $r
    $loanId = [int]$r.Json.id
    $location = $r.Location
    $locationPath = if ($location) { ([Uri]$location).AbsolutePath } else { '' }
    Add-Check '3b. Location header = /api/loans/{id}' "/api/loans/$loanId" $locationPath "Location: $location"

    # 4) GET the Location
    $r = Invoke-Api -Method GET -Path $location -Token $aToken
    Add-StatusCheck '4. Member A GET Location' 200 $r

    # 5) Same copy again
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $aToken -Body @{ copyId = $copyId }
    Add-StatusCheck '5. Member A borrows the same copy again' 409 $r

    # 6) Member B on member A's loan
    $r = Invoke-Api -Method PATCH -Path "/api/loans/$loanId" -Token $bToken
    Add-StatusCheck "6a. Member B PATCH loan $loanId" 403 $r
    $r = Invoke-Api -Method GET -Path "/api/loans/$loanId" -Token $bToken
    Add-StatusCheck "6b. Member B GET loan $loanId" 403 $r
    $r = Invoke-Api -Method GET -Path '/api/loans' -Token $bToken
    Add-StatusCheck '6c. Member B GET /api/loans' 403 $r

    # 7) Member A returns the loan
    $r = Invoke-Api -Method PATCH -Path "/api/loans/$loanId" -Token $aToken
    Add-StatusCheck "7a. Member A PATCH loan $loanId" 204 $r
    $r = Invoke-Api -Method PATCH -Path "/api/loans/$loanId" -Token $aToken
    Add-StatusCheck "7b. Member A PATCH loan $loanId again" 409 $r
    $r = Invoke-Api -Method GET -Path '/api/loans/me?PageSize=50' -Token $aToken
    Add-StatusCheck '7c. Member A GET /api/loans/me' 200 $r
    $mine = @(Find-Loan $r.Json $loanId)
    $returnedSet = ($mine.Count -eq 1) -and ($null -ne $mine[0].returnedAtUtc)
    Add-Check '7d. /me contains the loan with returnedAtUtc' $true $returnedSet $r.Text

    # 8) Retiring a loaned copy
    $copyX = (Get-AvailableCopyIds -BookId 1 -Token $aToken | Where-Object { $_ -ne $copyId })[0]
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $aToken -Body @{ copyId = $copyX }
    Add-StatusCheck "8a. Member A borrows copy X=$copyX" 201 $r
    $loanX = [int]$r.Json.id
    $r = Invoke-Api -Method DELETE -Path "/api/copies/$copyX" -Token $libToken
    Add-StatusCheck '8b. Librarian DELETE loaned copy X' 409 $r
    $r = Invoke-Api -Method PATCH -Path "/api/loans/$loanX" -Token $aToken
    Add-StatusCheck '8c. Member A returns X' 204 $r
    $r = Invoke-Api -Method DELETE -Path "/api/copies/$copyX" -Token $libToken
    Add-StatusCheck '8d. Librarian DELETE returned copy X' 204 $r
    $r = Invoke-Api -Method GET -Path '/api/loans?PageSize=50' -Token $libToken
    Add-StatusCheck '8e. Librarian GET /api/loans' 200 $r
    $historyKept = @(Find-Loan $r.Json $loanX).Count -eq 1
    Add-Check '8f. Old loan of retired X still listed' $true $historyKept $r.Text

    # 9) Filters, and the librarian cannot borrow. At this point both loans are returned.
    $r = Invoke-Api -Method GET -Path '/api/loans?ActiveOnly=true&PageSize=50' -Token $libToken
    Add-StatusCheck '9a. Librarian GET ?ActiveOnly=true' 200 $r
    $activeOk = ([int]$r.Json.totalCount -eq 0) -and (@($r.Json.items | Where-Object { $null -ne $_.returnedAtUtc }).Count -eq 0)
    Add-Check '9b. ActiveOnly: no returned loans (expect 0)' $true $activeOk $r.Text

    $r = Invoke-Api -Method GET -Path '/api/loans?ReturnedOnly=true&PageSize=50' -Token $libToken
    Add-StatusCheck '9c. Librarian GET ?ReturnedOnly=true' 200 $r
    $returnedItems = @($r.Json.items)
    $returnedOk = ([int]$r.Json.totalCount -eq 2) -and
        (@($returnedItems | Where-Object { $null -eq $_.returnedAtUtc }).Count -eq 0) -and
        (@(Find-Loan $r.Json $loanId).Count -eq 1) -and (@(Find-Loan $r.Json $loanX).Count -eq 1)
    Add-Check '9d. ReturnedOnly: exactly the 2 returned loans' $true $returnedOk $r.Text

    $libCopy = (Get-AvailableCopyIds -BookId 2 -Token $libToken)[0]
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $libToken -Body @{ copyId = $libCopy }
    Add-StatusCheck '9e. Librarian POST /api/loans' 403 $r

    # 10) A copy of a deleted book
    $r = Invoke-Api -Method DELETE -Path "/api/books/$newBookId" -Token $libToken
    Add-StatusCheck "10a. Librarian DELETE book $newBookId" 204 $r
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $aToken -Body @{ copyId = $newCopyId }
    Add-StatusCheck "10b. Member A borrows copy $newCopyId of deleted book" 404 $r

    # 11) No token
    $r = Invoke-Api -Method POST -Path '/api/loans' -Body @{ copyId = $copyId }
    Add-StatusCheck '11. POST /api/loans without a token' 401 $r
}
catch {
    Add-Check 'Script error' 'none' 'exception' $_.Exception.Message
}
finally {
    if ($server -and -not $server.HasExited) {
        Write-Host '== Stopping the API'
        # dotnet run starts Library.API.exe as a child process, so kill the whole tree.
        & taskkill /PID $server.Id /T /F | Out-Null
    }
    $Client.Dispose()
    Pop-Location
}

$Results | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Host

$passed = @($Results | Where-Object { $_.Result -eq 'PASS' }).Count
Write-Host "$passed / $($Results.Count) passed"

if ($Failures.Count -gt 0) {
    Write-Host ''
    Write-Host '== Failure details'
    foreach ($f in $Failures) {
        Write-Host "-- $($f.Check)"
        Write-Host $f.Detail
    }
    exit 1
}
