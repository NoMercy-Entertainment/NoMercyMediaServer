$cutoff = '2026-05-02T08:10:00'
Get-Content 'C:/Users/patri/AppData/Local/NoMercy_dev/log/log20260502_001.txt' | ForEach-Object {
    try {
        $entry = $_ | ConvertFrom-Json
        $t = $entry.'@t'
        $lvl = $entry.'@l'
        $msg = $entry.Message
        if ($t -gt $cutoff) {
            $short = if ($msg) { $msg.Substring(0, [Math]::Min(250, $msg.Length)) } else { '' }
            Write-Host ($t + ' [' + $lvl + '] ' + $short)
        }
    } catch {}
}
