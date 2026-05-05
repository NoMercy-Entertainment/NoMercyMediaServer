$cutoff = '2026-05-02T08:19:30'
Get-Content 'C:/Users/patri/AppData/Local/NoMercy_dev/log/log20260502_001.txt' | ForEach-Object {
    try {
        $entry = $_ | ConvertFrom-Json
        $t = $entry.'@t'
        $lvl = $entry.'@l'
        $msg = $entry.Message
        if ($t -gt $cutoff) {
            $short = if ($msg) { $msg.Substring(0, [Math]::Min(300, $msg.Length)) } else { '' }
            $line = $t + ' [' + $lvl + '] ' + $short
            # Filter for encoder-relevant lines
            if ($line -match 'Encod|Analyz|nfs|NFS|ffprobe|stage|queue|Kuro|5798564|Error|Warning|failed|pipeline') {
                Write-Host $line
            }
        }
    } catch {}
}
