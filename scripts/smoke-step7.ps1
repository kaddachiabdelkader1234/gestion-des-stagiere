# End-to-end smoke test for step 7's security work (evaluation authorization + ownership scoping)
# through the gateway.
#
# Requires the stack to be up (docker compose up -d). Creates throwaway LEARNER/ADMIN/TRAINER
# accounts, gets a candidature accepted, then drives the evaluation lifecycle:
#   accepted candidature -> projection -> encadrant grades -> admin validates -> frozen.
#
# Assertions are on status codes and JSON fields only, never on the server's French message text:
# this file is read by Windows PowerShell 5.1, which decodes a BOM-less UTF-8 script as ANSI and
# would mangle any accented literal here.
#
# Run:  powershell -ExecutionPolicy Bypass -File scripts/smoke-step7.ps1

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
        firstName = 'Smoke7'; email = $Email; password = $pw
        phone = '+21600000000'; role = $Role; imageBase64 = $null; experience = 0
    } $null
    if ($reg.Status -ne 201 -and $reg.Status -ne 200) { throw ("register $Email failed: " + $reg.Body) }
    return ($reg.Body | ConvertFrom-Json)
}

function New-Candidature {
    param([string]$Email, $Learner, [string]$Nom)
    $sub = Invoke-Json 'Post' '/api/v1/candidatures' @{
        nom = $Nom; prenom = 'Smoke'; email = $Email; departement = 'IT'
        typeStage = 'PFE'; ecole = 'ENIT'
        dateDebut = '2026-06-01'; dateFin = '2026-11-30'; motivation = 'Smoke test step 7'
    } $Learner.token
    if ($sub.Status -ne 201) { throw ("candidature for $Email failed: " + $sub.Body) }
    return ($sub.Body | ConvertFrom-Json).id
}

Write-Output '=== Setup: accounts, accepted candidature, and a still-pending one ==='

$learnerEmail = "smoke7.learner.$ts@stb.tn"
$adminEmail   = "smoke7.admin.$ts@stb.tn"
$trainerEmail = "smoke7.trainer.$ts@stb.tn"
$pendingEmail = "smoke7.pending.$ts@stb.tn"

$learner = New-Account $learnerEmail 'LEARNER'
$admin   = New-Account $adminEmail   'ADMIN'
$trainer = New-Account $trainerEmail 'TRAINER'
$pending = New-Account $pendingEmail 'LEARNER'

$stagiaireId = New-Candidature $learnerEmail $learner 'Evaluation'
$pendingId   = New-Candidature $pendingEmail $pending 'Pending'

$acc = Invoke-Json 'Post' ("/api/v1/candidatures/$stagiaireId/accepter") @{
    departement = 'IT'; encadrantId = $trainer.userId; encadrantNom = 'Smoke Trainer'
    dateDebut = '2026-06-01'; dateFin = '2026-11-30'
} $admin.token
Assert-Eq 200 $acc.Status 'candidature accepted'

$other        = New-Account "smoke7.other.$ts@stb.tn"    'LEARNER'
$otherTrainer = New-Account "smoke7.trainer2.$ts@stb.tn" 'TRAINER'

Write-Output ''
Write-Output '=== 1. The assigned encadrant grades the stagiaire ==='
# The StagiaireAffectation projection is built from CandidatureAccepted over RabbitMQ, so the first
# attempt can land before the consumer has run. Poll rather than assuming.
$create = $null
for ($i = 1; $i -le 15; $i++) {
    $create = Invoke-Json 'Post' '/api/v1/evaluations' @{
        stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
        dateEvaluation = '2026-08-15'; note = 15.5
        commentaire = 'Bonne progression technique sur le premier trimestre.'
    } $trainer.token
    if ($create.Status -ne 404) { break }
    Start-Sleep -Milliseconds 800
}

Assert-Eq 201 $create.Status 'assigned trainer creates an evaluation'
if ($create.Status -ne 201) {
    Write-Output ('        body: ' + $create.Body)
    Write-Output '        check: docker compose logs evaluation-service --tail 50'
    Write-Output 'STEP 7 SMOKE TEST FAILED - cannot continue without an evaluation'
    exit 1
}

$eval = $create.Body | ConvertFrom-Json
$evalId = $eval.id
Write-Output ("  evaluation id = {0}" -f $evalId)
# Ownership must come from the projection, not the body — the DTO no longer carries any of these.
Assert-Eq $trainer.userId $eval.encadrantId    'encadrantId resolved server-side'
Assert-Eq $learner.userId $eval.utilisateurId  'utilisateurId resolved server-side'
Assert-Eq 'Evaluation'    $eval.stagiaireNom   'stagiaireNom from the projection'
Assert-Eq 'Smoke'         $eval.stagiairePrenom 'stagiairePrenom from the projection'
# Statut is set by the server so an encadrant cannot submit one already marked validated.
Assert-Eq 'Soumise'       $eval.statut         'statut forced to Soumise on create'
Assert-Eq 15.5            $eval.note           'note round-trips'

