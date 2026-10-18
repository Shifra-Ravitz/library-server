#Requires -Version 7
<#
בדיקת עשן לשלב 13: מכסת ההשאלות של המנוי (ActiveLoansCount ו-Version על המשתמשת).

מה נבדק:
  1. ברצף, ללקוחה חדשה במנוי שהמקסימום שלו 1:
     השאלה (201); השאלת עותק אחר (409 עם הודעת המכסה); הספרנית מחזירה את ההשאלה (204);
     השאלה שוב (201); הלקוחה מחזירה (204).
  2. במקביל: בכל סבב שתי בקשות השאלה של אותה לקוחה על שני עותקים שונים, בו-זמנית.
     בכל סבב בדיוק 201 אחד ו-409 אחד. כל 409 נספר לפי ההודעה: מכסה (הבקשה השנייה ראתה את המונה המעודכן)
     או התנגשות (מסלול ה-Version של המשתמשת). ההשאלה שהצליחה מוחזרת מיד.
  3. בסוף ללקוחה אין השאלות פעילות.

מה הסקריפט יוצר ומשאיר בבסיס הנתונים:
  - לקוחה חדשה smoke13-<timestamp>@example.com. היא נשארת, כי אין endpoint למחיקת משתמשת.
  - ספר smoke13-<timestamp> עם 3 עותקים, בקטגוריה קיימת. בסוף הוא מחוק לוגית (IsDeleted, העותקים Retired)
    ונשאר בטבלאות.
  - השאלות של הלקוחה החדשה על העותקים האלה. כולן מוחזרות ונשארות בהיסטוריה.
  הסקריפט לא נוגע בספרים, בעותקים, בהשאלות או בקטגוריות שהוא לא יצר.

דרישות:
  - השרת כבר רץ (למשל dotnet run --project src/Library.API --launch-profile http), עם ה-migration של שלב 13.
    הסקריפט לא בונה, לא מריץ את השרת ולא מוחק את בסיס הנתונים.
  - הספרנית מ-DbInitializer קיימת (סיסמה Demo123!), ויש לפחות קטגוריה אחת.

קודי יציאה: 0 הכול עבר והניקוי הצליח; 1 בדיקה או ניקוי נכשלו; 2 הבדיקה המקדימה נכשלה או שלא אושר.

דוגמה: pwsh -File scripts/smoke-stage13.ps1 -Rounds 20 -Force
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5150',
    [ValidateRange(1, 1000)][int]$Rounds = 10,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')

# Demo librarian and password from src/Library.Infrastructure/Persistence/DbInitializer.cs
$Password = 'Demo123!'
$LibrarianEmail = 'librarian@library.local'

# Messages returned by LoanService. The concurrency one means the user's Version path was taken.
$MsgBorrowConcurrency = 'העותק נתפס על ידי משתמשת אחרת, או שנשלחה במקביל בקשת השאלה נוספת מהחשבון שלך. נסי שוב.'
$MsgQuotaReachedFormat = 'הגעת למכסת ההשאלות של המנוי שלך (עד {0} ספרים בו-זמנית). החזירי ספר כדי לשאול ספר נוסף.'
$MsgQuotaReached = $null   # set once the plan is known

$Prefix = 'smoke13-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$MemberEmail = "$Prefix@example.com"

$Client = [System.Net.Http.HttpClient]::new()
$Client.Timeout = [TimeSpan]::FromSeconds(30)

$Results = [System.Collections.Generic.List[object]]::new()

# Everything this run creates, so cleanup touches only these.
$CreatedBookId = $null
$BookDeleted = $false
$MemberRegistered = $false
$CreatedLoanIds = [System.Collections.Generic.HashSet[int]]::new()
$script:LibToken = $null
$script:MemberToken = $null

$Stats = @{ ParallelQuota = 0; ParallelVersion = 0; ParallelOther = 0 }

#region HTTP helpers

function New-ApiRequest {
    param([string]$Method, [string]$Path, [string]$Token, $Body)

    $uri = if ($Path -match '^https?://') { $Path } else { $BaseUrl + $Path }
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $uri)
    if ($Token) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
    }
    $request
}

