#Requires -Version 7
<#
בדיקת עשן לשלב 12: תחרות על העותק (optimistic concurrency ו-409).

מה נבדק:
  1. שתי לקוחות שואלות את אותו עותק בו-זמנית: בדיוק 201 אחד ו-409 אחד, ושום 500.
     ה-Location של ה-201 נפתח ב-GET ומחזיר 200.
  2. החזרה כפולה: שתי בקשות PATCH לאותה השאלה בו-זמנית: בדיוק 204 אחד ו-409 אחד.
  3. מחיקת ספר מול השאלת העותק שלו בו-זמנית: (204 למחיקה ו-404/409 להשאלה) או (409 למחיקה ו-201 להשאלה).
     אסור 204 ו-201 יחד, ואסור 500.
  4. רגרסיה: מחיקת עותק מושאל מחזירה 409, ואחרי החזרה מחיקתו מחזירה 204.
  בכל 409 נספר אם הוא הגיע ממסלול ה-Version (הודעת ה-concurrency) או מהבדיקה העסקית.

מה הסקריפט יוצר ומשאיר בבסיס הנתונים:
  - ספרים ועותקים חדשים, ששמם מתחיל ב-smoke12-<timestamp>. בסוף הם מחוקים לוגית (IsDeleted / Retired)
    ונשארים בטבלאות.
  - השאלות של משתמשות הדמו על העותקים האלה. כולן מוחזרות ונשארות בהיסטוריה.
  - קטגוריה נוצרת רק אם אין אף קטגוריה, ונמחקת בסוף.
  הסקריפט לא נוגע בספרים, בעותקים, בהשאלות או בקטגוריות שהוא לא יצר.

דרישות:
  - השרת כבר רץ (למשל dotnet run --project src/Library.API --launch-profile http).
    הסקריפט לא בונה, לא מריץ את השרת ולא מוחק את בסיס הנתונים.
  - משתמשי הדמו מ-DbInitializer קיימים (ספרנית ושתי לקוחות, סיסמה Demo123!).

קודי יציאה: 0 הכול עבר והניקוי הצליח; 1 בדיקה או ניקוי נכשלו; 2 הבדיקה המקדימה נכשלה או שלא אושר.

דוגמה: pwsh -File scripts/smoke-stage12.ps1 -Rounds 30 -Force
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5150',
    [ValidateRange(1, 1000)][int]$Rounds = 20,
    [ValidateRange(1, 1000)][int]$DeleteRounds = 10,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$BaseUrl = $BaseUrl.TrimEnd('/')

# Demo users and password from src/Library.Infrastructure/Persistence/DbInitializer.cs
$Password = 'Demo123!'
$LibrarianEmail = 'librarian@library.local'
$MemberAEmail = 'member1@library.local'
$MemberBEmail = 'member2@library.local'

# Messages returned by the services. The concurrency ones mean the Version path was taken.
$MsgBorrowConcurrency = 'העותק נתפס על ידי משתמש אחר, או שנשלחה במקביל בקשת השאלה נוספת מהחשבון שלך. נסה שוב.'
$MsgReturnConcurrency = 'ההשאלה עודכנה בינתיים על ידי בקשה אחרת. נסה שוב.'
$MsgBookDeleteConcurrency = 'הספר עודכן בינתיים (ייתכן שעותק שלו הושאל ברגע זה). נסה שוב.'
$MsgCopyAlreadyLoaned = 'העותק כבר מושאל.'
$MsgLoanAlreadyReturned = 'ההשאלה כבר הוחזרה.'
$MsgBookHasLoanedCopy = 'לא ניתן למחוק ספר שיש לו עותק מושאל כרגע.'

$Prefix = 'smoke12-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')

$Client = [System.Net.Http.HttpClient]::new()
$Client.Timeout = [TimeSpan]::FromSeconds(30)

$Results = [System.Collections.Generic.List[object]]::new()

# Everything this run creates, so cleanup touches only these.
$CreatedBooks = [System.Collections.Generic.List[object]]::new()   # { Id; Deleted }
$CreatedLoanIds = [System.Collections.Generic.HashSet[int]]::new()
$CreatedCategoryId = $null
$tokenA = $null
$tokenB = $null
$script:LibToken = $null
$script:CategoryId = $null

$Stats = @{
    BorrowVersion = 0; BorrowBusiness = 0; BorrowOther = 0
    ReturnVersion = 0; ReturnBusiness = 0
    DeleteRaceVersion = 0; DeleteRaceBusiness = 0
}

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

