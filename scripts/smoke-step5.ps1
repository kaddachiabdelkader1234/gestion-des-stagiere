# End-to-end smoke test for step 5 (convention auto-generation + PDF download) through the gateway.
#
# Requires the stack to be up (docker compose up -d). Creates throwaway LEARNER/ADMIN/TRAINER
# accounts, accepts a candidature, then drives the convention lifecycle:
#   accept -> draft auto-created by the RabbitMQ consumer -> generate PDF -> download -> sign.
#
# Run:  powershell -ExecutionPolicy Bypass -File scripts/smoke-step5.ps1

$ErrorActionPreference = 'Stop'
$gw = 'http://localhost:18080'
$ts = Get-Date -Format 'yyyyMMddHHmmss'
$pw = 'Passw0rd!'
$failures = 0

function Invoke-Json {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null)
    $params = @{ Method = $Method; Uri = $gw + $Path; UseBasicParsing = $true }
    if ($Token) { $params.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $params.ContentType = 'application/json'
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
    }
    try {
        $r = Invoke-WebRequest @params
        return @{ Status = [int]$r.StatusCode; Body = $r.Content }
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { return @{ Status = 0; Body = ('NETWORK: ' + $_.Exception.Message) } }
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        return @{ Status = [int]$resp.StatusCode; Body = $reader.ReadToEnd() }
    }
}

