#Requires -Version 7
<#
בדיקת עשן לשלב 15: לוגים עם NLog.

מה נבדק (כל בקשה נשלחת עם X-Correlation-ID ייחודי, smoke15-<guid>, ואחר כך מחפשים אותו בקובץ הלוג):
  א. GET /api/books עם query string שיש בו ערך ייחודי: 200; בלוג יש started ו-responded עם המזהה,
     והערך הייחודי לא מופיע בלוג בכלל.
  ב. כל שורות הבקשות בפורמט תאריך|רמה|logger|מזהה|הודעה.
  ג. GET /api/books/{BookId}: יש שורת DEBUG עם המזהה. עם -ExpectNoDebug: אין שום שורת DEBUG עם המזהה.
  ד. התחברות של משתמשת הדמו: 200, ושורת "logged in" עם המזהה; הסיסמה וה-token לא מופיעים בלוג.
  ה. בקשה מאומתת (GET /api/loans/me): started ו-responded עם המזהה; ה-token לא מופיע בלוג.
  ו. נתיב לא קיים: 404, ושורת responded 404 עם המזהה.
  ז. רק עם -ThrowPath (למשל /api/diagnostics/throw): 500, ושורת ERROR עם המזהה ועם ה-exception.

מה הסקריפט יוצר בבסיס הנתונים: כלום. כל הבקשות הן GET, חוץ מההתחברות.
הסקריפט לא מדפיס את הסיסמה ואת ה-token.

דרישות:
  - השרת כבר רץ (למשל dotnet run --project src/Library.API --launch-profile http).
    הסקריפט לא בונה, לא מריץ את השרת ולא מוחק את בסיס הנתונים.
  - משתמשת הדמו מ-DbInitializer קיימת (member1@library.local).
  - הספר -BookId קיים ולא מחוק.
  - -LogPath מצביע על הקובץ שהשרת כותב אליו. ברירת המחדל היא הקובץ של היום (UTC) תחת
    src/Library.API/bin/Debug/net8.0/logs, יחסית לשורש ה-repo.

קודי יציאה: 0 הכול עבר; 1 בדיקה נכשלה; 2 הבדיקה המקדימה נכשלה.

דוגמאות:
  pwsh -File scripts/smoke-stage15.ps1
  pwsh -File scripts/smoke-stage15.ps1 -ThrowPath /api/diagnostics/throw
  pwsh -File scripts/smoke-stage15.ps1 -ExpectNoDebug        (מול שרת שרץ ב-Production)
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5150',
    [string]$LogPath,
    [ValidateRange(1, [int]::MaxValue)][int]$BookId = 1,
    [string]$ThrowPath,
    [switch]$ExpectNoDebug
)

$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')

# The default is relative to the repository root, not to the current folder.
$RepoRoot = Split-Path -Parent $PSScriptRoot
if ($LogPath) {
    $LogPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LogPath)
} else {
    $LogFileName = 'library-{0}.log' -f [DateTime]::UtcNow.ToString('yyyy-MM-dd')
    $LogPath = Join-Path $RepoRoot 'src/Library.API/bin/Debug/net8.0/logs' $LogFileName
}

# Demo user and password from src/Library.Infrastructure/Persistence/DbInitializer.cs
$Password = 'Demo123!'
$MemberEmail = 'member1@library.local'

# From src/Library.API/Middleware and src/Library.API/nlog.config
$CorrelationHeader = 'X-Correlation-ID'
# date (UTC)|LEVEL|logger|correlation id|message
$LinePattern = '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{4}\|(TRACE|DEBUG|INFO|WARN|ERROR|FATAL)\|[^|]+\|(?<id>[^|]+)\|.+'

# NLog writes synchronously; this only covers the time between the response and the last line of the request.
$LogWaitMs = 1500

$Results = [System.Collections.Generic.List[object]]::new()

#region HTTP helpers

