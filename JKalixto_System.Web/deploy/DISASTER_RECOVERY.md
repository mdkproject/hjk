# Plan de recuperación ante desastres — JKalixto Web

Qué hacer si la base de datos se corrompe, se borra por error, o la PC que
corre el servidor se rompe. Pensado para que lo pueda seguir alguien sin
conocimiento previo del proyecto, paso a paso, con los comandos exactos.

## 0. Antes de que pase algo — qué hay que tener revisado de antemano

- **Verificar que los respaldos automáticos están corriendo de verdad.**
  Entrar a `http://<ip-del-servidor>:5078/salud` con un usuario
  Gerencia/Desarrollador. Ahí se ve la fecha del último respaldo y cuántas
  copias hay guardadas — si dice "⚠️ Atrasado" o "⚠️ Sin respaldos
  todavía", hay que resolverlo ANTES de que ocurra un desastre, no después.
- **Configurar la carpeta secundaria de respaldo** (muy recomendado). Sin
  ella, la única copia de seguridad vive en el mismo disco que la base real
  — un robo, un incendio o simplemente un disco que se rompe se lleva las
  dos cosas juntas. Se activa en
  `JKalixto_System.Web/appsettings.json`, sección `RespaldoBaseDeDatos`,
  poniendo algo en `"CarpetaSecundaria"` — lo más simple es apuntarla a una
  carpeta ya sincronizada por OneDrive/Google Drive/Dropbox que ya esté
  instalado en esa PC (por ejemplo
  `C:\Users\<usuario>\OneDrive\Respaldos-JKalixto`), así la copia también
  queda fuera del edificio sin que este sistema tenga que manejar ninguna
  contraseña ni credencial de nube. Después de cambiar el archivo, reiniciar
  el servicio (`Restart-Service "JKalixto Web"`) para que tome el cambio.
- **Saber dónde está todo:**
  - Base de datos real: `JKalixto_System.Web\Data\jkalixto.db`
    (ruta configurable en `appsettings.json` → `RutaBaseDeDatos`).
  - Respaldos: `JKalixto_System.Web\Data\Backups\` por defecto (o la que
    esté puesta en `RespaldoBaseDeDatos:Carpeta`), con nombres
    `jkalixto-AAAAMMDD-HHmmss.db`. Se genera uno nuevo cada 6 horas
    (configurable) y se guardan las últimas 30 copias (también
    configurable) — las viejas se borran solas.
  - Script de instalación del servicio:
    `JKalixto_System.Web\deploy\instalar-servicio.ps1`.

## 1. Restaurar desde un respaldo (el caso más común)

Cuándo usar esto: la base se corrompió, alguien borró datos por error, o el
archivo `jkalixto.db` desapareció/quedó dañado, pero la PC y Windows andan
bien.

1. Abrir PowerShell **como Administrador**.
2. Frenar el servicio para que nadie siga escribiendo en la base mientras se
   restaura:
   ```powershell
   Stop-Service "JKalixto Web"
   ```
3. Elegir el respaldo a restaurar. Lo normal es el más reciente, pero si el
   problema es un borrado o dato incorrecto cargado por error, puede
   convenir uno de unas horas antes de que pasara eso:
   ```powershell
   Get-ChildItem "C:\Users\MARCELO\source\repos\hjk-Hotel-y-Sauna-Pos\JKalixto_System.Web\Data\Backups" |
       Sort-Object CreationTime -Descending |
       Select-Object Name, CreationTime -First 10
   ```
4. Antes de reemplazar nada, **verificar que el respaldo elegido no está
   corrupto** (abrir un archivo roto solo cambiaría un problema por otro):
   ```powershell
   sqlite3 "<ruta-del-respaldo-elegido>.db" "PRAGMA integrity_check;"
   ```
   Tiene que devolver la palabra `ok`. Si `sqlite3` no está instalado en esa
   PC, se puede correr el mismo chequeo desde cualquier PC con el SDK de
   .NET instalado, usando el mismo motor que ya usa este proyecto (no hace
   falta instalar nada nuevo) — o simplemente probar el paso 6 con ese
   archivo primero en una carpeta aparte antes de pisar el original.
5. Guardar el archivo actual (aunque esté dañado) por las dudas, en vez de
   borrarlo directamente:
   ```powershell
   Rename-Item "C:\Users\MARCELO\source\repos\hjk-Hotel-y-Sauna-Pos\JKalixto_System.Web\Data\jkalixto.db" "jkalixto-antes-de-restaurar.db"
   ```
6. Copiar el respaldo elegido al lugar de la base real:
   ```powershell
   Copy-Item "<ruta-del-respaldo-elegido>.db" "C:\Users\MARCELO\source\repos\hjk-Hotel-y-Sauna-Pos\JKalixto_System.Web\Data\jkalixto.db"
   ```
   (Copiar un respaldo ya generado con `VACUUM INTO` es seguro porque es un
   archivo cerrado y consistente — el riesgo de corrupción que existe con
   `Copy-Item`/`cp` es solo cuando se copia la base **real, en uso**,
   mientras alguien la sigue escribiendo. Acá no es el caso.)
7. Volver a prender el servicio:
   ```powershell
   Start-Service "JKalixto Web"
   ```
8. Verificar en el navegador (`http://localhost:5078`) que el sistema
   arranca y los datos son los esperados — entrar a Recepción, Reservas,
   Gastos y confirmar que se ve lo que corresponde a la fecha del respaldo
   elegido. Avisarle al personal que cualquier movimiento cargado DESPUÉS de
   la hora de ese respaldo y ANTES de la restauración se perdió y hay que
   volver a cargarlo a mano (por eso el paso 3 conviene elegir el respaldo
   más nuevo posible).

