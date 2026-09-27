# Lightweight audit after dotnet build/publish/run — fail open
$inputJson = [Console]::In.ReadToEnd()
try {
  $payload = $inputJson | ConvertFrom-Json
} catch {
  Write-Output '{"permission":"allow"}'
  exit 0
}

# Always allow; optionally surface non-zero exit for agent awareness via stderr
if ($null -ne $payload.exitCode -and [int]$payload.exitCode -ne 0) {
  [Console]::Error.WriteLine("PokeHex hook: previous dotnet command exited $($payload.exitCode)")
}

Write-Output '{"permission":"allow"}'
exit 0
