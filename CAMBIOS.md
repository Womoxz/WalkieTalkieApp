# Walkie Talkie 2.1 — notas de la revisión

## Hablar con varios a la vez (2.1)

- La lista de contactos admite **selección múltiple**: con `Ctrl` marcas los que
  quieras y con `Mayús` un rango. El botón pasa a decir "hablar con N contactos".
- Arriba del todo aparece la fila **📢 Todos**, que envía a la lista entera de una
  vez. Es excluyente: al marcarla se desmarca el resto.
- Con varios elegidos, el panel derecho enseña a quién le va a llegar el mensaje
  antes de que hables.
- El audio se graba **una sola vez** y se envía por separado a cada destinatario
  (unicast), así que solo lo reciben los elegidos. En el historial aparece en la
  conversación de cada uno, apuntando al mismo archivo: no se duplica en disco.
  Se ve como "Tú → Daniel +2".
- A quien lo recibe le aparece marcado como **"(a varios)"**, para que sepa que no
  le hablaban solo a él.
- El nombre del archivo une los destinatarios con "+"; si son tantos que la ruta se
  pasaría del límite de Windows, se guarda como `Varios(N)_fecha.wav`.

> **Compatibilidad:** un mensaje enviado a **una sola** persona lo entiende también
> la 2.0. Los mensajes a **varios** usan un tipo de paquete nuevo, así que un equipo
> con la 2.0 los descartaría: para usar la difusión hay que tener la 2.1 en todos.

Ten en cuenta que enviar a N personas multiplica por N el tráfico de subida
(32 KB/s por destinatario). Con una decena de contactos en una red normal no se nota.

## Instalador

`installer\dist\WalkieTalkieVW_2.1.0_Setup.exe` (34,6 MB). Hecho con Inno Setup 6,
que ya estaba instalado en el equipo. El script es `installer\WalkieTalkie.iss`.

