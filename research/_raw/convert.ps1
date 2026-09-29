param(
    [Parameter(Mandatory=$true)][string]$In,
    [Parameter(Mandatory=$true)][string]$Out
)

function Dec {
    param([string]$s, [bool]$Trim = $true)
    if ($null -eq $s) { return '' }
    $s = $s -replace '(?s)<br\s*/?>', ' '
    $s = $s -replace '(?s)<strong[^>]*>(.*?)</strong>', '**$1**'
    $s = $s -replace '(?s)<em[^>]*>(.*?)</em>', '*$1*'
    $s = $s -replace '(?s)<code[^>]*>(.*?)</code>', '`$1`'
    $s = $s -replace '(?s)<img[^>]*alt="([^"]*)"[^>]*src="([^"]*)"[^>]*>', '![$1]($2)'
    $s = $s -replace '(?s)<img[^>]*src="([^"]*)"[^>]*>', '![]($1)'
    $s = $s -replace '(?s)<a[^>]*href="([^"]*)"[^>]*>(.*?)</a>', '$2'
    $s = $s -replace '(?s)<[^>]+>', ''
    $s = $s -replace '&quot;', '"'
    $s = $s -replace '&#0?39;', "'"
    $s = $s -replace '&lt;', '<'
    $s = $s -replace '&gt;', '>'
    $s = $s -replace '&nbsp;', ' '
    $s = $s -replace '&amp;', '&'
    if ($Trim) { return $s.Trim() }
    return $s
}

$c = [System.IO.File]::ReadAllText($In)
$c = [regex]::Replace($c, '(?s)<script.*?</script>', '')
$c = [regex]::Replace($c, '(?s)<style.*?</style>', '')

$i = $c.IndexOf('osu-md osu-md--wiki')
if ($i -lt 0) { throw "no wiki content found in $In" }
$a = $c.Substring($i)

# cut trailing chrome
$cuts = @()
foreach ($m in @('<footer', 'user-verification-popup')) {
    $p = $a.IndexOf($m)
    if ($p -ge 0) { $cuts += $p }
}
if ($cuts.Count -gt 0) { $a = $a.Substring(0, ($cuts | Measure-Object -Minimum).Minimum) }

$lines = $a -split "`r?`n"
$mdLines = [System.Collections.Generic.List[string]]::new()
$inPre = $false
$inTable = $false
$rows = [System.Collections.Generic.List[string]]::new()
$cells = [System.Collections.Generic.List[string]]::new()
$listStack = [System.Collections.Generic.List[string]]::new()

foreach ($line in $lines) {
    $t = $line.Trim()
    if ($t -eq '') { continue }

    # ---- code blocks ----
    if (-not $inPre -and $t -match '^<pre') {
        $rest = $t -replace '^<pre[^>]*>', '' -replace '^<code>', ''
        $mdLines.Add('```')
        $inPre = $true
        if ($rest -match '</code></pre>\s*$') {
            $rest = $rest -replace '</code></pre>\s*$', ''
            if ($rest -ne '') { $mdLines.Add((Dec $rest $false)) }
            $mdLines.Add('```')
            $inPre = $false
        } elseif ($rest -ne '') {
            $mdLines.Add((Dec $rest $false))
        }
        continue
    }
    if ($inPre) {
        if ($t -match '</code></pre>') {
            $rest = $t -replace '</code></pre>', ''
            if ($rest -ne '') { $mdLines.Add((Dec $rest $false)) }
            $mdLines.Add('```')
            $inPre = $false
        } else {
            $mdLines.Add((Dec $t $false))
        }
        continue
    }

    # ---- tables ----
    if ($t -match '^<table') { $inTable = $true; continue }
    if ($t -match '^</table>') {
        $inTable = $false
        for ($r = 0; $r -lt $rows.Count; $r++) {
            $mdLines.Add('| ' + $rows[$r] + ' |')
            if ($r -eq 0) {
                $n = ($rows[0] -split '\|').Count
                $mdLines.Add('|' + ((' --- |') * $n))
            }
        }
        $mdLines.Add('')
        $rows.Clear()
        continue
    }
    if ($inTable) {
        if ($t -match '^<tr') { $cells.Clear(); continue }
        if ($t -match '^</tr>') { $rows.Add(($cells -join ' | ')); continue }
        if ($t -match '^<(td|th)[^>]*>(.*)</(td|th)>$') { $cells.Add((Dec $matches[2])) ; continue }
        continue
    }

    # ---- headings ----
    if ($t -match '^<h([1-6])[^>]*>(.*)</h\1>$') {
        $lvl = [int]$matches[1]
        $mdLines.Add(('#' * $lvl) + ' ' + (Dec $matches[2]))
        $mdLines.Add('')
        continue
    }

    # ---- lists ----
    if ($t -match '^<(ul|ol)[^>]*>$') { $listStack.Add($matches[1]); continue }
    if ($t -match '^</(ul|ol)>$') {
        if ($listStack.Count -gt 0) { $listStack.RemoveAt($listStack.Count - 1) }
        continue
    }
    if ($t -match '^<li[^>]*>(.*)</li>$') {
        $depth = [Math]::Max(0, $listStack.Count - 1)
        $kind = if ($listStack.Count -gt 0) { $listStack[$listStack.Count - 1] } else { 'ul' }
        $bullet = if ($kind -eq 'ol') { '1.' } else { '- ' }
        $mdLines.Add(('  ' * $depth) + $bullet + ' ' + (Dec $matches[1]))
        continue
    }

    # ---- paragraphs ----
    if ($t -match '^<p[^>]*>(.*)</p>$') {
        $mdLines.Add((Dec $matches[1]))
        $mdLines.Add('')
        continue
    }

    # ---- anything else: strip and keep if non-empty ----
    $d = Dec $t
    if ($d -ne '') { $mdLines.Add($d); $mdLines.Add('') }
}

# collapse blank runs
$sb = [System.Text.StringBuilder]::new()
$blank = 0
foreach ($l in $mdLines) {
    if ($l.Trim() -eq '') {
        $blank++
        if ($blank -gt 1) { continue }
    } else { $blank = 0 }
    [void]$sb.AppendLine($l)
}
[System.IO.File]::WriteAllText($Out, $sb.ToString())