# Every request gets its own correlation id, returned as SentId so its lines can be found in the log.
function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body)

    $sentId = 'smoke15-' + [Guid]::NewGuid().ToString()
    $headers = @{ Accept = 'application/json'; $CorrelationHeader = $sentId }
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }

    $params = @{
        Method             = $Method
        Uri                = if ($Path -match '^https?://') { $Path } else { $BaseUrl + $Path }
        Headers            = $headers
        SkipHttpErrorCheck = $true
        TimeoutSec         = 30
    }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Depth 10 -Compress
        $params.ContentType = 'application/json; charset=utf-8'
    }

    $response = Invoke-WebRequest @params

    $text = [System.Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
    $parsed = $null
    if ($text) {
        try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null }
    }

    [pscustomobject]@{
        Status = [int]$response.StatusCode
        Json   = $parsed
        SentId = $sentId
    }
}

#endregion

#region Log helpers

# Opened with FileShare.ReadWrite, so the file can be read while the server still has it open for writing.
function Read-LogText {
    $stream = [System.IO.FileStream]::new($LogPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read,
        [System.IO.FileShare]'ReadWrite, Delete')
    try {
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8)
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally {
        $stream.Dispose()
    }
}

# The lines written for one request: its id is the fourth field. Continuation lines of a stack trace have no id.
function Get-RequestLines {
    param([string]$CorrelationId)
    @($script:LogLines | Where-Object { $_.Contains("|$CorrelationId|") })
}

function Test-HasLine {
    param([string[]]$Lines, [string]$Level, [string]$Pattern)
    @($Lines | Where-Object { $_.Contains("|$Level|") -and $_ -match $Pattern }).Count -gt 0
}

#endregion

#region Result helpers

function Add-Result {
    param([string]$Name, [bool]$Pass, [string]$Statuses, [string]$Detail)

    $Results.Add([pscustomobject]@{ Name = $Name; Pass = $Pass; Statuses = $Statuses; Detail = $Detail })
    $tag = if ($Pass) { 'PASS' } else { 'FAIL' }
    $color = if ($Pass) { 'Green' } else { 'Red' }
    Write-Host ("[{0}] {1}  ->  {2}" -f $tag, $Name, $Statuses) -ForegroundColor $color
    if (-not $Pass -and $Detail) { Write-Host "       $Detail" -ForegroundColor DarkGray }
}

# Adds one result from a list of problems (empty means it passed).
function Add-IssuesResult {
    param([string]$Name, $Response, [System.Collections.Generic.List[string]]$Issues)
    $statuses = "HTTP $($Response.Status), $CorrelationHeader=$($Response.SentId)"
    Add-Result -Name $Name -Pass ($Issues.Count -eq 0) -Statuses $statuses -Detail ($Issues -join '; ')
}

function New-Issues { , [System.Collections.Generic.List[string]]::new() }

# started and responded lines of CorrelationIdMiddleware, with the path only.
function Add-StartedRespondedIssues {
    param([System.Collections.Generic.List[string]]$Issues, [string[]]$Lines, [string]$Path, [int]$Status)
    $escaped = [regex]::Escape($Path)
    if (-not (Test-HasLine $Lines 'INFO' "\|HTTP GET $escaped started$")) { $Issues.Add("no 'HTTP GET $Path started' line") }
    if (-not (Test-HasLine $Lines 'INFO' "\|HTTP GET $escaped responded $Status in ")) { $Issues.Add("no 'HTTP GET $Path responded $Status' line") }
}

#endregion

# Hebrew server messages may be printed in failure details.
$originalOutputEncoding = [Console]::OutputEncoding
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$exitCode = 0
$preflightDone = $false