Para regenerarlo tras cambiar código:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\WalkieTalkie.iss
```

Qué hace al instalar:

- Se instala en `C:\WalkieTalkie` (la misma ruta de siempre) y **da permiso de
  escritura a los usuarios** sobre esa carpeta: la app guarda ahí la configuración,
  el usuario y los audios, y en `Archivos de programa` un usuario sin permisos de
  administrador no podría hacerlo.
- **No pisa `appsettings.json` si ya existe**, así que al actualizar no se pierden
  los contactos ni los ajustes de ese equipo.
- Abre los puertos **UDP 5000 y 5001 en el Firewall de Windows** (casilla marcada por
  defecto). Sin esto, Windows muestra un aviso en el primer arranque que un usuario
  sin permisos de administrador no puede aceptar, y el walkie-talkie se queda mudo.
- Si la aplicación está abierta, ofrece cerrarla en lugar de fallar (usa el mutex de
  instancia única).
- Casillas opcionales: acceso directo en el escritorio y **arranque automático con
  Windows** (se registra en HKLM, para el equipo entero, no para la cuenta del técnico
  que instala).
- Al desinstalar pregunta si borrar también los audios y la configuración; por
  defecto los conserva.

Requiere permisos de administrador para instalar (por el firewall y la carpeta).
Se puede desplegar en silencio con:

```
WalkieTalkieVW_2.1.0_Setup.exe /VERYSILENT /NORESTART
```

## Descubrimiento automático de contactos

Se ha incorporado —y ampliado— el autodescubrimiento que tenía la versión instalada
en `C:\WalkieTalkie` (compilada el 15/08/2025). **Se mantiene su mismo protocolo**
(`DISCOVER;nombre` y `RESPONSE;nombre` por broadcast UDP en el puerto 5001, cada 4 s),
así que los equipos que aún tengan aquella versión y los que tengan esta **se siguen
viendo en la lista**.

Lo que se añadió por encima de aquella implementación:

| Aquella versión | Ahora |
|---|---|
| Los contactos vivían solo en memoria: al reiniciar la lista salía vacía | Se guardan en `appsettings.json`, así que la lista está desde el arranque aunque los demás estén apagados |
| Abría un socket nuevo por cada datagrama recibido, solo para saber su propia IP | Se descarta el eco comparando el nombre; sin sockets extra |
| No había forma de saber quién estaba encendido | De los mismos latidos sale el estado **en línea / sin conexión** de cada contacto |
| Al cerrar, los demás tardaban en enterarse | Avisa al salir (`BYE;nombre`, que las versiones antiguas ignoran sin problema) |
| Siempre activo, sin ajustes | Se puede activar/desactivar y cambiar el puerto desde Configuración |

Probado contra la red real: partiendo de un `appsettings.json` **sin ningún contacto**,
la aplicación encontró sola a Jose, Daniel, Jennifer y Yeiner, con su IP correcta y
marcados como conectados.

> Las IPs que había guardadas en el archivo estaban desactualizadas (Daniel figuraba
> en `.30` y está en `.24`; Jennifer en `.46` y está en `.31`), y había gente que ni
> aparecía. Por eso `appsettings.json` se entrega con la lista vacía: se rellena sola
> y se corrige cuando el router cambia una dirección.

## ⚠️ Importante antes de instalar

**Hay que actualizar todos los equipos a la vez.** El formato de los datagramas cambió
(ahora llevan cabecera con el nombre de quien habla) y la calidad pasó de 44.1 kHz a
16 kHz. Un PC con la versión antigua y otro con la nueva **no se oirán entre sí**.

Los audios ya guardados se conservan y se siguen viendo en el historial.

## Qué se arregló

| # | Problema | Efecto que tenía |
|---|----------|------------------|
| 1 | El nombre de archivo se parseaba mal (`parts[1]` con formato `yyyyMMdd_HHmmss`) | El historial **nunca** cargaba al abrir la app |
| 2 | `udpSender` se creaba en cada pulsación y no se liberaba | Una fuga de socket por cada vez que hablabas |
| 3 | Los `.wav` recibidos se copiaban tras `Flush()` en vez de `Dispose()` | Cabecera RIFF con tamaño incorrecto: duración 0 en reproductores externos |
| 4 | Faltaba `sounds/f7.wav` y todos los `resources/*.png` | Sin pitido al hablar y sin ningún avatar |
| 5 | `SetUserImage` comparaba con `"facturacion"` sin tilde | Facturación nunca tenía avatar |
| 6 | Archivos guardados en ANSI en vez de UTF-8 | Se leía `"�Hola de nuevo, Jose!"` en pantalla |
| 7 | Rutas basadas en el directorio de trabajo | La app fallaba al abrirse desde un acceso directo |
| 8 | `PlaybackStopped +=` en cada aviso sonoro | Handlers acumulados sin liberar |
| 9 | El hook devolvía siempre `1` | **F7 quedaba inutilizable en todo Windows** |
| 10 | `while(...) Application.DoEvents()` al reproducir | La ventana se congelaba y se podía reentrar |
| 11 | `Math.Abs(short.MinValue)` en el medidor | Desbordaba y mataba la captura de audio |

## Qué se añadió

**Interfaz**
- Lista de contactos con **estado en línea** (latidos cada 3 s) y globo de audios sin escuchar.
- Historial que distingue enviados de recibidos, con hora y duración; doble clic reproduce, `Supr` borra.
- Medidor de nivel de micrófono, indicador **AL AIRE** parpadeante y barra de estado.
- Ventana redimensionable de verdad (antes los controles se quedaban clavados arriba a la izquierda).
- Icono en la bandeja del sistema, con avisos al recibir audio con la ventana cerrada.
- Tema oscuro coherente en las cuatro ventanas y avatares generados con la inicial cuando no hay PNG.
- Textos de ayuda cuando no hay contactos o no hay audios.

**Configuración (⚙)**
- Alta, edición y borrado de contactos sin tocar el JSON.
- Elección de micrófono y altavoces, volumen, calidad y sonidos de aviso.
- Tecla de hablar configurable y opción de bloquearla o no en el resto de programas.
- Puerto, retención de audios, corte de seguridad y cambio de usuario.
- Copia de seguridad (`appsettings.json.bak`) y escritura atómica al guardar.

**Red y audio**
- Un solo socket UDP para enviar y recibir.
- 16 kHz y paquetes de 40 ms: **de 88 KB/s a 32 KB/s** y sin fragmentación IP (antes cada
  paquete de 8.8 KB se partía en 6 y perder uno estropeaba el bloque entero).
- Micrófono siempre abierto: ya no se pierde el arranque de cada frase.
- Mezclador por remitente: dos personas hablando a la vez ya no se pisan.
- Se ignora el audio de equipos que no estén en la lista de contactos.
- Corte automático si la tecla se queda pegada (por defecto 60 s).
- Al iniciar sesión se registra la IP real del equipo, así el DHCP deja de romper la lista.

**Mantenimiento**
- Instancia única: abrir el .exe otra vez restaura la ventana en lugar de duplicar la app.
- Borrado automático de audios con más de 30 días (configurable).
- Logs en `%APPDATA%\WalkieTalkie\logs` en vez de junto al ejecutable.
- DPI por monitor: se acabó el texto borroso al 125 %.
- Compila con **0 avisos** (antes 251).

## Archivos retirados

En `_backup_original/`: `KeyboardHook.cs` y `SafeWaveIn.cs` (código muerto, nunca se usaban),
`ThreadSafeFlag.cs` (ya no hace falta), y los `.resx` huérfanos —`MainForm.resx` pesaba
105 KB de recursos que no se referenciaban desde ningún sitio.

## Recursos recuperados

Los avatares y el pitido de hablar que faltaban se han copiado desde la instalación de
`C:\WalkieTalkie`: `f7.wav` y `daniel/jennifer/jose/leidy.png`. Como el código busca la
imagen por el nombre del usuario, `leidy.png` está también como `leydy.png`. Para añadir
a alguien basta con dejar su foto en `resources` con su nombre en minúsculas; si no hay
foto se genera un avatar con su inicial.

Siguen sin existir `bodega.png`, `pcprueba.png`, `facturacion.png` y `yeiner.png`.

## Pendiente / ideas

- Comprimir el audio (Opus/G.711) bajaría el consumo de red otro 80 %.
- Grupos o canal general para hablar con todos a la vez.
- El descubrimiento no cruza subredes (el broadcast no pasa del router). Para equipos de
  otra sede hay que añadirlos a mano en Configuración, que se sigue pudiendo.