# Both requests are built first, then sent with no await between the two SendAsync calls,
# so they reach the server at (almost) the same moment over two connections of the same HttpClient.
function Invoke-ApiPair {
    param([hashtable]$First, [hashtable]$Second)

    $req1 = New-ApiRequest @First
    $req2 = New-ApiRequest @Second
    try {
        $task1 = $Client.SendAsync($req1)
        $task2 = $Client.SendAsync($req2)
        [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]@($task1, $task2))
        , @((Read-ApiResponse $task1.Result), (Read-ApiResponse $task2.Result))
    } finally {
        $req1.Dispose()
        $req2.Dispose()
    }
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

function Format-Status {
    param($Response)
    $s = "$($Response.Status)"
    if ($Response.Status -eq 409) {
        $s += " ($(Get-ConflictKind $Response.Detail))"
    }
    $s
}

function Get-ConflictKind {
    param([string]$Detail)
    switch ($Detail) {
        $MsgBorrowConcurrency { 'version' }
        $MsgReturnConcurrency { 'version' }
        $MsgBookDeleteConcurrency { 'version' }
        $MsgCopyAlreadyLoaned { 'business' }
        $MsgLoanAlreadyReturned { 'business' }
        $MsgBookHasLoanedCopy { 'business' }
        default { 'other' }
    }
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

function New-SmokeBook {
    param([string]$Title)

    $r = Invoke-Api -Method POST -Path '/api/books' -Token $script:LibToken -Body @{
        title = $Title; author = 'smoke12'; publicationYear = 2020; categoryIds = @($script:CategoryId)
    }
    if ($r.Status -ne 201) { throw "Creating book '$Title' failed (HTTP $($r.Status)): $($r.Text)" }
    $id = [int]$r.Json.id
    $CreatedBooks.Add([pscustomobject]@{ Id = $id; Deleted = $false })
    $id
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

function Set-BookDeleted {
    param([int]$BookId)
    foreach ($b in $CreatedBooks) { if ($b.Id -eq $BookId) { $b.Deleted = $true } }
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
        $r = Invoke-Api -Method GET -Path '/api/membership-plans'
        if ($r.Status -ge 500) { throw "HTTP $($r.Status)" }
    } catch {
        throw [System.InvalidOperationException]::new("The server at $BaseUrl is not reachable ($($_.Exception.Message)). Start it first: dotnet run --project src/Library.API --launch-profile http")
    }

    $script:LibToken = Get-Login -Email $LibrarianEmail -ExpectedRole 'Librarian'
    $tokenA = Get-Login -Email $MemberAEmail -ExpectedRole 'Member'
    $tokenB = Get-Login -Email $MemberBEmail -ExpectedRole 'Member'
    $members = @(
        [pscustomobject]@{ Email = $MemberAEmail; Token = $tokenA },
        [pscustomobject]@{ Email = $MemberBEmail; Token = $tokenB }
    )

    foreach ($m in $members) {
        $r = Invoke-Api -Method GET -Path '/api/loans/me?OverdueOnly=true&PageSize=1' -Token $m.Token
        if ($r.Status -ne 200) { throw "GET /api/loans/me for $($m.Email) returned HTTP $($r.Status)." }
        if ([int]$r.Json.totalCount -gt 0) {
            throw "$($m.Email) has $($r.Json.totalCount) overdue loan(s), so every borrow would be rejected. Return them first; this script does not touch loans it did not create."
        }

        $r = Invoke-Api -Method GET -Path '/api/loans/me?ActiveOnly=true&PageSize=1' -Token $m.Token
        if ([int]$r.Json.totalCount -gt 0) {
            Write-Warning "$($m.Email) already has $($r.Json.totalCount) active loan(s). Once loan limits exist (stage 13) this may make borrows fail."
        }
    }

    $r = Invoke-Api -Method GET -Path '/api/categories' -Token $script:LibToken
    if ($r.Status -ne 200) { throw "GET /api/categories returned HTTP $($r.Status)." }
    $existingCategory = @($r.Json) | Select-Object -First 1
    $script:CategoryId = if ($existingCategory) { [int]$existingCategory.id } else { $null }

    Write-Host '== Preflight OK'
    $preflightDone = $true

    #endregion

    #region Warning and confirmation

    $bookCount = 1 + $DeleteRounds
    $copyCount = $Rounds + 2 + $DeleteRounds
    $minLoans = $Rounds + 2
    $maxLoans = $Rounds + 2 + $DeleteRounds

    Write-Host ''
    Write-Host "This run will create data with the prefix '$Prefix':" -ForegroundColor Yellow
    if ($script:CategoryId) {
        Write-Host "  - Category: none created; uses existing category $($script:CategoryId) ('$($existingCategory.name)'), read only."
    } else {
        Write-Host "  - Category: '$Prefix-cat' (there is none), physically deleted at the end."
    }
    Write-Host "  - Books:    $bookCount ('$Prefix-main' and '$Prefix-del-1'..'$Prefix-del-$DeleteRounds')"
    Write-Host "  - Copies:   $copyCount ($($Rounds + 2) of the main book, 1 per delete-round book)"
    Write-Host "  - Loans:    $minLoans to $maxLoans, by $MemberAEmail and $MemberBEmail; all returned, and they stay in the loan history"
    Write-Host "  - At the end all $bookCount books are soft-deleted (IsDeleted, copies Retired) and stay in the tables."
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

    if (-not $script:CategoryId) {
        $r = Invoke-Api -Method POST -Path '/api/categories' -Token $script:LibToken -Body @{ name = "$Prefix-cat" }
        if ($r.Status -ne 201) { throw "Creating category '$Prefix-cat' failed (HTTP $($r.Status)): $($r.Text)" }
        $CreatedCategoryId = [int]$r.Json.id
        $script:CategoryId = $CreatedCategoryId
    }

    $mainBookId = New-SmokeBook -Title "$Prefix-main"
    Write-Host "== Created main book $mainBookId"

    #region Test 1: two members borrow the same copy

    Write-Host "== Test 1: two members borrow the same copy ($Rounds rounds)"
    for ($i = 1; $i -le $Rounds; $i++) {
        $copyId = New-SmokeCopy -BookId $mainBookId

        # Alternate who is sent first, so neither member always has the head start.
        $order = if ($i % 2) { @($members[0], $members[1]) } else { @($members[1], $members[0]) }
        $pair = Invoke-ApiPair `
            @{ Method = 'POST'; Path = '/api/loans'; Token = $order[0].Token; Body = @{ copyId = $copyId } } `
            @{ Method = 'POST'; Path = '/api/loans'; Token = $order[1].Token; Body = @{ copyId = $copyId } }
        Register-Loan $pair[0]
        Register-Loan $pair[1]

        $statuses = "$(Format-Status $pair[0]) / $(Format-Status $pair[1])"
        $winners = @(0, 1 | Where-Object { $pair[$_].Status -eq 201 })
        $losers = @(0, 1 | Where-Object { $pair[$_].Status -eq 409 })
        $pass = ($winners.Count -eq 1) -and ($losers.Count -eq 1)
        $detail = Get-ProblemTexts $pair

        if ($losers.Count -eq 1) {
            switch (Get-ConflictKind $pair[$losers[0]].Detail) {
                'version' { $Stats.BorrowVersion++ }
                'business' { $Stats.BorrowBusiness++ }
                default { $Stats.BorrowOther++ }
            }
        }

        # Every loan this round created is returned right away (also a stray second 201).
        foreach ($w in $winners) {
            $winner = $pair[$w]
            $winnerToken = $order[$w].Token

            if ($pass) {
                $g = if ($winner.Location) { Invoke-Api -Method GET -Path $winner.Location -Token $winnerToken } else { $null }
                $gStatus = if ($g) { $g.Status } else { 'no Location' }
                $statuses += ", GET Location $gStatus"
                if (-not $g -or $g.Status -ne 200) { $pass = $false }
            }

            $ret = Invoke-Api -Method PATCH -Path "/api/loans/$($winner.Json.id)" -Token $winnerToken
            $statuses += ", return $($ret.Status)"
            if ($ret.Status -ne 204) {
                $pass = $false
                $detail += " | return: $($ret.Text)"
            }
        }

        Add-Result -Name ("1.{0:D2} borrow race, copy {1}" -f $i, $copyId) -Pass $pass -Statuses $statuses -Detail $detail
    }

    #endregion

    #region Test 2: the same loan returned twice at once

    Write-Host '== Test 2: double return'
    $copyId = New-SmokeCopy -BookId $mainBookId
    $borrow = Invoke-Api -Method POST -Path '/api/loans' -Token $tokenA -Body @{ copyId = $copyId }
    Register-Loan $borrow
    if ($borrow.Status -ne 201) {
        Add-Result -Name "2.   double return, copy $copyId" -Pass $false -Statuses "borrow $($borrow.Status)" -Detail $borrow.Text
    } else {
        $loanId = [int]$borrow.Json.id
        $pair = Invoke-ApiPair `
            @{ Method = 'PATCH'; Path = "/api/loans/$loanId"; Token = $tokenA } `
            @{ Method = 'PATCH'; Path = "/api/loans/$loanId"; Token = $tokenA }

        $codes = @($pair | ForEach-Object Status | Sort-Object)
        $pass = ($codes -join ',') -eq '204,409'
        $loser = $pair | Where-Object Status -eq 409 | Select-Object -First 1
        if ($loser) {
            if ((Get-ConflictKind $loser.Detail) -eq 'version') { $Stats.ReturnVersion++ } else { $Stats.ReturnBusiness++ }
        }
        Add-Result -Name "2.   double return, loan $loanId" -Pass $pass `
            -Statuses "$(Format-Status $pair[0]) / $(Format-Status $pair[1])" -Detail (Get-ProblemTexts $pair)
    }

    #endregion

    #region Test 3: delete a book while its copy is being borrowed

    Write-Host "== Test 3: delete book vs. borrow its copy ($DeleteRounds rounds)"
    for ($i = 1; $i -le $DeleteRounds; $i++) {
        $bookId = New-SmokeBook -Title "$Prefix-del-$i"
        $copyId = New-SmokeCopy -BookId $bookId
        $member = $members[($i - 1) % 2]

        # Alternate which request is sent first.
        $deleteSpec = @{ Method = 'DELETE'; Path = "/api/books/$bookId"; Token = $script:LibToken }
        $borrowSpec = @{ Method = 'POST'; Path = '/api/loans'; Token = $member.Token; Body = @{ copyId = $copyId } }
        if ($i % 2) {
            $pair = Invoke-ApiPair $deleteSpec $borrowSpec
            $del = $pair[0]; $bor = $pair[1]
        } else {
            $pair = Invoke-ApiPair $borrowSpec $deleteSpec
            $bor = $pair[0]; $del = $pair[1]
        }
        Register-Loan $bor
        if ($del.Status -eq 204) { Set-BookDeleted $bookId }

        $pass = (($del.Status -eq 204) -and ($bor.Status -in 404, 409)) -or
                (($del.Status -eq 409) -and ($bor.Status -eq 201))
        $statuses = "delete $(Format-Status $del) / borrow $(Format-Status $bor)"
        $detail = Get-ProblemTexts $pair
        if ($del.Status -eq 204 -and $bor.Status -eq 201) {
            $detail = "BOTH SUCCEEDED: a loan exists for a copy of a deleted book. $detail"
        }

        foreach ($resp in (@($del, $bor) | Where-Object Status -eq 409)) {
            if ((Get-ConflictKind $resp.Detail) -eq 'version') { $Stats.DeleteRaceVersion++ } else { $Stats.DeleteRaceBusiness++ }
        }
        if ($bor.Status -eq 404) { $Stats.DeleteRaceBusiness++ }

        if ($bor.Status -eq 201) {
            $ret = Invoke-Api -Method PATCH -Path "/api/loans/$($bor.Json.id)" -Token $member.Token
            $statuses += ", return $($ret.Status)"
            if ($ret.Status -ne 204) {
                $pass = $false
                $detail += " | return: $($ret.Text)"
            }
        }

        Add-Result -Name ("3.{0:D2} delete vs. borrow, book {1}" -f $i, $bookId) -Pass $pass -Statuses $statuses -Detail $detail
    }

    #endregion

    #region Test 4: regression, retire a loaned copy

    Write-Host '== Test 4: retire a loaned copy'
    $copyId = New-SmokeCopy -BookId $mainBookId
    $steps = [System.Collections.Generic.List[string]]::new()
    $pass = $true
    $detail = ''

    $borrow = Invoke-Api -Method POST -Path '/api/loans' -Token $tokenB -Body @{ copyId = $copyId }
    Register-Loan $borrow
    $steps.Add("borrow $($borrow.Status)")
    if ($borrow.Status -ne 201) {
        $pass = $false
        $detail = $borrow.Text
    } else {
        $r = Invoke-Api -Method DELETE -Path "/api/copies/$copyId" -Token $script:LibToken
        $steps.Add("delete loaned $(Format-Status $r)")
        if ($r.Status -ne 409) { $pass = $false; $detail += " | $($r.Text)" }

        $r = Invoke-Api -Method PATCH -Path "/api/loans/$($borrow.Json.id)" -Token $tokenB
        $steps.Add("return $($r.Status)")
        if ($r.Status -ne 204) { $pass = $false; $detail += " | $($r.Text)" }

        $r = Invoke-Api -Method DELETE -Path "/api/copies/$copyId" -Token $script:LibToken
        $steps.Add("delete returned $($r.Status)")
        if ($r.Status -ne 204) { $pass = $false; $detail += " | $($r.Text)" }
    }
    Add-Result -Name "4.   retire loaned copy $copyId" -Pass $pass -Statuses ($steps -join ', ') -Detail $detail

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

    $hasSomethingToClean = ($CreatedBooks.Count -gt 0) -or ($null -ne $CreatedCategoryId) -or ($CreatedLoanIds.Count -gt 0)
    if ($hasSomethingToClean) {
        Write-Host '== Cleanup'
        try {
            # Safety net: active loans of the demo members on books of this run, even if their id was not recorded.
            foreach ($t in @($tokenA, $tokenB)) {
                if (-not $t) { continue }
                $r = Invoke-Api -Method GET -Path '/api/loans/me?ActiveOnly=true&PageSize=50' -Token $t
                if ($r.Status -eq 200) {
                    foreach ($l in @($r.Json.items)) {
                        if ($l -and "$($l.bookTitle)".StartsWith("$Prefix-")) { [void]$CreatedLoanIds.Add([int]$l.id) }
                    }
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

            foreach ($b in $CreatedBooks) {
                if ($b.Deleted) { continue }
                $r = Invoke-Api -Method DELETE -Path "/api/books/$($b.Id)" -Token $script:LibToken
                if ($r.Status -in 204, 404) {
                    $b.Deleted = $true
                    Write-Host "  soft-deleted book $($b.Id)"
                } else {
                    Write-Host "  cleanup: deleting book $($b.Id) failed ($($r.Status)): $($r.Text)" -ForegroundColor Red
                    $cleanupFailed = $true
                }
            }

            if ($null -ne $CreatedCategoryId) {
                $r = Invoke-Api -Method DELETE -Path "/api/categories/$CreatedCategoryId" -Token $script:LibToken
                if ($r.Status -eq 204) {
                    Write-Host "  deleted category $CreatedCategoryId"
                } else {
                    Write-Host "  cleanup: deleting category $CreatedCategoryId failed ($($r.Status)): $($r.Text)" -ForegroundColor Red
                    $cleanupFailed = $true
                }
            }
        } catch {
            Write-Host "  cleanup error: $($_.Exception.Message)" -ForegroundColor Red
            $cleanupFailed = $true
        }
    }

    $Client.Dispose()
    try { [Console]::OutputEncoding = $originalOutputEncoding } catch { }

    #endregion
}

if ($exitCode -eq 2) { exit 2 }

$passed = @($Results | Where-Object Pass).Count
$failed = $Results.Count - $passed
$versionHits = $Stats.BorrowVersion + $Stats.ReturnVersion + $Stats.DeleteRaceVersion

Write-Host ''
Write-Host '== Summary'
Write-Host "  Checks: $passed passed, $failed failed"
Write-Host "  Test 1 409s: $($Stats.BorrowVersion) version, $($Stats.BorrowBusiness) business ('already loaned'), $($Stats.BorrowOther) other"
Write-Host "  Test 2 409s: $($Stats.ReturnVersion) version, $($Stats.ReturnBusiness) business ('already returned')"
Write-Host "  Test 3 losers: $($Stats.DeleteRaceVersion) version, $($Stats.DeleteRaceBusiness) business (404 or 'has a loaned copy')"
Write-Host "  Version path caught: $versionHits time(s)"
if ($versionHits -eq 0) {
    Write-Warning 'The Version path was never hit. That depends on timing, so it is not a failure; try a larger -Rounds.'
}
Write-Host ("  Cleanup: {0}" -f $(if ($cleanupFailed) { 'FAILED' } else { 'OK' }))

if ($failed -gt 0 -or $cleanupFailed) { exit 1 }
exit 0
