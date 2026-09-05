# End-to-end smoke test for step 6 (journal de bord) through the gateway.
#
# Requires the stack to be up (docker compose up -d). Creates throwaway LEARNER/ADMIN/TRAINER
# accounts and drives the journal lifecycle:
#   accepted candidature -> learner writes weekly entries -> encadrant comments -> entry freezes.
#
# Assertions are on status codes and JSON fields only, never on the server's French message text:
# this file is read by Windows PowerShell 5.1, which decodes a BOM-less UTF-8 script as ANSI and
# would mangle any accented literal here.
#
# Run:  powershell -ExecutionPolicy Bypass -File scripts/smoke-step6.ps1

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
        firstName = 'Smoke6'; email = $Email; password = $pw
        phone = '+21600000000'; role = $Role; imageBase64 = $null; experience = 0
    } $null
    if ($reg.Status -ne 201 -and $reg.Status -ne 200) { throw ("register $Email failed: " + $reg.Body) }
    return ($reg.Body | ConvertFrom-Json)
}

function New-Candidature {
    param([string]$Email, $Learner, [string]$Nom)
    # A stage that has already started, so weekly entries are legitimately backdated. The validator
    # rejects an entry dated more than 7 days ahead, which a not-yet-started stage would require.
    $sub = Invoke-Json 'Post' '/api/v1/candidatures' @{
        nom = $Nom; prenom = 'Smoke'; email = $Email; departement = 'IT'
        typeStage = 'PFE'; ecole = 'ENIT'
        dateDebut = '2026-06-01'; dateFin = '2026-11-30'; motivation = 'Smoke test step 6'
    } $Learner.token
    if ($sub.Status -ne 201) { throw ("candidature for $Email failed: " + $sub.Body) }
    return ($sub.Body | ConvertFrom-Json).id
}

Write-Output '=== Setup: accounts, accepted candidature, and a still-pending one ==='

$learnerEmail = "smoke6.learner.$ts@stb.tn"
$adminEmail   = "smoke6.admin.$ts@stb.tn"
$trainerEmail = "smoke6.trainer.$ts@stb.tn"
$pendingEmail = "smoke6.pending.$ts@stb.tn"

$learner = New-Account $learnerEmail 'LEARNER'
$admin   = New-Account $adminEmail   'ADMIN'
$trainer = New-Account $trainerEmail 'TRAINER'
$pending = New-Account $pendingEmail 'LEARNER'

$stagiaireId = New-Candidature $learnerEmail $learner 'Journal'
$pendingId   = New-Candidature $pendingEmail $pending 'Pending'

$acc = Invoke-Json 'Post' ("/api/v1/candidatures/$stagiaireId/accepter") @{
    departement = 'IT'; encadrantId = $trainer.userId; encadrantNom = 'Smoke Trainer'
    dateDebut = '2026-06-01'; dateFin = '2026-11-30'
} $admin.token
Assert-Eq 200 $acc.Status 'candidature accepted'

$other        = New-Account "smoke6.other.$ts@stb.tn"    'LEARNER'
$otherTrainer = New-Account "smoke6.trainer2.$ts@stb.tn" 'TRAINER'

$week1 = '2026-08-03'
$week2 = '2026-08-10'

Write-Output ''
Write-Output '=== 1. No journal before the candidature is accepted ==='
$tooEarly = Invoke-Json 'Post' ("/api/v1/stagiaires/$pendingId/journal") @{
    dateEntree = $week1; texte = 'Should not be accepted while pending.'
} $pending.token
Assert-Eq 409 $tooEarly.Status 'entry on a pending candidature'

Write-Output ''
Write-Output '=== 2. The stagiaire writes a weekly entry ==='
$create = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week1; texte = 'Semaine 1: prise en main du projet et de la base de code.'
} $learner.token
Assert-Eq 201 $create.Status 'learner creates an entry'
if ($create.Status -ne 201) {
    Write-Output ('        body: ' + $create.Body)
    Write-Output 'STEP 6 SMOKE TEST FAILED - cannot continue without an entry'
    exit 1
}
$entry = $create.Body | ConvertFrom-Json
$entryId = $entry.id
Write-Output ("  entry id = {0}" -f $entryId)
Assert-Eq $week1 $entry.dateEntree     'dateEntree round-trips'
Assert-Eq $false $entry.estCommentee   'not commented yet'
Assert-Eq $true  $entry.modifiable     'editable while uncommented'
Assert-Eq $null  $entry.dateModification 'never edited'

