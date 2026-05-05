$token = (Get-Content 'C:/Users/patri/AppData/Local/NoMercy_dev/config/tokens.json' | ConvertFrom-Json).access_token
$baseUrl = 'https://192-168-2-201.fcbb730a-a562-2ca5-d054-5d6acf2a1aaa.nomercy.tv:7626'
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function PostJson($endpoint, $bodyStr, $timeoutMs = 120000) {
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
        return "ERROR [$statusCode]: $($webEx.Message) | $errBody"
    }
}

# S05E01 "His Butler, Doing Fieldwork" = episode ID 5798564
# Source: Vault NFS driver 01KQK5NFSRXDP6F83NZK1YA6WF
# Library: 01HQ5W2HMZ5QKDSXTTN9EQRERH (anime library)
# Folder: 01HQ5W4Y1ZHYZKS87P0AG24ERE

$episodeId = "5798564"
$nfsPath = "Download/complete/[Sokudo] Kuroshitsuji v3 [1080p AV1][Dual Audio]/Season 05 - Emerald Witch Arc/[Sokudo] Kuroshitsuji - Midori no Majo Hen - S05E01 v3 [1080p AV1][Dual Audio].mkv"

$addFilesBody = @"
{
    "library_id": "01HQ5W2HMZ5QKDSXTTN9EQRERH",
    "folder_id": "01HQ5W4Y1ZHYZKS87P0AG24ERE",
    "source_driver_id": "01KQK5NFSRXDP6F83NZK1YA6WF",
    "files": [
        {
            "id": "$episodeId",
            "path": "$nfsPath"
        }
    ]
}
"@

Write-Host "Dispatching encode for S05E01..."
$result = PostJson "$baseUrl/api/v1/dashboard/server/addfiles" $addFilesBody 30000
Write-Host "Response: $result"
