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

    # Stop-Service a veces devuelve el control antes de que el proceso termine
    # de verdad (o directamente no lo mata) -- ya paso mas de una vez, y
    # "dotnet publish" fallaba porque el .exe/.dll seguian bloqueados.
    #
    # BUG ya corregido una vez y que volvio a pasar: la version anterior de
    # este bucle solo intentaba matar el proceso a la fuerza en el ULTIMO
    # segundo de espera, y cortaba el bucle ahi mismo sin volver a comprobar
    # si de verdad ya se habia cerrado -- el script seguia para adelante
    # igual, sin ningun margen. Ahora: si a mitad de camino (5s) sigue vivo,
    # se lo mata a la fuerza YA, se le siguen dando varios segundos mas para
    # que el sistema operativo termine de soltar el archivo, y al final se
    # vuelve a comprobar de verdad -- si sigue vivo, se corta el script con
    # un error claro en vez de chocar contra la pared de "publish" fallando.
    $exeBuscado = "JKalixto_System.Web"
    $maxEsperaSegundos = 15
    for ($intento = 1; $intento -le $maxEsperaSegundos; $intento++) {
        $procesoViejo = Get-Process -Name $exeBuscado -ErrorAction SilentlyContinue
        if (-not $procesoViejo) {
            break
        }
        if ($intento -eq 5) {
            Write-Host "El proceso viejo no se cerro solo - lo cierro a la fuerza." -ForegroundColor Yellow
            $procesoViejo | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 1
    }

    $procesoTodaviaVivo = Get-Process -Name $exeBuscado -ErrorAction SilentlyContinue
    if ($procesoTodaviaVivo) {
        throw "El proceso viejo (PID $($procesoTodaviaVivo.Id)) sigue corriendo despues de $maxEsperaSegundos segundos y no se pudo cerrar. Cerralo a mano con: Stop-Process -Id $($procesoTodaviaVivo.Id) -Force -- y volve a correr este script."
    }

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
