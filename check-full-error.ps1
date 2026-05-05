$cutoff = '2026-05-02T08:20:00'
Get-Content 'C:/Users/patri/AppData/Local/NoMercy_dev/log/log20260502_001.txt' | ForEach-Object {
    try {
        $entry = $_ | ConvertFrom-Json
        $t = $entry.'@t'
        $lvl = $entry.'@l'
        $msg = $entry.Message
        if ($t -gt $cutoff -and $lvl -eq 'Error') {
            Write-Host "=== $t ==="
            Write-Host $msg
            Write-Host ""
        }
    } catch {}
}
