Function Get-Hash
{
  Param(
    [Parameter(Mandatory=$True,
               ValueFromPipeline=$True)]
    [String]
    $string
  )

  process {
    $stream = [IO.MemoryStream]::new([byte[]][char[]]$string)
    Get-FileHash -InputStream $stream -Algorithm MD5
  }
}

# First, generate the necessary tokens
$id = (New-Guid | Get-Hash).Hash.ToLowerInvariant()
$url = "http://localhost:5000"
$as_token = (New-Guid | Get-Hash).Hash.ToLowerInvariant()
$hs_token = (New-Guid | Get-Hash).Hash.ToLowerInvariant()
$localpart = "neofarkas"

Write-Output @"
id: "$id"
url: "$url"
as_token: "$as_token"
hs_token: "$hs_token"
sender_localpart: "$localpart"
namespaces:
  users:
    - exclusive: true
      regex: @neofarkas.*
"@