Write-Output ''
Write-Output '=== 2. One evaluation per type per stagiaire ==='
$dup = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-16'; note = 12
    commentaire = 'Deuxieme evaluation du meme type, doit etre refusee.'
} $trainer.token
Assert-Eq 409 $dup.Status 'duplicate type'

$finale = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'Finale'
    dateEvaluation = '2026-11-25'; note = 17
    commentaire = 'Stage complete avec un bon niveau d autonomie.'
} $trainer.token
Assert-Eq 201 $finale.Status 'a different type is allowed'

Write-Output ''
Write-Output '=== 3. Field validation ==='
# The note range was 0-99.9 while the whole system treats it as out of 20.
$badNote = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 25
    commentaire = 'Une note au-dessus de 20 doit etre refusee.'
} $trainer.token
Assert-Eq 400 $badNote.Status 'note above 20'

$shortComment = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 14
    commentaire = 'court'
} $trainer.token
Assert-Eq 400 $shortComment.Status 'commentaire under 10 characters'

Write-Output ''
Write-Output '=== 4. No stage, no evaluation ==='
$noStage = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $pendingId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 14
    commentaire = 'Candidature encore en attente, rien a evaluer.'
} $trainer.token
Assert-Eq 404 $noStage.Status 'evaluation on a pending candidature'

Write-Output ''
Write-Output '=== 5. An unassigned encadrant cannot grade someone else s stagiaire ==='
# 403 not 404: this is the hole that existed before — any trainer could grade anyone.
$foreignWrite = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 8
    commentaire = 'Note posee par un encadrant non assigne.'
} $otherTrainer.token
Assert-Eq 403 $foreignWrite.Status 'unassigned trainer creating'

Write-Output ''
Write-Output '=== 6. Role scoping on reads ==='
# Before this work the controller had no [Authorize] at all and no scoping, so any authenticated
# learner could list every evaluation in the bank.
$otherList = Invoke-Json 'Get' '/api/v1/evaluations' $null $other.token
Assert-Eq 200 $otherList.Status 'other learner may call the list'
Assert-Eq 0 (($otherList.Body | ConvertFrom-Json).totalCount) 'other learner sees none'

$otherFiltered = Invoke-Json 'Get' ("/api/v1/evaluations?stagiaireId=$stagiaireId") $null $other.token
Assert-Eq 0 (($otherFiltered.Body | ConvertFrom-Json).totalCount) 'other learner cannot filter into it'

$otherById = Invoke-Json 'Get' ("/api/v1/evaluations/$evalId") $null $other.token
Assert-Eq 404 $otherById.Status 'other learner reading by id'

$unassignedList = Invoke-Json 'Get' '/api/v1/evaluations' $null $otherTrainer.token
Assert-Eq 0 (($unassignedList.Body | ConvertFrom-Json).totalCount) 'unassigned trainer sees none'

$unassignedById = Invoke-Json 'Get' ("/api/v1/evaluations/$evalId") $null $otherTrainer.token
Assert-Eq 404 $unassignedById.Status 'unassigned trainer reading by id'

Write-Output ''
Write-Output '=== 7. The owner and the assigned encadrant can read ==='
$mine = Invoke-Json 'Get' '/api/v1/evaluations' $null $learner.token
Assert-Eq 200 $mine.Status 'learner lists own evaluations'
Assert-Eq 2 (($mine.Body | ConvertFrom-Json).totalCount) 'learner sees both of theirs'

$mineById = Invoke-Json 'Get' ("/api/v1/evaluations/$evalId") $null $learner.token
Assert-Eq 200 $mineById.Status 'learner reads own evaluation by id'

$trainerList = Invoke-Json 'Get' '/api/v1/evaluations' $null $trainer.token
Assert-Eq 2 (($trainerList.Body | ConvertFrom-Json).totalCount) 'assigned trainer sees exactly theirs'

# A filter must not be able to widen the scope past what the caller may see.
$widen = Invoke-Json 'Get' ("/api/v1/evaluations?encadrantId=" + $otherTrainer.userId) $null $trainer.token
Assert-Eq 0 (($widen.Body | ConvertFrom-Json).totalCount) 'trainer cannot filter into another roster'

$adminList = Invoke-Json 'Get' '/api/v1/evaluations' $null $admin.token
Assert-Eq 200 $adminList.Status 'admin lists'
if ((($adminList.Body | ConvertFrom-Json).totalCount) -ge 2) {
    Write-Output '  PASS  admin sees everything'
} else {
    Write-Output '  FAIL  admin should see at least the two created here'
    $failures++
}