function Read-ApiResponse {
    param([System.Net.Http.HttpResponseMessage]$Response)

    $text = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $parsed = $null
    if ($text) {
        try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $null }
    }

    $detail = $null
    if ($parsed -and $parsed.PSObject.Properties['detail']) { $detail = [string]$parsed.detail }

    $location = $null
    if ($Response.Headers.Location) { $location = $Response.Headers.Location.ToString() }

    [pscustomobject]@{
        Status   = [int]$Response.StatusCode
        Text     = $text
        Json     = $parsed
        Detail   = $detail
        Location = $location
    }
}

function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body)

    $request = New-ApiRequest -Method $Method -Path $Path -Token $Token -Body $Body
    try {
        Read-ApiResponse ($Client.SendAsync($request).GetAwaiter().GetResult())
    } finally {
        $request.Dispose()
    }
}

# One POST /api/loans per copy, each from its own runspace (ForEach-Object -Parallel).
# The runspaces start at different moments, so each one waits on a shared Barrier and only then sends:
# the requests reach the server at (almost) the same moment. Returns the responses in the order of $CopyIds.
function Invoke-ParallelBorrows {
    param([string]$Token, [int[]]$CopyIds)

    $barrier = [System.Threading.Barrier]::new($CopyIds.Count)
    try {
        $raw = @($CopyIds | ForEach-Object -ThrottleLimit $CopyIds.Count -Parallel {
            # $using: values are copied into locals first; members cannot be called on $using: directly.
            $client = $using:Client
            $gate = $using:barrier
            $copyId = $_

            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "$($using:BaseUrl)/api/loans")
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $using:Token)
            $json = @{ copyId = $copyId } | ConvertTo-Json -Compress
            $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
            try {
                [void]$gate.SignalAndWait([TimeSpan]::FromSeconds(30))
                [pscustomobject]@{ CopyId = $copyId; Response = $client.SendAsync($request).GetAwaiter().GetResult() }
            } finally {
                $request.Dispose()
            }
        })
    } finally {
        $barrier.Dispose()
    }

    , @($CopyIds | ForEach-Object {
        $id = $_
        Read-ApiResponse ($raw | Where-Object CopyId -eq $id | Select-Object -First 1).Response
    })
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

function Get-ConflictKind {
    param([string]$Detail)
    if ($Detail -ceq $MsgQuotaReached) { return 'quota' }
    if ($Detail -ceq $MsgBorrowConcurrency) { return 'version' }
    'other'
}

function Format-Status {
    param($Response)
    $s = "$($Response.Status)"
    if ($Response.Status -eq 409) {
        $s += " ($(Get-ConflictKind $Response.Detail))"
    }
    $s
}

