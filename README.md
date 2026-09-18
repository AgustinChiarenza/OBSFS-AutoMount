# OBSFS AutoMount

Monta un bucket de **Huawei Cloud OBS** como una unidad de disco de Windows, desde una sola
ventana. Es la automatización del instructivo manual de *rclone + WinFsp*: en vez de PowerShell,
asistentes interactivos y `schtasks`, se completan cuatro campos y se aprieta un botón.

[![build](https://github.com/AgustinChiarenza/OBSFS-AutoMount/actions/workflows/build.yml/badge.svg)](https://github.com/AgustinChiarenza/OBSFS-AutoMount/actions/workflows/build.yml)

## Descargar

**[⬇ OBSFS-AutoMount.exe](https://github.com/AgustinChiarenza/OBSFS-AutoMount/releases/latest)** — un único archivo de ~170 KB.

Se copia a cualquier Windows 10/11 y se ejecuta con doble clic. No hay instalador, no hay
carpeta de dependencias, no necesita permisos de administrador para funcionar. La primera vez
Windows muestra *"Windows protegió su PC"* porque el ejecutable no está firmado:
*Más información → Ejecutar de todas formas*.

---

## Uso

1. Doble clic en `OBSFS-AutoMount.exe`.
2. **Paso 1 — Requisitos**: clic en *Instalar faltantes*. Descarga rclone y WinFsp solo.
   WinFsp es un driver, así que Windows pide confirmación de administrador **una vez**.
3. **Paso 2 — Credenciales**: endpoint, AK y SK. Clic en *Probar conexión*: valida las claves y
   llena la lista de buckets disponibles. Elegir el bucket.
4. **Paso 3 — Unidad**: letra y etiqueta. Los valores de caché ya vienen con los recomendados.
5. Tildar **Montar al iniciar el equipo** si se quiere que aparezca sola en cada sesión.
6. **Montar ahora**. La unidad se abre en el Explorador.

A partir de ahí la unidad se comporta como cualquier otra: copiar, pegar, arrastrar, abrir
archivos desde otras aplicaciones.

### Endpoint

El combo trae los endpoints públicos más usados (Santiago, São Paulo, México, Singapur, etc.)
pero es editable: se puede escribir cualquier otro. Se acepta con o sin `https://`.

> El endpoint tiene que ser el de la región real del bucket. Si no coincide, el montaje falla o
> escribe en la región equivocada. Verificarlo en la consola de OBS.

---

## Qué hace por dentro

| Paso del instructivo manual | Qué hace la app |
|---|---|
| Descargar y descomprimir rclone, agregarlo al PATH | Lo baja a `%LOCALAPPDATA%\OBSFS-AutoMount\bin` y lo llama por ruta completa (no toca el PATH del sistema) |
| Descargar e instalar WinFsp | Baja el MSI oficial y lo instala con un único prompt de UAC |
| `rclone config` (asistente de 12 preguntas) | Los valores se inyectan como variables de entorno del proceso de rclone |
| `rclone lsd remote:` para verificar | Botón *Probar conexión*, con los errores traducidos a algo accionable |
| `rclone mount ...` con seis flags | Botón *Montar ahora* |
| `schtasks /Create /SC ONLOGON` | Checkbox *Montar al iniciar el equipo* |

Comando de montaje equivalente al que arma la app:

```
rclone mount obs:<bucket> <L>: --vfs-cache-mode writes --vfs-cache-max-size 50G
  --dir-cache-time 24h --cache-dir <cache> --volname "<etiqueta>" --rc --rc-no-auth --no-console
```

---

## Dónde queda todo

Todo vive bajo `%LOCALAPPDATA%\OBSFS-AutoMount\`:

| Archivo | Contenido |
|---|---|
| `bin\rclone.exe` | El binario de rclone que descargó la app |
| `settings.ini` | Configuración. La SK está cifrada, el resto en texto plano |
| `state.ini` | PID y puerto del montaje activo, para poder desmontarlo después |
| `cache\` | Caché de escritura de rclone (VFS) |
| `logs\mount.log` | Log de rclone — es lo que muestra *Ver registro* |

Para desinstalar: desmontar, destildar el inicio automático, borrar el `.exe` y esa carpeta.
WinFsp se quita aparte, desde *Agregar o quitar programas*.

---

## Seguridad de las credenciales

- La **Secret Key nunca se escribe en disco en texto plano**. Se guarda cifrada con DPAPI,
  con alcance de usuario: solo la cuenta de Windows que la guardó puede descifrarla, y solo en
  esa máquina. Copiar `settings.ini` a otra PC no sirve de nada.
- El `settings.ini` además queda con una ACL restringida al usuario actual.
- La SK **tampoco pasa por la línea de comandos** de rclone (que es visible para cualquier
  proceso del sistema) ni se guarda en `rclone.conf`. Se inyecta como variable de entorno del
  proceso hijo, que solo el propio usuario puede leer.
- El `rclone.conf` que crea la app queda vacío a propósito, para no pisar la configuración
  personal de rclone si el usuario ya lo usa para otra cosa.

---

## Inicio automático

El checkbox escribe un valor en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`:

```
"C:\ruta\OBSFS-AutoMount.exe" --autostart
```

Es por usuario, no requiere administrador, y se puede ver y desactivar desde
*Administrador de tareas → Inicio*. Si se mueve el `.exe` de carpeta, la app corrige
la ruta sola la próxima vez que se abre.

En modo `--autostart` la app no abre ventana: espera unos segundos a que la red esté lista,
monta la unidad (hasta 3 intentos) y se queda como **ícono en la bandeja del sistema**, con
menú para desmontar, abrir la unidad o abrir la ventana.

---

## Desmontar

- Botón *Desmontar*, o clic derecho en el ícono de la bandeja.
- Antes de bajar la unidad la app consulta cuántos archivos están subiendo todavía y avisa si
  hay pendientes. El cierre es limpio (vacía la cola), no un `taskkill`.
- Si se cierra la ventana con la unidad montada, la app pregunta si dejarla montada o desmontarla.
- Desde línea de comandos: `OBSFS-AutoMount.exe --unmount`.

> Con caché de escritura activada, un archivo copiado termina de subir a OBS **después** de que
> Windows dice que terminó el copiado. No apagar el equipo inmediatamente después de copiar
> archivos grandes. Lo que quede a medias se reanuda en el próximo montaje: la caché es persistente.

---

## Compilar desde el código

No hace falta Visual Studio, ni el SDK de .NET, ni NuGet. Se usa el compilador de C# que ya
viene con Windows (.NET Framework 4.x, presente en todo Windows 10/11).

```bash
powershell -ExecutionPolicy Bypass -File build.ps1
```

Sale en `dist\OBSFS-AutoMount.exe`. Con `-Run` además lo ejecuta.

Para regenerar el ícono: `powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 -Force`

### Estructura

```
src\
  Program.cs      Punto de entrada, instancia única, argumentos de línea de comandos
  MainForm.cs     Toda la interfaz y el ícono de bandeja
  Mounter.cs      Ejecución de rclone: prueba de conexión, montaje, desmontaje limpio
  Deps.cs         Detección, descarga e instalación de rclone y WinFsp
  AppConfig.cs    Configuración persistida
  Crypto.cs       Cifrado DPAPI de la Secret Key
  Startup.cs      Inicio automático (clave Run de HKCU)
  IniStore.cs     Almacén clave=valor
  AppPaths.cs     Rutas y log
  Theme.cs        Paleta y fábricas de controles
  LogForm.cs      Visor de logs
  Native.cs       Interop mínimo (traer al frente la instancia abierta)
assets\           Ícono y manifiesto
tools\            Generador de ícono, captura de pantalla
```

---

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| Windows muestra "Windows protegió su PC" | El `.exe` no está firmado digitalmente. *Más información → Ejecutar de todas formas*. |
| *SignatureDoesNotMatch* | La SK no corresponde a esa AK. Regenerar el par en *My Credentials → Access Keys*. |
| *NoSuchBucket* | El endpoint es de otra región que la del bucket. |
| *AccessDenied* al listar buckets | La clave está acotada a un bucket. Escribir el nombre del bucket a mano y probar de nuevo: la app valida contra ese bucket. |
| La unidad no aparece después de montar | *Ver registro*. Casi siempre es WinFsp faltante o el endpoint mal escrito. |
| La unidad no se montó al iniciar sesión | Revisar que el `.exe` no se haya movido, y *Administrador de tareas → Inicio*. |
| No se puede reinstalar rclone | Hay un montaje activo usando el binario. Desmontar primero. |

---

## Requisitos

- Windows 10 (1903+) u 11, x64 o ARM64.
- .NET Framework 4.x — ya viene con Windows, no hay que instalar nada.
- Conexión a internet en el primer uso (descarga de rclone y WinFsp).
- Permisos de administrador **una única vez**, solo para instalar el driver WinFsp.

## Créditos

Construido sobre [rclone](https://rclone.org/) y [WinFsp](https://winfsp.dev/), que hacen el
trabajo pesado. Esta app solo los orquesta.

Ninguno de los dos se redistribuye acá: la app los descarga de sus sitios oficiales en el
primer arranque. rclone es MIT, WinFsp es GPLv3 con excepción de enlace.

## Licencia

[MIT](LICENSE).