function Invoke-Binary {
    param([string]$Path, [string]$Token)
    try {
        $r = Invoke-WebRequest -Method Get -Uri ($gw + $Path) -UseBasicParsing `
            -Headers @{ Authorization = "Bearer $Token" }
        $bytes = $r.Content
        if ($bytes -is [string]) { $bytes = [System.Text.Encoding]::UTF8.GetBytes($bytes) }
        return @{ Status = [int]$r.StatusCode; Bytes = $bytes; Type = $r.Headers['Content-Type'] }
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { return @{ Status = 0; Bytes = @(); Type = '' } }
        return @{ Status = [int]$resp.StatusCode; Bytes = @(); Type = '' }
    }
}

function Assert-Eq {
    param($Expected, $Actual, [string]$Label)
    if ($Expected -eq $Actual) {
        Write-Output ("  PASS  {0} = {1}" -f $Label, $Actual)
    }
    else {
        Write-Output ("  FAIL  {0}: expected {1}, got {2}" -f $Label, $Expected, $Actual)
        $script:failures++
    }
}

function New-Account {
    param([string]$Email, [string]$Role)
    $reg = Invoke-Json 'Post' '/api/v1/auth/register' @{
        firstName = 'Smoke5'; email = $Email; password = $pw
        phone = '+21600000000'; role = $Role; imageBase64 = $null; experience = 0
    } $null
    if ($reg.Status -ne 201 -and $reg.Status -ne 200) { throw ("register $Email failed: " + $reg.Body) }
    return ($reg.Body | ConvertFrom-Json)
}

Write-Output '=== Setup: accounts + accepted candidature ==='

$learnerEmail = "smoke5.learner.$ts@stb.tn"
$adminEmail   = "smoke5.admin.$ts@stb.tn"
$trainerEmail = "smoke5.trainer.$ts@stb.tn"

$learner = New-Account $learnerEmail 'LEARNER'
$admin   = New-Account $adminEmail   'ADMIN'
$trainer = New-Account $trainerEmail 'TRAINER'

$sub = Invoke-Json 'Post' '/api/v1/candidatures' @{
    nom = 'Convention'; prenom = 'Smoke'; email = $learnerEmail; departement = 'IT'
    typeStage = 'PFE'; ecole = 'ENIT'
    dateDebut = '2026-09-01'; dateFin = '2027-02-28'; motivation = 'Smoke test step 5'
} $learner.token
Assert-Eq 201 $sub.Status 'candidature submitted'
$stagiaireId = ($sub.Body | ConvertFrom-Json).id

$acc = Invoke-Json 'Post' ("/api/v1/candidatures/$stagiaireId/accepter") @{
    departement = 'IT'; encadrantId = $trainer.userId; encadrantNom = 'Smoke Trainer'
    dateDebut = '2026-09-01'; dateFin = '2027-02-28'
} $admin.token
Assert-Eq 200 $acc.Status 'candidature accepted'

Write-Output ''
Write-Output '=== 1. Draft convention auto-created by the CandidatureAccepted consumer ==='

# RabbitMQ delivery is asynchronous; poll rather than assuming it has landed.
$convention = $null
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Milliseconds 800
    $list = Invoke-Json 'Get' ("/api/v1/conventions?stagiaireId=$stagiaireId") $null $admin.token
    if ($list.Status -eq 200) {
        $page = $list.Body | ConvertFrom-Json
        if ($page.totalCount -ge 1) { $convention = $page.items[0]; break }
    }
}

if ($null -eq $convention) {
    Write-Output '  FAIL  no convention was auto-created within ~12s'
    Write-Output '        check: docker compose logs convention-service --tail 50'
    exit 1
}

$cid = $convention.id
Write-Output ("  convention id = {0}" -f $cid)
Assert-Eq 'EnAttente' $convention.statutSignature 'draft signature status'
Assert-Eq $false      $convention.pdfDisponible   'pdf not generated yet'
# The stagiaire details must be copied onto the row — this is what the missing migration broke.
Assert-Eq 'Smoke'     $convention.stagiairePrenom 'stagiaire prenom denormalised'
Assert-Eq 'IT'        $convention.departement     'departement denormalised'
Assert-Eq '2026-09-01' $convention.dateDebut      'dateDebut denormalised'

Write-Output ''
Write-Output '=== 2. Idempotence: the consumer must not create a second row ==='
$again = Invoke-Json 'Get' ("/api/v1/conventions?stagiaireId=$stagiaireId") $null $admin.token
Assert-Eq 1 (($again.Body | ConvertFrom-Json).totalCount) 'exactly one convention for this stagiaire'

Write-Output ''
Write-Output '=== 3. Authorization on writes (gateway restricts to ADMIN) ==='
$forbidGen = Invoke-Json 'Post' ("/api/v1/conventions/$cid/generer") @{} $learner.token
Assert-Eq 403 $forbidGen.Status 'learner cannot generate'
$forbidSign = Invoke-Json 'Post' ("/api/v1/conventions/$cid/signer") @{} $trainer.token
Assert-Eq 403 $forbidSign.Status 'trainer cannot sign'

Write-Output ''
Write-Output '=== 4. PDF not generated yet -> 404 on download ==='
$early = Invoke-Json 'Get' ("/api/v1/conventions/$cid/pdf") $null $admin.token
Assert-Eq 404 $early.Status 'download before generation'

Write-Output ''
Write-Output '=== 5. ADMIN generates the PDF ==='
$gen = Invoke-Json 'Post' ("/api/v1/conventions/$cid/generer") @{} $admin.token
Assert-Eq 200 $gen.Status 'generate'
if ($gen.Status -eq 200) {
    Assert-Eq $true (($gen.Body | ConvertFrom-Json).pdfDisponible) 'pdfDisponible after generation'
}
else {
    Write-Output ('        body: ' + $gen.Body)
}

Write-Output ''
Write-Output '=== 6. Download returns a real PDF ==='
$dl = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $admin.token
Assert-Eq 200 $dl.Status 'download'
if ($dl.Status -eq 200) {
    $magic = -join ($dl.Bytes[0..3] | ForEach-Object { [char]$_ })
    Assert-Eq '%PDF' $magic 'PDF magic bytes'
    if ($dl.Bytes.Length -gt 1000) {
        Write-Output ("  PASS  pdf size = {0} bytes" -f $dl.Bytes.Length)
    }
    else {
        Write-Output ("  FAIL  pdf suspiciously small: {0} bytes" -f $dl.Bytes.Length)
        $failures++
    }
}

Write-Output ''
Write-Output '=== 7. Regenerating replaces the PDF (no orphan, still downloadable) ==='
$regen = Invoke-Json 'Post' ("/api/v1/conventions/$cid/generer") @{} $admin.token
Assert-Eq 200 $regen.Status 'regenerate'
$dl2 = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $admin.token
Assert-Eq 200 $dl2.Status 'download after regenerate'

Write-Output ''
Write-Output '=== 8. PUT no longer requires cheminPdf (stale validator fix) ==='
$put = Invoke-Json 'Put' ("/api/v1/conventions/$cid") @{
    stagiaireId = $stagiaireId
    stagiaireNom = 'Convention'; stagiairePrenom = 'Smoke'; stagiaireEmail = $learnerEmail
    departement = 'IT'; dateDebut = '2026-09-01'; dateFin = '2027-02-28'
    dateGeneration = '2026-08-18'; statutSignature = 'EnAttente'
} $admin.token
Assert-Eq 204 $put.Status 'update without cheminPdf'
if ($put.Status -ne 204) { Write-Output ('        body: ' + $put.Body) }

Write-Output ''
Write-Output '=== 9. Reversed date range is rejected ==='
$bad = Invoke-Json 'Put' ("/api/v1/conventions/$cid") @{
    stagiaireId = $stagiaireId
    stagiaireNom = 'Convention'; stagiairePrenom = 'Smoke'; stagiaireEmail = $learnerEmail
    departement = 'IT'; dateDebut = '2027-02-28'; dateFin = '2026-09-01'
    dateGeneration = '2026-08-18'; statutSignature = 'EnAttente'
} $admin.token
Assert-Eq 400 $bad.Status 'dateFin before dateDebut'

Write-Output ''
Write-Output '=== 10. ADMIN marks it signed, twice -> 409 ==='
$sign = Invoke-Json 'Post' ("/api/v1/conventions/$cid/signer") @{} $admin.token
Assert-Eq 200 $sign.Status 'sign'
if ($sign.Status -eq 200) {
    Assert-Eq 'Signee' (($sign.Body | ConvertFrom-Json).statutSignature) 'status after signing'
}
$signAgain = Invoke-Json 'Post' ("/api/v1/conventions/$cid/signer") @{} $admin.token
Assert-Eq 409 $signAgain.Status 'signing twice'

Write-Output ''
Write-Output '=== 11. The learner can read their own convention ==='
$mine = Invoke-Json 'Get' ("/api/v1/conventions?stagiaireId=$stagiaireId") $null $learner.token
Assert-Eq 200 $mine.Status 'learner reads own convention'
if ($mine.Status -eq 200) {
    $minePage = $mine.Body | ConvertFrom-Json
    Assert-Eq 1 $minePage.totalCount 'learner sees their convention'
    Assert-Eq $true $minePage.items[0].pdfDisponible 'learner can see the pdf is available'
}
$mineDl = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $learner.token
Assert-Eq 200 $mineDl.Status 'learner downloads own convention pdf'

Write-Output ''
Write-Output '=== 12. Role scoping: nobody else can read or download it ==='
# Regression guard for the gap closed after step 5: GET /conventions is permitted for any
# authenticated user at the gateway, so the boundary has to be enforced inside the service.
# An out-of-scope id must return 404, not 403 — 403 would confirm the id exists.

$otherLearner = New-Account "smoke5.other.$ts@stb.tn" 'LEARNER'
$otherTrainer = New-Account "smoke5.trainer2.$ts@stb.tn" 'TRAINER'

$foreignList = Invoke-Json 'Get' '/api/v1/conventions' $null $otherLearner.token
Assert-Eq 200 $foreignList.Status 'other learner may call the list'
Assert-Eq 0 (($foreignList.Body | ConvertFrom-Json).totalCount) 'other learner sees no conventions'

# Explicitly asking for someone else's stagiaireId must not widen the scope.
$foreignFiltered = Invoke-Json 'Get' ("/api/v1/conventions?stagiaireId=$stagiaireId") $null $otherLearner.token
Assert-Eq 0 (($foreignFiltered.Body | ConvertFrom-Json).totalCount) 'other learner cannot filter into it'

$foreignById = Invoke-Json 'Get' ("/api/v1/conventions/$cid") $null $otherLearner.token
Assert-Eq 404 $foreignById.Status 'other learner reading by id'

$foreignPdf = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $otherLearner.token
Assert-Eq 404 $foreignPdf.Status 'other learner downloading the pdf'

$unassignedTrainer = Invoke-Json 'Get' '/api/v1/conventions' $null $otherTrainer.token
Assert-Eq 0 (($unassignedTrainer.Body | ConvertFrom-Json).totalCount) 'unassigned trainer sees none'

$unassignedPdf = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $otherTrainer.token
Assert-Eq 404 $unassignedPdf.Status 'unassigned trainer downloading the pdf'

Write-Output ''
Write-Output '=== 13. The assigned encadrant CAN read it ==='
$assigned = Invoke-Json 'Get' '/api/v1/conventions' $null $trainer.token
Assert-Eq 200 $assigned.Status 'assigned trainer may list'
Assert-Eq 1 (($assigned.Body | ConvertFrom-Json).totalCount) 'assigned trainer sees exactly their one'
$assignedPdf = Invoke-Binary ("/api/v1/conventions/$cid/pdf") $trainer.token
Assert-Eq 200 $assignedPdf.Status 'assigned trainer downloads the pdf'

Write-Output ''
if ($failures -eq 0) {
    Write-Output 'STEP 5 SMOKE TEST COMPLETE - ALL CHECKS PASSED'
}
else {
    Write-Output ("STEP 5 SMOKE TEST FAILED - {0} check(s) failed" -f $failures)
    exit 1
}
