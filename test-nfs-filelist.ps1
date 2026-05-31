$token = (Get-Content 'C:/Users/patri/AppData/Local/NoMercy_dev/config/tokens.json' | ConvertFrom-Json).access_token
$url = 'https://192-168-2-201.fcbb730a-a562-2ca5-d054-5d6acf2a1aaa.nomercy.tv:7626/api/v1/dashboard/server/filelist'
$addFilesUrl = 'https://192-168-2-201.fcbb730a-a562-2ca5-d054-5d6acf2a1aaa.nomercy.tv:7626/api/v1/dashboard/server/addfiles'
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function PostJson($endpoint, $bodyStr, $timeoutMs = 60000) {
    $req = [System.Net.WebRequest]::Create($endpoint)
    $req.Method = 'POST'
    $req.Headers.Add('Authorization', "Bearer $token")
    $req.ContentType = 'application/json'
    $req.Timeout = $timeoutMs
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($bodyStr)
    $req.ContentLength = $bytes.Length
    $s = $req.GetRequestStream()
    $s.Write($bytes, 0, $bytes.Length)
    $s.Close()
    try {
        $resp = $req.GetResponse()
        $rdr = New-Object System.IO.StreamReader($resp.GetResponseStream())
        return $rdr.ReadToEnd()
    } catch [System.Net.WebException] {
        $webEx = $_.Exception
        $statusCode = if ($webEx.Response) { [int]$webEx.Response.StatusCode } else { "no_response" }
        $errBody = ""
        if ($webEx.Response) {
            try {
                $rdr = New-Object System.IO.StreamReader($webEx.Response.GetResponseStream())
                $errBody = $rdr.ReadToEnd()
            } catch {}
        }
        return "EXCEPTION [$statusCode]: $($webEx.Message) | Body: $errBody"
    }
}

# Test filelist at multiple depths
$paths = @(
    'Download',
    'Download/complete',
    '[Sokudo] Kuroshitsuji v3 [1080p AV1][Dual Audio]',
    'Download/complete/[Sokudo] Kuroshitsuji v3 [1080p AV1][Dual Audio]/Season 05 - Emerald Witch Arc'
)

foreach ($p in $paths) {
    $body = '{"folder":"' + $p + '","type":"anime","driver_id":"01KQK5NFSRXDP6F83NZK1YA6WF"}'
    Write-Host "`nTesting path: $p"
    $result = PostJson $url $body 30000
    try {
        $obj = $result | ConvertFrom-Json
        Write-Host "  Files: $($obj.data.files.Count)"
        if ($obj.data.files.Count -gt 0) {
            $obj.data.files | Select-Object -First 3 | ForEach-Object { Write-Host "    - $($_.path) [$($_.id)]" }
        }
    } catch {
        Write-Host "  Raw response: $($result.Substring(0, [Math]::Min(300, $result.Length)))"
    }
}