Write-Output ''
Write-Output '=== 8. A learner cannot write (gateway restricts writes to TRAINER/ADMIN) ==='
$learnerWrite = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 20
    commentaire = 'Le stagiaire se met vingt sur vingt.'
} $learner.token
Assert-Eq 403 $learnerWrite.Status 'learner creating'

$learnerEdit = Invoke-Json 'Put' ("/api/v1/evaluations/$evalId") @{
    typeEvaluation = 'MiParcours'; dateEvaluation = '2026-08-15'; note = 20
    commentaire = 'Le stagiaire modifie sa propre note.'
} $learner.token
Assert-Eq 403 $learnerEdit.Status 'learner editing'

Write-Output ''
Write-Output '=== 9. Update is limited to the assigned encadrant ==='
$foreignEdit = Invoke-Json 'Put' ("/api/v1/evaluations/$evalId") @{
    typeEvaluation = 'MiParcours'; dateEvaluation = '2026-08-15'; note = 5
    commentaire = 'Modification par un encadrant non assigne.'
} $otherTrainer.token
Assert-Eq 404 $foreignEdit.Status 'unassigned trainer editing'

$edit = Invoke-Json 'Put' ("/api/v1/evaluations/$evalId") @{
    typeEvaluation = 'MiParcours'; dateEvaluation = '2026-08-15'; note = 16.5
    commentaire = 'Note revue apres le point hebdomadaire avec le stagiaire.'
} $trainer.token
Assert-Eq 204 $edit.Status 'assigned trainer edits'
if ($edit.Status -ne 204) { Write-Output ('        body: ' + $edit.Body) }

$afterEdit = Invoke-Json 'Get' ("/api/v1/evaluations/$evalId") $null $trainer.token
if ($afterEdit.Status -eq 200) {
    $edited = $afterEdit.Body | ConvertFrom-Json
    Assert-Eq 16.5 $edited.note 'edited note persisted'
    # A replacement PUT must not be able to null the columns visibility depends on.
    Assert-Eq $trainer.userId $edited.encadrantId   'encadrantId survived the PUT'
    Assert-Eq $learner.userId $edited.utilisateurId 'utilisateurId survived the PUT'
}

Write-Output ''
Write-Output '=== 10. Only an admin validates, and validation freezes the evaluation ==='
$trainerValide = Invoke-Json 'Post' ("/api/v1/evaluations/$evalId/valider") @{} $trainer.token
Assert-Eq 403 $trainerValide.Status 'trainer cannot self-validate'

$valide = Invoke-Json 'Post' ("/api/v1/evaluations/$evalId/valider") @{} $admin.token
Assert-Eq 200 $valide.Status 'admin validates'
if ($valide.Status -eq 200) {
    Assert-Eq 'Validee' (($valide.Body | ConvertFrom-Json).statut) 'statut after validation'
}

$valideAgain = Invoke-Json 'Post' ("/api/v1/evaluations/$evalId/valider") @{} $admin.token
Assert-Eq 409 $valideAgain.Status 'validating twice'

$frozen = Invoke-Json 'Put' ("/api/v1/evaluations/$evalId") @{
    typeEvaluation = 'MiParcours'; dateEvaluation = '2026-08-15'; note = 19
    commentaire = 'Tentative de modification apres validation.'
} $trainer.token
Assert-Eq 409 $frozen.Status 'editing a validated evaluation'

Write-Output ''
Write-Output '=== 11. Delete is admin-only ==='
$trainerDelete = Invoke-Json 'Delete' ("/api/v1/evaluations/$evalId") $null $trainer.token
Assert-Eq 403 $trainerDelete.Status 'trainer deleting'

$del = Invoke-Json 'Delete' ("/api/v1/evaluations/$evalId") $null $admin.token
Assert-Eq 204 $del.Status 'admin deletes'

$gone = Invoke-Json 'Get' ("/api/v1/evaluations/$evalId") $null $admin.token
Assert-Eq 404 $gone.Status 'deleted evaluation is gone'

# The freed type can be recorded again.
$reuse = Invoke-Json 'Post' '/api/v1/evaluations' @{
    stagiaireId = $stagiaireId; typeEvaluation = 'MiParcours'
    dateEvaluation = '2026-08-15'; note = 15
    commentaire = 'Evaluation de mi-parcours reprise apres suppression.'
} $trainer.token
Assert-Eq 201 $reuse.Status 'type reused after deletion'

Write-Output ''
Write-Output '=== 12. Unauthenticated access is refused ==='
$anon = Invoke-Json 'Get' '/api/v1/evaluations' $null $null
Assert-Eq 401 $anon.Status 'anonymous listing'

Write-Output ''
Write-Output ("stagiaireId = {0}" -f $stagiaireId)
if ($failures -eq 0) {
    Write-Output 'STEP 7 SMOKE TEST COMPLETE - ALL CHECKS PASSED'
}
else {
    Write-Output ("STEP 7 SMOKE TEST FAILED - {0} check(s) failed" -f $failures)
    exit 1
}
