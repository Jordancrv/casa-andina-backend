param(
    [string]$FrontendUrl = 'http://localhost:5173',
    [string]$RedirectUri = 'http://localhost:51141/api/calendario/google/callback'
)
$ErrorActionPreference = 'Stop'
$calendarProjectPath = Join-Path $PSScriptRoot '../src/CasaAndina.Api/CasaAndina.Api.csproj'
$calendarClientId = Read-Host 'OAuth Client ID (aplicación web)'
$calendarSecureSecret = Read-Host 'OAuth Client Secret (oculto)' -AsSecureString
$calendarSecretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($calendarSecureSecret)
try {
    $calendarClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($calendarSecretPointer)
    if ([string]::IsNullOrWhiteSpace($calendarClientId) -or [string]::IsNullOrWhiteSpace($calendarClientSecret)) {
        throw 'El Client ID y el Client Secret son obligatorios.'
    }
    $calendarSettings = @{
        'GoogleCalendar:ClientId' = $calendarClientId.Trim()
        'GoogleCalendar:ClientSecret' = $calendarClientSecret
        'GoogleCalendar:RedirectUri' = $RedirectUri
        'GoogleCalendar:FrontendUrl' = $FrontendUrl.TrimEnd('/')
    }
    $calendarSettings | ConvertTo-Json -Compress | dotnet user-secrets set --project $calendarProjectPath
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la configuración.' }
    Write-Host "Configuración guardada en los secretos locales de .NET. Reinicia la API."
    Write-Host "Registra esta URI de redirección en Google Cloud: $RedirectUri"
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($calendarSecretPointer)
    $calendarClientSecret = $null
    $calendarSettings = $null
}