try {
    #region Preflight (no changes)

    Write-Host "== Preflight against $BaseUrl"
    Write-Host "   Log file: $LogPath"
    try {
        $r = Invoke-Api -Method GET -Path '/api/membership-plans'
    } catch {
        throw [System.InvalidOperationException]::new("The server at $BaseUrl is not reachable ($($_.Exception.Message)). Start it first: dotnet run --project src/Library.API --launch-profile http")
    }
    if ($r.Status -ne 200) {
        throw "GET /api/membership-plans (no token) returned HTTP $($r.Status)."
    }

    # Test ד uses this request. The password is never printed, also not in this message.
    $login = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $MemberEmail; password = $Password }
    if ($login.Status -ne 200 -or -not $login.Json -or -not $login.Json.token) {
        throw "Login failed for demo user $MemberEmail (HTTP $($login.Status)). Make sure the demo users from DbInitializer exist."
    }
    $token = [string]$login.Json.token
    $userId = $login.Json.userId

    Start-Sleep -Milliseconds $LogWaitMs
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        throw "Log file '$LogPath' does not exist. Check that the server runs from bin/Debug/net8.0 and that nlog.config was copied there, or pass -LogPath."
    }

    Write-Host '== Preflight OK'
    $preflightDone = $true

    #endregion

    #region Requests

    Write-Host '== Sending requests'

    # A value that cannot appear in the log by chance; it must not appear there at all.
    $queryValue = 'q15' + [Guid]::NewGuid().ToString('N')
    $search = Invoke-Api -Method GET -Path "/api/books?Title=$queryValue&PageSize=1" -Token $token
    $book = Invoke-Api -Method GET -Path "/api/books/$BookId" -Token $token
    $mine = Invoke-Api -Method GET -Path '/api/loans/me?PageSize=1' -Token $token
    $missingPath = '/api/no-such-route-smoke15'
    $missing = Invoke-Api -Method GET -Path $missingPath -Token $token
    $throw = if ($ThrowPath) { Invoke-Api -Method GET -Path $ThrowPath -Token $token } else { $null }

    Start-Sleep -Milliseconds $LogWaitMs
    $logText = Read-LogText
    $script:LogLines = $logText -split "`r?`n"

    Write-Host "== Read $($script:LogLines.Count) lines from the log"

    #endregion

    #region א. query string is not logged

    $lines = Get-RequestLines $search.SentId
    $issues = New-Issues
    if ($search.Status -ne 200) { $issues.Add("status $($search.Status), expected 200") }
    Add-StartedRespondedIssues $issues $lines '/api/books' 200
    if ($logText.Contains($queryValue)) {
        $issues.Add("the query string value '$queryValue' appears in the log")
    }
    Add-IssuesResult -Name 'א. GET /api/books?Title=<unique>: started/responded, no query string' -Response $search -Issues $issues

    #endregion

    #region ב. line format

    $issues = New-Issues
    $requests = @($login, $search, $book, $mine, $missing)
    $checked = 0
    foreach ($req in $requests) {
        foreach ($line in (Get-RequestLines $req.SentId)) {
            $checked++
            if ($line -notmatch $LinePattern -or $Matches['id'] -cne $req.SentId) {
                $issues.Add("bad line: $line")
            }
        }
    }
    if ($checked -eq 0) { $issues.Add('no line of these requests was found') }
    Add-Result -Name 'ב. Line format date|LEVEL|logger|id|message' -Pass ($issues.Count -eq 0) `
        -Statuses "$checked line(s) checked" -Detail (($issues | Select-Object -First 5) -join ' | ')

    #endregion

    #region ג. Debug line of GET /api/books/{id}

    $lines = Get-RequestLines $book.SentId
    $issues = New-Issues
    if ($book.Status -ne 200) {
        $issues.Add("status $($book.Status), expected 200 (does book $BookId exist? pass -BookId)")
    }
    $debugLines = @($lines | Where-Object { $_.Contains('|DEBUG|') })
    if ($ExpectNoDebug) {
        if ($debugLines.Count -gt 0) { $issues.Add("$($debugLines.Count) DEBUG line(s) with the id, expected none") }
        $name = "ג. GET /api/books/$BookId has no DEBUG line (-ExpectNoDebug)"
    } else {
        if (-not (Test-HasLine $lines 'DEBUG' "\|Book $BookId has \d+ available copies$")) {
            $issues.Add("no DEBUG 'Book $BookId has N available copies' line (is LogSettings:MinLevel Debug?)")
        }
        $name = "ג. GET /api/books/$BookId has a DEBUG line"
    }
    Add-IssuesResult -Name $name -Response $book -Issues $issues

    #endregion

    #region ד. login: no password, no token

    $lines = Get-RequestLines $login.SentId
    $issues = New-Issues
    if (-not (Test-HasLine $lines 'INFO' "\|User $userId logged in$")) { $issues.Add("no 'User $userId logged in' line") }
    # Counts only: the matching lines would show the secret.
    $passwordHits = @($script:LogLines | Where-Object { $_.Contains($Password) }).Count
    if ($passwordHits -gt 0) { $issues.Add("the password appears in $passwordHits log line(s)") }
    $tokenHits = @($script:LogLines | Where-Object { $_.Contains($token) }).Count
    if ($tokenHits -gt 0) { $issues.Add("the token appears in $tokenHits log line(s)") }
    Add-IssuesResult -Name 'ד. Login: logged in, no password or token in the log' -Response $login -Issues $issues

    #endregion

    #region ה. authenticated request: no token

    $lines = Get-RequestLines $mine.SentId
    $issues = New-Issues
    if ($mine.Status -ne 200) { $issues.Add("status $($mine.Status), expected 200") }
    Add-StartedRespondedIssues $issues $lines '/api/loans/me' 200
    if ($tokenHits -gt 0) { $issues.Add("the token appears in $tokenHits log line(s)") }
    Add-IssuesResult -Name 'ה. GET /api/loans/me with a token: started/responded, no token' -Response $mine -Issues $issues

    #endregion

    #region ו. unknown route

    $lines = Get-RequestLines $missing.SentId
    $issues = New-Issues
    if ($missing.Status -ne 404) { $issues.Add("status $($missing.Status), expected 404") }
    if (-not (Test-HasLine $lines 'INFO' "\|HTTP GET $([regex]::Escape($missingPath)) responded 404 in ")) {
        $issues.Add("no 'responded 404' line")
    }
    Add-IssuesResult -Name "ו. GET $missingPath -> 404 responded line" -Response $missing -Issues $issues

    #endregion

    #region ז. unhandled exception (only with -ThrowPath)

    if ($throw) {
        $lines = Get-RequestLines $throw.SentId
        $issues = New-Issues
        if ($throw.Status -ne 500) { $issues.Add("status $($throw.Status), expected 500") }
        # ${onexception} appends |<exception type>: <message> to the line of the message.
        if (-not (Test-HasLine $lines 'ERROR' '\|Unhandled exception for HTTP .+\|[\w.`]+Exception\b')) {
            $issues.Add('no ERROR line with the id and the exception')
        }
        Add-IssuesResult -Name "ז. GET $ThrowPath -> 500 with an ERROR line" -Response $throw -Issues $issues
    } else {
        Write-Host '== Test ז skipped (no -ThrowPath)' -ForegroundColor Yellow
    }

    #endregion
}
catch {
    if (-not $preflightDone) {
        Write-Host "PREFLIGHT FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $exitCode = 2
    } else {
        Add-Result -Name 'Script error' -Pass $false -Statuses 'exception' -Detail $_.Exception.Message
    }
}
finally {
    try { [Console]::OutputEncoding = $originalOutputEncoding } catch { }
}

if ($exitCode -eq 2) { exit 2 }

$passed = @($Results | Where-Object Pass).Count
$failed = $Results.Count - $passed

Write-Host ''
Write-Host '== Summary'
Write-Host "  $passed/$($Results.Count) passed"

if ($failed -gt 0) { exit 1 }
exit 0
