# End-to-end smoke test for step 4 (candidature lifecycle) through the API gateway.
#
# Requires the stack to be up (docker compose up -d). Creates throwaway LEARNER/ADMIN/TRAINER
# accounts and one candidature, then drives submit -> track -> accept -> trainer roster.
#
# Run:  powershell -ExecutionPolicy Bypass -File scripts/smoke-step4.ps1

$ErrorActionPreference = 'Stop'
$gw = 'http://localhost:18080'
$ts = Get-Date -Format 'yyyyMMddHHmmss'
$pw = 'Passw0rd!'

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

function New-Account {
    param([string]$Email, [string]$Role)
    $reg = Invoke-Json 'Post' '/api/v1/auth/register' @{
        firstName = 'Smoke'; email = $Email; password = $pw
        phone = '+21600000000'; role = $Role; imageBase64 = $null; experience = 0
    } $null
    Write-Output ("[register] {0} ({1}) -> HTTP {2}" -f $Email, $Role, $reg.Status)
    return ($reg.Body | ConvertFrom-Json)
}

$learnerEmail = "smoke.learner.$ts@stb.tn"
$adminEmail   = "smoke.admin.$ts@stb.tn"
$trainerEmail = "smoke.trainer.$ts@stb.tn"

$learner = New-Account $learnerEmail 'LEARNER'
$admin   = New-Account $adminEmail   'ADMIN'
$trainer = New-Account $trainerEmail 'TRAINER'

# --- 1. LEARNER submits a candidature (always EnAttente) -------------------
$sub = Invoke-Json 'Post' '/api/v1/candidatures' @{
    nom = 'Smoke'; prenom = 'Learner'; email = $learnerEmail; departement = 'IT'
    typeStage = 'PFE'; ecole = 'ENIT'
    dateDebut = '2026-09-01'; dateFin = '2027-02-28'; motivation = 'Smoke test only'
} $learner.token
Write-Output ("[submit] learner -> HTTP {0} {1}" -f $sub.Status, $sub.Body)
if ($sub.Status -ne 201) { throw 'submit did not return 201' }
$cand = $sub.Body | ConvertFrom-Json
$cid = $cand.id
Write-Output ("[submit] id={0} statut={1}" -f $cid, $cand.statut)

# --- 2. LEARNER tracks own candidature -------------------------------------
$moi = Invoke-Json 'Get' '/api/v1/candidatures/moi' $null $learner.token
Write-Output ("[moi]   learner -> HTTP {0} statut={1}" -f $moi.Status, (($moi.Body | ConvertFrom-Json).statut))

# --- 3. LEARNER tries to accept own candidature (must be 403/404) ----------
$forbid = Invoke-Json 'Post' ("/api/v1/candidatures/$cid/accepter") @{
    departement = 'IT'; encadrantId = $trainer.userId; encadrantNom = 'Smoke Trainer'
    dateDebut = '2026-09-01'; dateFin = '2027-02-28'
} $learner.token
Write-Output ("[forbid] learner accept self -> HTTP {0} (expect 403)" -f $forbid.Status)

# --- 4. ADMIN lists TRAINER accounts for the encadrant dropdown ------------
$users = Invoke-Json 'Get' '/api/v1/auth/users?role=TRAINER' $null $admin.token
Write-Output ("[users] admin lists trainers -> HTTP {0} {1}" -f $users.Status, $users.Body)

# --- 5. ADMIN accepts (assign encadrant + departement) ---------------------
$acc = Invoke-Json 'Post' ("/api/v1/candidatures/$cid/accepter") @{
    departement = 'IT'; encadrantId = $trainer.userId; encadrantNom = 'Smoke Trainer'
    dateDebut = '2026-09-01'; dateFin = '2027-02-28'
} $admin.token
$accObj = $acc.Body | ConvertFrom-Json
Write-Output ("[accept] admin -> HTTP {0} statut={1} encadrant={2}" -f $acc.Status, $accObj.statut, $accObj.encadrantNom)

# --- 6. TRAINER lists stagiaires (must see only their assigned row) --------
$roster = Invoke-Json 'Get' '/api/v1/stagiaires' $null $trainer.token
$rosterObj = $roster.Body | ConvertFrom-Json
Write-Output ("[roster] trainer -> HTTP {0} total={1} first-statut={2}" -f $roster.Status, $rosterObj.totalCount, $rosterObj.items[0].statut)

Write-Output 'SMOKE TEST COMPLETE'
