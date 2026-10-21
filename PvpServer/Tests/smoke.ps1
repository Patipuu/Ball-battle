param([string]$BaseUrl = 'http://127.0.0.1:5088')
$ErrorActionPreference = 'Stop'

function Register {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/accounts" -ContentType 'application/json' -Body '{}'
}

function Send($Token, $Method, $Route, $Body) {
    $args = @{ Method = $Method; Uri = "$BaseUrl$Route"; Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) { $args.ContentType = 'application/json'; $args.Body = ($Body | ConvertTo-Json -Depth 12 -Compress) }
    Invoke-RestMethod @args
}

$accounts = @((Register), (Register), (Register), (Register))
$balls = @('blade', 'fang', 'pike', 'brawler', 'volley', 'venom', 'aegis', 'rig', 'chorus', 'serpent') |
    ForEach-Object { @{ id = $_; level = 1; shards = 0; vit = 0; pow = 0; growth = 0; start = $false; skillUps = 0; branch = 0; finale = $false } }
$defense = @{ balls = @($balls); roster = @('blade', 'fang', 'pike'); banBall = 'serpent'; banArena = 'spike-walls' }
foreach ($account in $accounts) { $null = Send $account.token Put '/v1/me/defense' $defense }

$challenger = $accounts[0]
$offers = Send $challenger.token Get '/v1/offers' $null
if ($offers.opponents.Count -ne 3) { throw "Expected exactly 3 opponents, got $($offers.opponents.Count)." }
$chosen = $offers.opponents[0]
$challenge = Send $challenger.token Post '/v1/challenges' @{ offerId = $offers.offerId; opponentId = $chosen.id }
if ($challenge.opponent.PSObject.Properties.Name -contains 'roster' -or $challenge.opponent.PSObject.Properties.Name -contains 'banBall') {
    throw 'Defender roster or ban leaked in opponent profile.'
}
$hidden = Send $challenger.token Get "/v1/challenges/$($challenge.challengeId)" $null
if ($hidden.PSObject.Properties.Name -contains 'defenderBanBall') { throw 'Blind defender ban leaked.' }
$reveal = Send $challenger.token Post "/v1/challenges/$($challenge.challengeId)/ban" @{ ballId = 'aegis'; arenaId = 'low-gravity' }
if ($reveal.defenderBanBall -ne 'serpent') { throw 'Defender ban was not revealed after lock.' }
$result = Send $challenger.token Post "/v1/challenges/$($challenge.challengeId)/pick" @{ ballId = 'blade' }
$again = Send $challenger.token Post "/v1/challenges/$($challenge.challengeId)/pick" @{ ballId = 'fang' }
$me = Send $challenger.token Get '/v1/me' $null
if ($result.state -ne 'done' -or $again.delta -ne $result.delta -or $me.mmr -ne $result.mmrAfter) { throw 'Settlement was not idempotent.' }
if ($result.challengerProgress.Id -ne 'blade' -or $result.defenderProgress.Id -notin @('blade','fang','pike') -or $result.roundSeeds.Count -lt 2) {
    throw 'Replay progress or round seeds were not serialized.'
}
Write-Output "PASS: 3 offers, blind ban, server result, idempotent MMR $($result.mmrAfter) ($($result.delta))"
