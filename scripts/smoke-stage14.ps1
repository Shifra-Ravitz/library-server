#Requires -Version 7
<#
בדיקת עשן לשלב 14: Middleware לשגיאות ו-CorrelationId.

מה נבדק:
  בכל תשובת שגיאה: Content-Type מסוג application/problem+json, השדה status שווה לסטטוס, יש correlationId
  והוא שווה ל-header X-Correlation-ID, ואין בגוף stackTrace או exception.
  1. GET /api/books בלי token: 401 עם גוף.
  2. לקוחה עושה POST /api/books: 403 עם גוף.
  3. GET /api/books/999999 עם token: 404 עם detail.
  4. ספרנית עושה POST /api/books עם גוף {}: 400 עם errors.
  5. GET /api/no-such-route עם token: 404 עם גוף.
  6. X-Correlation-ID שנשלח (smoke14-<timestamp>) חוזר ב-header וב-correlationId.
  7. בלי header: ה-header בתשובה הוא Guid תקין.
  8. header לא תקין (עם רווחים, או באורך 100): מוחלף ב-Guid.
  9. רק עם -ThrowPath (למשל /api/diagnostics/throw): 500 עם הכותרת הקבועה, בלי הטקסט של ה-exception.

מה הסקריפט יוצר בבסיס הנתונים: כלום. הבקשות היחידות שמשנות נתונים (POST /api/books) נשלחות עם גוף {},
כך שגם אם ההרשאה או הוולידציה שבורות לא נוצר ספר.

דרישות:
  - השרת כבר רץ (למשל dotnet run --project src/Library.API --launch-profile http).
    הסקריפט לא בונה, לא מריץ את השרת ולא מוחק את בסיס הנתונים.
  - משתמשי הדמו מ-DbInitializer קיימים (ספרנית ולקוחה, סיסמה Demo123!).

קודי יציאה: 0 הכול עבר; 1 בדיקה נכשלה; 2 הבדיקה המקדימה נכשלה.

דוגמה: pwsh -File scripts/smoke-stage14.ps1 -ThrowPath /api/diagnostics/throw
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5150',
    [string]$ThrowPath
)

$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')

# Demo users and password from src/Library.Infrastructure/Persistence/DbInitializer.cs
$Password = 'Demo123!'
$LibrarianEmail = 'librarian@library.local'
$MemberEmail = 'member1@library.local'

# From src/Library.API/Middleware
$CorrelationHeader = 'X-Correlation-ID'
$MsgServerError = 'אירעה שגיאה בלתי צפויה בשרת.'
$ExceptionText = 'Stage 14 test exception'

$MissingBookPath = '/api/books/999999'
$Prefix = 'smoke14-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')

$Results = [System.Collections.Generic.List[object]]::new()

#region HTTP helpers

function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body, [hashtable]$Headers = @{})

    # Accept as a browser client of this API would send it; ProblemDetails is written for JSON clients.
    $requestHeaders = @{ Accept = 'application/json' }
    foreach ($key in $Headers.Keys) { $requestHeaders[$key] = $Headers[$key] }
    if ($Token) { $requestHeaders['Authorization'] = "Bearer $Token" }

    $params = @{
        Method               = $Method
        Uri                  = if ($Path -match '^https?://') { $Path } else { $BaseUrl + $Path }
        Headers              = $requestHeaders
        SkipHttpErrorCheck   = $true
        SkipHeaderValidation = $true   # test 8 sends a header value with spaces on purpose
        TimeoutSec           = 30
    }
    if ($null -ne $Body) {
        $params.Body = $Body | ConvertTo-Json -Depth 10 -Compress
        $params.ContentType = 'application/json; charset=utf-8'
    }

    $response = Invoke-WebRequest @params

    # Read the raw bytes as UTF-8: problem+json is sent without a charset, and the titles are in Hebrew.
    $text = [System.Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
    $parsed = $null
    if ($text) {
        try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null }
    }

    [pscustomobject]@{
        Status        = [int]$response.StatusCode
        Text          = $text
        Json          = $parsed
        ContentType   = Get-Header $response 'Content-Type'
        CorrelationId = Get-Header $response $CorrelationHeader
    }
}