Write-Output ''
Write-Output '=== 3. One entry per week: the same date twice is a conflict ==='
$dup = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week1; texte = 'Deuxieme entree pour la meme semaine.'
} $learner.token
Assert-Eq 409 $dup.Status 'duplicate week'

Write-Output ''
Write-Output '=== 4. Field validation ==='
# Nine characters: MinimumLength(10) is inclusive, so a 10-character text is valid and would be
# created, silently consuming $week2 and breaking every later count.
$short = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week2; texte = 'trop bref'
} $learner.token
Assert-Eq 400 $short.Status 'texte under 10 characters'

$farAhead = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = '2027-06-01'; texte = 'Une entree datee bien trop loin dans le futur.'
} $learner.token
Assert-Eq 400 $farAhead.Status 'dateEntree more than 7 days ahead'

Write-Output ''
Write-Output '=== 5. The learner reads their own journal ==='
$mine = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $learner.token
Assert-Eq 200 $mine.Status 'learner lists own journal'
if ($mine.Status -eq 200) {
    Assert-Eq 1 (($mine.Body | ConvertFrom-Json).totalCount) 'exactly one entry'
}
$byId = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $learner.token
Assert-Eq 200 $byId.Status 'learner reads the entry by id'

Write-Output ''
Write-Output '=== 6. The assigned encadrant can read it, but not write entries ==='
$trainerList = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $trainer.token
Assert-Eq 200 $trainerList.Status 'assigned trainer lists the journal'
if ($trainerList.Status -eq 200) {
    Assert-Eq 1 (($trainerList.Body | ConvertFrom-Json).totalCount) 'trainer sees the entry'
}

# 403 not 404: the trainer legitimately sees this stagiaire, so denying the id would be a lie.
$trainerWrites = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week2; texte = 'Entree ecrite par erreur par l encadrant.'
} $trainer.token
Assert-Eq 403 $trainerWrites.Status 'trainer cannot write an entry'

Write-Output ''
Write-Output '=== 7. The learner edits their entry while it is still uncommented ==='
$edit = Invoke-Json 'Put' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") @{
    dateEntree = $week1
    texte = 'Semaine 1 (corrigee): prise en main du projet, du depot et de la CI.'
} $learner.token
Assert-Eq 204 $edit.Status 'learner edits own entry'
if ($edit.Status -ne 204) { Write-Output ('        body: ' + $edit.Body) }

$afterEdit = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $learner.token
if ($afterEdit.Status -eq 200) {
    $edited = $afterEdit.Body | ConvertFrom-Json
    if ($edited.texte -like '*corrigee*') { Write-Output '  PASS  edited text persisted' }
    else { Write-Output '  FAIL  edited text not persisted'; $failures++ }
    if ($null -ne $edited.dateModification) { Write-Output '  PASS  dateModification stamped' }
    else { Write-Output '  FAIL  dateModification not stamped'; $failures++ }
}

Write-Output ''
Write-Output '=== 8. Role scoping: nobody else can see or touch this journal ==='
# The gateway permits GET/POST/PUT on /api/v1/stagiaires/*/journal/** for all three roles, so the
# boundary exists only inside the service. An out-of-scope stagiaire must 404, never 403.
$otherList = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $other.token
Assert-Eq 404 $otherList.Status 'other learner listing the journal'

$otherRead = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $other.token
Assert-Eq 404 $otherRead.Status 'other learner reading an entry'

$otherWrite = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week2; texte = 'Entree ecrite dans le journal de quelqu un d autre.'
} $other.token
Assert-Eq 404 $otherWrite.Status 'other learner writing an entry'

$otherEdit = Invoke-Json 'Put' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") @{
    dateEntree = $week1; texte = 'Modification par un tiers non autorise.'
} $other.token
Assert-Eq 404 $otherEdit.Status 'other learner editing an entry'

$unassignedList = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $otherTrainer.token
Assert-Eq 404 $unassignedList.Status 'unassigned trainer listing the journal'

$unassignedComment = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId/commentaire") @{
    commentaire = 'Commentaire d un encadrant non assigne.'
} $otherTrainer.token
Assert-Eq 404 $unassignedComment.Status 'unassigned trainer commenting'

Write-Output ''
Write-Output '=== 9. An entry id cannot be read through the wrong parent ==='
# Admin can see both stagiaires, so this isolates the parent-id check from the visibility check.
$crossParent = Invoke-Json 'Get' ("/api/v1/stagiaires/$pendingId/journal/$entryId") $null $admin.token
Assert-Eq 404 $crossParent.Status 'entry fetched under another stagiaire'

Write-Output ''
Write-Output '=== 10. The learner cannot delete; DELETE is admin-only at the gateway ==='
$learnerDelete = Invoke-Json 'Delete' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $learner.token
Assert-Eq 403 $learnerDelete.Status 'learner deleting an entry'