function Get-ProblemTexts {
    param([object[]]$Responses)
    ($Responses | Where-Object { $_.Status -ge 400 } | ForEach-Object { "$($_.Status): $($_.Text)" }) -join ' | '
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

function New-SmokeCopy {
    param([int]$BookId)

    $r = Invoke-Api -Method POST -Path "/api/books/$BookId/copies" -Token $script:LibToken
    if ($r.Status -ne 201) { throw "Creating a copy of book $BookId failed (HTTP $($r.Status)): $($r.Text)" }
    [int]$r.Json.id
}

function Register-Loan {
    param($Response)
    if ($Response.Status -eq 201 -and $Response.Json -and $Response.Json.PSObject.Properties['id']) {
        [void]$CreatedLoanIds.Add([int]$Response.Json.id)
    }
}

function Invoke-Borrow {
    param([int]$CopyId)
    $r = Invoke-Api -Method POST -Path '/api/loans' -Token $script:MemberToken -Body @{ copyId = $CopyId }
    Register-Loan $r
    $r
}

#endregion

# Hebrew server messages are printed in failure details.
$originalOutputEncoding = [Console]::OutputEncoding
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$exitCode = 0
$preflightDone = $false
$cleanupFailed = $false

try {
    #region Preflight (no changes)

    Write-Host "== Preflight against $BaseUrl"
    try {
        $plansResponse = Invoke-Api -Method GET -Path '/api/membership-plans'
    } catch {
        throw [System.InvalidOperationException]::new("The server at $BaseUrl is not reachable ($($_.Exception.Message)). Start it first: dotnet run --project src/Library.API --launch-profile http")
    }
    if ($plansResponse.Status -ne 200) {
        throw "GET /api/membership-plans (no token) returned HTTP $($plansResponse.Status): $($plansResponse.Text)"
    }

    $script:LibToken = Get-Login -Email $LibrarianEmail -ExpectedRole 'Librarian'

    $plan = @($plansResponse.Json) | Sort-Object { [int]$_.maxConcurrentLoans }, { [int]$_.id } | Select-Object -First 1
    if (-not $plan) { throw 'GET /api/membership-plans returned no plans.' }
    $maxLoans = [int]$plan.maxConcurrentLoans
    if ($maxLoans -ne 1) {
        throw "The plan with the lowest limit ('$($plan.name)', id $($plan.id)) allows $maxLoans loans; this script needs a plan that allows exactly 1."
    }
    $MsgQuotaReached = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, $MsgQuotaReachedFormat, $maxLoans)

    $r = Invoke-Api -Method GET -Path '/api/categories' -Token $script:LibToken
    if ($r.Status -ne 200) { throw "GET /api/categories returned HTTP $($r.Status)." }
    $category = @($r.Json) | Select-Object -First 1
    if (-not $category) { throw 'There is no category; the smoke book needs an existing one. Create a category first.' }
    $categoryId = [int]$category.id

    Write-Host '== Preflight OK'
    $preflightDone = $true

    #endregion

    #region Warning and confirmation

    Write-Host ''
    Write-Host "This run will create data with the prefix '$Prefix':" -ForegroundColor Yellow
    Write-Host "  - Member:   $MemberEmail on plan '$($plan.name)' (id $($plan.id), up to $maxLoans loan)."
    Write-Host '              It STAYS in the database: there is no endpoint for deleting a user.'
    Write-Host "  - Book:     '$Prefix' with 3 copies, in existing category $categoryId ('$($category.name)'), read only."
    Write-Host "  - Loans:    $(2 + $Rounds) to $(2 + 2 * $Rounds) by the new member; all returned, and they stay in the loan history."
    Write-Host '  - At the end the book is soft-deleted (IsDeleted, copies Retired) and stays in the tables.'
    Write-Host '  Nothing that this run did not create is changed or deleted.'
    Write-Host ''

    if (-not $Force) {
        try {
            $answer = Read-Host 'Continue? [Y/N]'
        } catch {
            throw [System.OperationCanceledException]::new('Could not ask for confirmation (non-interactive session). Run with -Force to skip it.')
        }
        if ($answer -notmatch '^(y|yes)$') {
            # Thrown rather than 'return': a return here would end the script before the exit code is set.
            throw [System.OperationCanceledException]::new('Cancelled by the user. Nothing was changed.')
        }
    }

    #endregion

    #region Setup

    $r = Invoke-Api -Method POST -Path '/api/auth/register' -Body @{
        name = $Prefix; email = $MemberEmail; password = $Password; membershipPlanId = [int]$plan.id
    }
    if ($r.Status -ne 200 -or -not $r.Json -or -not $r.Json.token) {
        throw "Registering $MemberEmail failed (HTTP $($r.Status)): $($r.Text)"
    }
    $MemberRegistered = $true
    $script:MemberToken = [string]$r.Json.token
    Write-Host "== Registered member $MemberEmail (user $($r.Json.userId))"

    $r = Invoke-Api -Method POST -Path '/api/books' -Token $script:LibToken -Body @{
        title = $Prefix; author = 'smoke13'; publicationYear = 2020; categoryIds = @($categoryId)
    }
    if ($r.Status -ne 201) { throw "Creating book '$Prefix' failed (HTTP $($r.Status)): $($r.Text)" }
    $CreatedBookId = [int]$r.Json.id
    $copyIds = @(1..3 | ForEach-Object { New-SmokeCopy -BookId $CreatedBookId })
    Write-Host "== Created book $CreatedBookId with copies $($copyIds -join ', ')"

    #endregion

    #region Test 1: the quota, one request at a time

    Write-Host "== Test 1: quota of $maxLoans, sequential"

    $first = Invoke-Borrow -CopyId $copyIds[0]
    Add-Result -Name "1a. borrow copy $($copyIds[0])" -Pass ($first.Status -eq 201) -Statuses (Format-Status $first) -Detail $first.Text

    $r = Invoke-Borrow -CopyId $copyIds[1]
    $pass = ($r.Status -eq 409) -and ((Get-ConflictKind $r.Detail) -eq 'quota')
    Add-Result -Name "1b. borrow copy $($copyIds[1]) over the quota" -Pass $pass -Statuses (Format-Status $r) -Detail $r.Text

    if ($first.Status -eq 201) {
        $r = Invoke-Api -Method PATCH -Path "/api/loans/$($first.Json.id)" -Token $script:LibToken
        Add-Result -Name "1c. librarian returns loan $($first.Json.id)" -Pass ($r.Status -eq 204) -Statuses (Format-Status $r) -Detail $r.Text
    } else {
        Add-Result -Name '1c. librarian returns the loan' -Pass $false -Statuses 'skipped' -Detail 'No loan from 1a.'
    }

    $second = Invoke-Borrow -CopyId $copyIds[1]
    Add-Result -Name "1d. borrow copy $($copyIds[1]) again" -Pass ($second.Status -eq 201) -Statuses (Format-Status $second) -Detail $second.Text

    if ($second.Status -eq 201) {
        $r = Invoke-Api -Method PATCH -Path "/api/loans/$($second.Json.id)" -Token $script:MemberToken
        Add-Result -Name "1e. member returns loan $($second.Json.id)" -Pass ($r.Status -eq 204) -Statuses (Format-Status $r) -Detail $r.Text
    } else {
        Add-Result -Name '1e. member returns the loan' -Pass $false -Statuses 'skipped' -Detail 'No loan from 1d.'
    }

    #endregion

    #region Test 2: two borrows by the same member at once

    Write-Host "== Test 2: two parallel borrows by the same member ($Rounds rounds)"
    $pairCopies = @($copyIds[0], $copyIds[1])
    for ($i = 1; $i -le $Rounds; $i++) {
        $pair = Invoke-ParallelBorrows -Token $script:MemberToken -CopyIds $pairCopies
        Register-Loan $pair[0]
        Register-Loan $pair[1]

        $statuses = "copy $($pairCopies[0]): $(Format-Status $pair[0]) / copy $($pairCopies[1]): $(Format-Status $pair[1])"
        $winners = @(0, 1 | Where-Object { $pair[$_].Status -eq 201 })
        $losers = @(0, 1 | Where-Object { $pair[$_].Status -eq 409 })
        $pass = ($winners.Count -eq 1) -and ($losers.Count -eq 1)
        $detail = Get-ProblemTexts $pair
        if ($winners.Count -eq 2) {
            $detail = "BOTH SUCCEEDED: the member holds 2 loans on a plan of $maxLoans. $detail"
        }

        if ($losers.Count -eq 1) {
            switch (Get-ConflictKind $pair[$losers[0]].Detail) {
                'quota' { $Stats.ParallelQuota++ }
                'version' { $Stats.ParallelVersion++ }
                default { $Stats.ParallelOther++ }
            }
        }

        # Every loan this round created is returned right away (also a stray second 201).
        foreach ($w in $winners) {
            $ret = Invoke-Api -Method PATCH -Path "/api/loans/$($pair[$w].Json.id)" -Token $script:MemberToken
            $statuses += ", return $($ret.Status)"
            if ($ret.Status -ne 204) {
                $pass = $false
                $detail += " | return: $($ret.Text)"
            }
        }

        Add-Result -Name ("2.{0:D2} parallel borrows" -f $i) -Pass $pass -Statuses $statuses -Detail $detail
    }

    #endregion

    #region Test 3: nothing is left active

    Write-Host '== Test 3: no active loans left'
    $r = Invoke-Api -Method GET -Path '/api/loans/me?ActiveOnly=true&PageSize=50' -Token $script:MemberToken
    $pass = ($r.Status -eq 200) -and ([int]$r.Json.totalCount -eq 0)
    $active = if ($r.Status -eq 200) { "$($r.Json.totalCount) active" } else { '' }
    Add-Result -Name '3.   GET /api/loans/me?ActiveOnly=true' -Pass $pass -Statuses "$($r.Status) $active" -Detail $r.Text

    #endregion
}
catch {
    if ($_.Exception -is [System.OperationCanceledException]) {
        Write-Host $_.Exception.Message
        $exitCode = 2
    } elseif (-not $preflightDone) {
        # Nothing was changed yet.
        Write-Host "PREFLIGHT FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $exitCode = 2
    } else {
        Add-Result -Name 'Script error' -Pass $false -Statuses 'exception' -Detail $_.Exception.Message
    }
}
finally {
    #region Cleanup: only what this run created

    $hasSomethingToClean = ($null -ne $CreatedBookId) -or ($CreatedLoanIds.Count -gt 0) -or $MemberRegistered
    if ($hasSomethingToClean) {
        Write-Host '== Cleanup'
        try {
            # Safety net: every active loan of the new member, even if its id was not recorded.
            # The member was created by this run, so all of its loans are ours.
            if ($script:MemberToken) {
                $r = Invoke-Api -Method GET -Path '/api/loans/me?ActiveOnly=true&PageSize=50' -Token $script:MemberToken
                if ($r.Status -eq 200) {
                    foreach ($l in @($r.Json.items)) {
                        if ($l) { [void]$CreatedLoanIds.Add([int]$l.id) }
                    }
                } else {
                    Write-Host "  cleanup: GET /api/loans/me returned $($r.Status)" -ForegroundColor Red
                    $cleanupFailed = $true
                }
            }

            foreach ($loanId in $CreatedLoanIds) {
                $r = Invoke-Api -Method GET -Path "/api/loans/$loanId" -Token $script:LibToken
                if ($r.Status -ne 200) {
                    Write-Host "  cleanup: GET loan $loanId returned $($r.Status)" -ForegroundColor Red
                    $cleanupFailed = $true
                    continue
                }
                if ($null -eq $r.Json.returnedAtUtc) {
                    $p = Invoke-Api -Method PATCH -Path "/api/loans/$loanId" -Token $script:LibToken
                    if ($p.Status -eq 204) {
                        Write-Host "  returned loan $loanId"
                    } else {
                        Write-Host "  cleanup: returning loan $loanId failed ($($p.Status)): $($p.Text)" -ForegroundColor Red
                        $cleanupFailed = $true
                    }
                }
            }

            if ($null -ne $CreatedBookId -and -not $BookDeleted) {
                $r = Invoke-Api -Method DELETE -Path "/api/books/$CreatedBookId" -Token $script:LibToken
                if ($r.Status -in 204, 404) {
                    $BookDeleted = $true
                    Write-Host "  soft-deleted book $CreatedBookId"
                } else {
                    Write-Host "  cleanup: deleting book $CreatedBookId failed ($($r.Status)): $($r.Text)" -ForegroundColor Red
                    $cleanupFailed = $true
                }
            }
        } catch {
            Write-Host "  cleanup error: $($_.Exception.Message)" -ForegroundColor Red
            $cleanupFailed = $true
        }

        if ($MemberRegistered) {
            Write-Host "  note: member $MemberEmail stays in the database (there is no endpoint for deleting a user)." -ForegroundColor Yellow
        }
    }

    $Client.Dispose()
    try { [Console]::OutputEncoding = $originalOutputEncoding } catch { }

    #endregion
}

if ($exitCode -eq 2) { exit 2 }

$passed = @($Results | Where-Object Pass).Count
$failed = $Results.Count - $passed

Write-Host ''
Write-Host '== Summary'
Write-Host "  Checks: $passed passed, $failed failed"
Write-Host "  Test 2 409s: $($Stats.ParallelQuota) quota (counter already updated), $($Stats.ParallelVersion) conflict (user Version), $($Stats.ParallelOther) other"
if ($Stats.ParallelVersion -eq 0) {
    Write-Warning 'The Version path was never hit. That depends on timing, so it is not a failure; try a larger -Rounds.'
}
Write-Host ("  Cleanup: {0}" -f $(if ($cleanupFailed) { 'FAILED' } else { 'OK' }))
if ($MemberRegistered) {
    Write-Host "  Left in the database: member $MemberEmail (no endpoint for deleting a user)."
}

if ($failed -gt 0 -or $cleanupFailed) { exit 1 }
exit 0