# Joined with ', ' so a header that was sent twice shows up as a mismatch.
function Get-Header {
    param($Response, [string]$Name)
    $values = $Response.Headers[$Name]
    if ($null -eq $values) { return $null }
    @($values) -join ', '
}

function Get-JsonProperty {
    param($Json, [string]$Name)
    if ($Json -and $Json.PSObject.Properties[$Name]) { $Json.$Name } else { $null }
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

function Test-IsGuid {
    param([string]$Value)
    $guid = [Guid]::Empty
    [Guid]::TryParse($Value, [ref]$guid)
}

# What every error response must satisfy. Returns the list of problems found (empty when it is fine).
function Get-ProblemIssues {
    param($Response, [int]$ExpectedStatus)

    $issues = [System.Collections.Generic.List[string]]::new()
    if ($Response.Status -ne $ExpectedStatus) { $issues.Add("status $($Response.Status), expected $ExpectedStatus") }
    if ($Response.ContentType -notlike 'application/problem+json*') { $issues.Add("Content-Type is '$($Response.ContentType)'") }
    if ($Response.Text -match 'stacktrace|exception') { $issues.Add('the body mentions stackTrace/exception') }

    if (-not $Response.Json) {
        $issues.Add('the body is not JSON')
        return , $issues
    }

    $status = Get-JsonProperty $Response.Json 'status'
    if ("$status" -ne "$ExpectedStatus") { $issues.Add("body status is '$status'") }

    $correlationId = Get-JsonProperty $Response.Json 'correlationId'
    if (-not $correlationId) {
        $issues.Add('no correlationId in the body')
    } elseif ("$correlationId" -cne "$($Response.CorrelationId)") {
        $issues.Add("correlationId '$correlationId' differs from header '$($Response.CorrelationId)'")
    }

    , $issues
}

function Add-ProblemResult {
    param([string]$Name, $Response, [System.Collections.Generic.List[string]]$Issues)

    $statuses = "$($Response.Status) $($Response.ContentType), $CorrelationHeader=$($Response.CorrelationId)"
    $detail = if ($Issues.Count -gt 0) { ($Issues -join '; ') + " | body: $($Response.Text)" } else { '' }
    Add-Result -Name $Name -Pass ($Issues.Count -eq 0) -Statuses $statuses -Detail $detail
}

#endregion

#region Steps

function Get-Login {
    param([string]$Email, [string]$ExpectedRole)

    $r = Invoke-Api -Method POST -Path '/api/auth/login' -Body @{ email = $Email; password = $Password }
    if ($r.Status -ne 200 -or -not $r.Json -or -not $r.Json.token) {
        throw "Login failed for demo user $Email (HTTP $($r.Status)). Make sure the demo users from DbInitializer exist and the password is $Password."
    }
    if ("$($r.Json.role)" -ne $ExpectedRole) {
        throw "Demo user $Email has role '$($r.Json.role)', expected '$ExpectedRole'."
    }
    [string]$r.Json.token
}

#endregion

# Hebrew server messages are printed in failure details.
$originalOutputEncoding = [Console]::OutputEncoding
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$exitCode = 0
$preflightDone = $false

try {
    #region Preflight (no changes)

    Write-Host "== Preflight against $BaseUrl"
    try {
        $r = Invoke-Api -Method GET -Path '/api/membership-plans'
    } catch {
        throw [System.InvalidOperationException]::new("The server at $BaseUrl is not reachable ($($_.Exception.Message)). Start it first: dotnet run --project src/Library.API --launch-profile http")
    }
    if ($r.Status -ne 200) {
        throw "GET /api/membership-plans (no token) returned HTTP $($r.Status): $($r.Text)"
    }

    $libToken = Get-Login -Email $LibrarianEmail -ExpectedRole 'Librarian'
    $memberToken = Get-Login -Email $MemberEmail -ExpectedRole 'Member'

    Write-Host '== Preflight OK'
    $preflightDone = $true

    #endregion

    #region Tests 1-5: every error status has a ProblemDetails body

    Write-Host '== Tests 1-5: error responses'

    $r = Invoke-Api -Method GET -Path '/api/books'
    Add-ProblemResult -Name '1. GET /api/books without a token' -Response $r -Issues (Get-ProblemIssues $r 401)

    # Body {}: if authorization were broken this gives 400, and still no book is created.
    $r = Invoke-Api -Method POST -Path '/api/books' -Token $memberToken -Body @{}
    Add-ProblemResult -Name '2. Member POST /api/books' -Response $r -Issues (Get-ProblemIssues $r 403)

    $r = Invoke-Api -Method GET -Path $MissingBookPath -Token $memberToken
    $issues = Get-ProblemIssues $r 404
    if (-not (Get-JsonProperty $r.Json 'detail')) { $issues.Add('no detail') }
    Add-ProblemResult -Name "3. GET $MissingBookPath" -Response $r -Issues $issues

    $r = Invoke-Api -Method POST -Path '/api/books' -Token $libToken -Body @{}
    $issues = Get-ProblemIssues $r 400
    $errors = Get-JsonProperty $r.Json 'errors'
    if (-not $errors -or @($errors.PSObject.Properties).Count -eq 0) { $issues.Add('no errors') }
    Add-ProblemResult -Name '4. Librarian POST /api/books with {}' -Response $r -Issues $issues

    $r = Invoke-Api -Method GET -Path '/api/no-such-route' -Token $memberToken
    Add-ProblemResult -Name '5. GET /api/no-such-route' -Response $r -Issues (Get-ProblemIssues $r 404)

    #endregion

    #region Tests 6-8: the correlation id

    Write-Host '== Tests 6-8: correlation id'

    $sent = $Prefix
    $r = Invoke-Api -Method GET -Path $MissingBookPath -Token $memberToken -Headers @{ $CorrelationHeader = $sent }
    $issues = Get-ProblemIssues $r 404
    if ($r.CorrelationId -cne $sent) { $issues.Add("header is '$($r.CorrelationId)', expected '$sent'") }
    Add-ProblemResult -Name "6. $CorrelationHeader '$sent' is echoed" -Response $r -Issues $issues

    $r = Invoke-Api -Method GET -Path '/api/membership-plans'
    $pass = ($r.Status -eq 200) -and (Test-IsGuid $r.CorrelationId)
    Add-Result -Name '7. No header: a Guid is generated' -Pass $pass `
        -Statuses "$($r.Status), $CorrelationHeader=$($r.CorrelationId)" -Detail 'expected 200 and a Guid in the header'

    $invalidValues = [ordered]@{
        'with spaces' = 'smoke14 has spaces'
        'length 100'  = 'a' * 100
    }
    foreach ($entry in $invalidValues.GetEnumerator()) {
        $r = Invoke-Api -Method GET -Path $MissingBookPath -Token $memberToken -Headers @{ $CorrelationHeader = $entry.Value }
        $issues = Get-ProblemIssues $r 404
        if (-not (Test-IsGuid $r.CorrelationId)) { $issues.Add("header '$($r.CorrelationId)' is not a Guid") }
        Add-ProblemResult -Name "8. Invalid header ($($entry.Key)) is replaced" -Response $r -Issues $issues
    }

    #endregion

    #region Test 9: an unhandled exception (only with -ThrowPath)

    if ($ThrowPath) {
        Write-Host "== Test 9: unhandled exception at $ThrowPath"
        $r = Invoke-Api -Method GET -Path $ThrowPath
        $issues = Get-ProblemIssues $r 500
        $title = Get-JsonProperty $r.Json 'title'
        if ($title -cne $MsgServerError) { $issues.Add("title is '$title'") }
        if ($r.Text.Contains($ExceptionText)) { $issues.Add('the body contains the exception message') }
        Add-ProblemResult -Name "9. GET $ThrowPath" -Response $r -Issues $issues
    } else {
        Write-Host '== Test 9 skipped (no -ThrowPath)' -ForegroundColor Yellow
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
Write-Host "  Checks: $passed passed, $failed failed"

if ($failed -gt 0) { exit 1 }
exit 0