Write-Output ''
Write-Output '=== 11. The assigned encadrant comments, which freezes the entry ==='
$comment = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId/commentaire") @{
    commentaire = 'Bon demarrage, pense a documenter tes choix techniques.'
} $trainer.token
Assert-Eq 200 $comment.Status 'assigned trainer comments'
if ($comment.Status -eq 200) {
    $commented = $comment.Body | ConvertFrom-Json
    Assert-Eq $true  $commented.estCommentee 'estCommentee after commenting'
    Assert-Eq $false $commented.modifiable   'no longer editable'
    Assert-Eq $trainer.userId $commented.commentaireParId 'comment attributed to the trainer'
    if ($null -ne $commented.dateCommentaire) { Write-Output '  PASS  dateCommentaire stamped' }
    else { Write-Output '  FAIL  dateCommentaire not stamped'; $failures++ }
}
else {
    Write-Output ('        body: ' + $comment.Body)
}

$frozen = Invoke-Json 'Put' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") @{
    dateEntree = $week1; texte = 'Tentative de modification apres commentaire.'
} $learner.token
Assert-Eq 409 $frozen.Status 'editing a commented entry'

# The comment belongs to its author: re-posting refines it rather than conflicting.
$recomment = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId/commentaire") @{
    commentaire = 'Bon demarrage. Pense aussi a documenter tes choix techniques.'
} $trainer.token
Assert-Eq 200 $recomment.Status 'trainer refines their own comment'

Write-Output ''
Write-Output '=== 12. Filters ==='
$second = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week2; texte = 'Semaine 2: premiere fonctionnalite livree et testee.'
} $learner.token
Assert-Eq 201 $second.Status 'learner creates a second entry'

$all = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $learner.token
Assert-Eq 2 (($all.Body | ConvertFrom-Json).totalCount) 'two entries total'

# Newest week first, so the second entry leads.
if ($all.Status -eq 200) {
    Assert-Eq $week2 (($all.Body | ConvertFrom-Json).items[0].dateEntree) 'ordered newest week first'
}

$pendingReview = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal?sansCommentaire=true") $null $trainer.token
Assert-Eq 1 (($pendingReview.Body | ConvertFrom-Json).totalCount) 'only the uncommented entry'

$ranged = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal?du=$week2&au=$week2") $null $learner.token
Assert-Eq 1 (($ranged.Body | ConvertFrom-Json).totalCount) 'date range narrows to one week'

$emptyRange = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal?du=2026-01-01&au=2026-01-31") $null $learner.token
Assert-Eq 0 (($emptyRange.Body | ConvertFrom-Json).totalCount) 'date range with no entries'

Write-Output ''
Write-Output '=== 13. Admin sees everything and can delete ==='
$adminList = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $admin.token
Assert-Eq 200 $adminList.Status 'admin lists the journal'
Assert-Eq 2 (($adminList.Body | ConvertFrom-Json).totalCount) 'admin sees both entries'

$del = Invoke-Json 'Delete' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $admin.token
Assert-Eq 204 $del.Status 'admin deletes an entry'

$afterDelete = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $admin.token
Assert-Eq 1 (($afterDelete.Body | ConvertFrom-Json).totalCount) 'one entry left after delete'

$deleteAgain = Invoke-Json 'Delete' ("/api/v1/stagiaires/$stagiaireId/journal/$entryId") $null $admin.token
Assert-Eq 404 $deleteAgain.Status 'deleting the same entry twice'

Write-Output ''
Write-Output '=== 14. A freed week can be reused after its entry is deleted ==='
$reuse = Invoke-Json 'Post' ("/api/v1/stagiaires/$stagiaireId/journal") @{
    dateEntree = $week1; texte = 'Semaine 1 reecrite apres suppression par l administrateur.'
} $learner.token
Assert-Eq 201 $reuse.Status 'week reused after deletion'

Write-Output ''
Write-Output '=== 15. Unauthenticated access is refused ==='
$anon = Invoke-Json 'Get' ("/api/v1/stagiaires/$stagiaireId/journal") $null $null
Assert-Eq 401 $anon.Status 'anonymous listing'

Write-Output ''
Write-Output ("stagiaireId = {0}" -f $stagiaireId)
if ($failures -eq 0) {
    Write-Output 'STEP 6 SMOKE TEST COMPLETE - ALL CHECKS PASSED'
}
else {
    Write-Output ("STEP 6 SMOKE TEST FAILED - {0} check(s) failed" -f $failures)
    exit 1
}