## 2. La PC del servidor se rompió por completo (no prende, disco muerto, etc.)

Cuándo usar esto: hay que levantar el sistema en una PC distinta.

1. Conseguir el respaldo más reciente posible. Si estaba configurada la
   `CarpetaSecundaria` (ver sección 0), ahí debería estar disponible aunque
   la PC original no prenda más. Si no, y el disco viejo todavía se puede
   leer conectándolo como disco externo, buscar
   `Data\Backups\jkalixto-*.db` ahí.
2. En la PC nueva, clonar el repositorio (o copiar la carpeta del proyecto)
   y confirmar que tiene instalado el .NET Runtime/SDK necesario (ver
   `JKalixto_System.Web.csproj` para la versión — hoy net10.0).
3. Poner el respaldo elegido en `JKalixto_System.Web\Data\jkalixto.db`
   (mismo chequeo de integridad del paso 4 de la sección 1 antes de darlo
   por bueno).
4. Ajustar `appsettings.json` si la ruta cambió (`RutaBaseDeDatos`,
   `RespaldoBaseDeDatos:Carpeta`, `RespaldoBaseDeDatos:CarpetaSecundaria`).
5. Correr `JKalixto_System.Web\deploy\instalar-servicio.ps1` como
   Administrador (instala el servicio de Windows, lo deja arrancando solo
   con la PC — ver comentarios del propio script para más detalle).
6. Habilitar el puerto en el Firewall de Windows (el script lo recuerda al
   final):
   ```powershell
   New-NetFirewallRule -DisplayName "JKalixto Web (5078)" -Direction Inbound -Protocol TCP -LocalPort 5078 -Action Allow -Profile Private
   ```
7. Actualizar la IP/nombre que usan las demás PCs/celulares del hotel para
   conectarse (la URL cambia si la PC nueva tiene otra IP en la red local).

## 3. Qué NO hacer nunca

- **Nunca copiar `jkalixto.db` con `Copy-Item`/`cp`/explorador de archivos
  mientras el servicio está corriendo**, para sacar un respaldo manual "por
  las dudas". SQLite puede estar a mitad de una escritura en ese instante
  (un check-in, una venta) y la copia queda con el archivo a medio escribir
  — corrupta. Si hace falta un respaldo manual extra en el momento, frenar
  el servicio primero (`Stop-Service "JKalixto Web"`), copiar, y volver a
  prenderlo — o simplemente esperar al próximo respaldo automático, que sí
  usa un método seguro (`VACUUM INTO`) que no tiene este problema aunque el
  servicio siga corriendo.
- **Nunca restaurar un respaldo sin correr antes el chequeo de integridad**
  del paso 4 (sección 1) — restaurar un archivo ya corrupto solo cambia un
  problema por otro, y ahí sí se puede terminar perdiendo TODAS las copias
  buenas si se sigue "restaurando" sobre el mismo archivo dañado.
- **Nunca dejar pasar una alerta de "⚠️ Atrasado" en el Panel de Salud**
  (`/salud`) sin investigar por qué — significa que, si pasara un desastre
  hoy, se perdería más de un día de trabajo cargado a mano.
