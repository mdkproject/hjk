<#
    instalar-servicio.ps1
    ----------------------
    Instala JKalixto Web como Servicio de Windows, para que arranque solo con
    la PC (sin depender de dejar una ventana de terminal abierta con
    "dotnet run") y se reinicie solo si el proceso se cae.

    COMO CORRERLO:
      1. Abri PowerShell COMO ADMINISTRADOR (clic derecho -> "Ejecutar como
         administrador").
      2. Para cualquier "dotnet run" que tengas abierto para este proyecto.
      3. Ubicate en la carpeta JKalixto_System.Web (donde esta este script,
         dentro de deploy/) o corre el script con su ruta completa.
      4. Ejecuta:  .\deploy\instalar-servicio.ps1

    Que hace, paso a paso:
      1. Publica la app en modo Release (autocontenida, sin depender de tener
         el SDK de .NET instalado en la PC del servidor - solo el runtime).
      2. Registra el servicio de Windows "JKalixto Web" apuntando al .exe
         publicado.
      3. Configura reinicio automatico si el proceso se cae inesperadamente.
      4. Recuerda el paso pendiente del Firewall (no lo hace solo, porque ya
         se documento antes como un paso manual - ver el mensaje final).

    Para DESINSTALARLO mas adelante (si hiciera falta):
        Stop-Service "JKalixto Web"
        sc.exe delete "JKalixto Web"

    NOTA: este archivo se guarda a proposito sin tildes ni caracteres
    especiales (solo texto ASCII), porque Windows PowerShell 5.1 puede leer
    mal los acentos si el archivo no tiene BOM UTF-8, lo que rompe el script
    a mitad de una linea con errores de "parser" dificiles de entender.
#>

$ErrorActionPreference = "Stop"

$nombreServicio = "JKalixto Web"
$carpetaProyecto = Split-Path -Parent $PSScriptRoot   # .../JKalixto_System.Web
$carpetaRepo = Split-Path -Parent $carpetaProyecto    # .../hjk-Hotel-y-Sauna-Pos

# A PROPOSITO fuera de JKalixto_System.Web: si la publicacion queda DENTRO del
# proyecto, el SDK de .NET la detecta como si fuera codigo fuente propio en el
# siguiente "dotnet build" (archivos duplicados, error BLAZOR106) y rompe la
# compilacion normal del proyecto. Un bug real que ya paso una vez.
$carpetaPublicacion = Join-Path $carpetaRepo "publish-jkalixto-web"

Write-Host "== 1/4: Deteniendo el servicio si ya estaba corriendo ==" -ForegroundColor Cyan
# Tiene que ir ANTES de publicar: si el servicio ya esta corriendo, sus .dll
# quedan bloqueados y "dotnet publish" no puede sobrescribirlos (error real ya
# visto: MSB3021/MSB3026 "being used by another process").
$servicioExistente = Get-Service -Name $nombreServicio -ErrorAction SilentlyContinue
if ($servicioExistente) {
    Write-Host "El servicio '$nombreServicio' ya existe - se detiene para volver a publicarlo." -ForegroundColor Yellow
    Stop-Service $nombreServicio -Force -ErrorAction SilentlyContinue
    sc.exe delete $nombreServicio | Out-Null
    Start-Sleep -Seconds 2
}
else {
    Write-Host "No habia un servicio instalado todavia - se instala por primera vez." -ForegroundColor DarkGray
}

Write-Host "== 2/4: Publicando la app en modo Release ==" -ForegroundColor Cyan
dotnet publish $carpetaProyecto -c Release -o $carpetaPublicacion --self-contained false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo - revisa el error de arriba antes de seguir."
}

$exePublicado = Join-Path $carpetaPublicacion "JKalixto_System.Web.exe"
if (-not (Test-Path $exePublicado)) {
    throw "No se encontro $exePublicado despues de publicar - algo salio mal."
}

Write-Host "== 3/4: Registrando el servicio de Windows ==" -ForegroundColor Cyan
New-Service -Name $nombreServicio `
    -BinaryPathName "`"$exePublicado`"" `
    -DisplayName $nombreServicio `
    -Description "Sistema de Hotel y Sauna JKalixto - servidor web para la red local del hotel." `
    -StartupType Automatic

Write-Host "== 4/4: Configurando reinicio automatico si el proceso se cae ==" -ForegroundColor Cyan
# reset= 86400 (24h): despues de un dia sin fallas, se resetea el contador de reintentos.
sc.exe failure $nombreServicio reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

Start-Service $nombreServicio

Write-Host ""
Write-Host "Listo. El servicio '$nombreServicio' quedo instalado y arrancado." -ForegroundColor Green
Write-Host "Se va a iniciar solo cada vez que prenda esta PC, sin necesidad de dejar" -ForegroundColor Green
Write-Host "ninguna ventana abierta." -ForegroundColor Green
Write-Host ""
Write-Host "PENDIENTE (si todavia no lo hiciste): habilitar el puerto en el Firewall" -ForegroundColor Yellow
Write-Host "de Windows para que la laptop pueda conectarse por la red:" -ForegroundColor Yellow
Write-Host '    New-NetFirewallRule -DisplayName "JKalixto Web (5078)" -Direction Inbound -Protocol TCP -LocalPort 5078 -Action Allow -Profile Private' -ForegroundColor White
Write-Host ""
Write-Host "Para ver el estado del servicio:  Get-Service '$nombreServicio'" -ForegroundColor DarkGray
Write-Host "Para desinstalarlo:  Stop-Service '$nombreServicio'; sc.exe delete '$nombreServicio'" -ForegroundColor DarkGray
