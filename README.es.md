# Parental OpControl

[English](README.md) · [Español](README.es.md)

Control parental autoalojado para familias: un panel web para padres, un cliente de Windows para la computadora del niño y un instalador guiado, disponible en inglés y español. Construido desde cero con herramientas de IA para la planeación, la arquitectura y el código, con cada cambio revisado y probado por una persona.

**Versión actual: v0.17.29 · Beta.** Las funciones pueden cambiar antes de la versión estable.

![Demostración de Parental OpControl](assets/media/hero.gif)

### Mira la demostración completa (5:45)

[![Ver la demostración de Parental OpControl en YouTube](assets/media/video-thumb.jpg)](https://youtu.be/khIcOS5drO8)

El recorrido completo del panel para padres, la vista del niño y Luna AI está en YouTube (en inglés): https://youtu.be/khIcOS5drO8

## Para padres

| Panel familiar | Dispositivos y recursos en vivo |
|---|---|
| ![Panel familiar](assets/screenshots/parent-01-home-dark.png) | ![Dispositivos](assets/screenshots/parent-02-devices-dark.png) |

- Un panel por familia: dispositivos en línea, tiempo de pantalla de hoy, intentos bloqueados y el próximo bloqueo.
- Bloquear, desbloquear y dar tiempo extra (+15, +30 o una cantidad personalizada) desde el panel o la pantalla del niño.
- Límites diarios por dispositivo y por aplicación, con gráficas de día, semana y mes y una línea de actividad.

| Reglas y sitios web | Mensajes |
|---|---|
| ![Sitios web y reglas](assets/screenshots/parent-07-websites-dark.png) | ![Mensajes](assets/screenshots/parent-05-messages-dark.png) |

- Filtro de sitios con un resolutor DNS local, categorías curadas y tiempo extra por sitio.
- Mensajes cortos entre padres e hijos con estado entregado, visto y confirmado, agrupados por día.
- Vista remota de pantalla y cámara (iniciada por el padre, hasta 10 minutos, sin audio ni grabación).
- Alertas por manipulación, límites y advertencias del dispositivo, con correos bilingües y resumen diario.
- Copadres con permisos por padre, autenticación de dos factores y herramientas de administración del servidor.

| Luna AI para un niño | Revisión y seguridad de Luna |
|---|---|
| ![Luna AI](assets/screenshots/parent-03-luna-child-dark.png) | ![Revisión de Luna](assets/screenshots/parent-04-luna-review-dark.png) |

- Usa tu propia clave de IA. Cada pregunta y respuesta pasa por un filtro de seguridad antes de que el niño la vea.
- Los padres pueden leer cada chat, ver las fuentes web que usó Luna y revisar los temas marcados.
- Límite diario de mensajes, horas de silencio, retención cero en la configuración A y sin cuenta para el niño.

| Tema claro | Diseño para teléfono |
|---|---|
| ![Tema claro](assets/screenshots/parent-09-home-light.png) | ![Teléfono](assets/screenshots/parent-10-home-phone-dark.png) |

## Para niños

| My OpControl | Tema Moon gray |
|---|---|
| ![My OpControl](assets/screenshots/kid-11-home-night.png) | ![Tema Moon gray](assets/screenshots/kid-12-home-gray.png) |

- My OpControl muestra el tiempo restante de hoy, el próximo bloqueo, las aplicaciones más usadas y las reglas de la familia.
- Un diseño tranquilo de cielo nocturno, con una alternativa Moon gray, en inglés y español.
- Los niños pueden escribir a sus padres desde la ventana, la bandeja o la pantalla de bloqueo.

| Chat de Luna y fuentes | Luna busca en la web |
|---|---|
| ![Chat de Luna](assets/screenshots/kid-14-chat-sources-expanded.png) | ![Luna investigando](assets/screenshots/kid-15-detective-moon.png) |

- Luna explica con respuestas cortas y adecuadas a la edad, con imágenes, y puede mostrar “Dónde encontré esto”.
- La luna detective aparece mientras Luna investiga en la web.
- Los bloqueos y horarios también aplican a Luna.

## Próximamente: Juega con Luna

Los Space Boards convierten la práctica de matemáticas en pequeños juegos. Moon Tower, Orbit y Crater Clash están en revisión de diseño.

| Moon Tower | Orbit |
|---|---|
| ![Moon Tower](assets/screenshots/games-18-moon-tower.png) | ![Orbit](assets/screenshots/games-19-orbit.png) |

![Crater Clash](assets/screenshots/games-20-crater-clash.png)

LunarCraft viene después: **Critter Lab** (criar y cuidar criaturas lunares), **Critter Arena** (batallas amistosas que practican matemáticas) y **Lunar Harvest** (un pequeño ciclo de cultivo). Aún sin imágenes.

## Solución

| Proyecto | Propósito |
|---|---|
| `src/OpControl.Parental.Server` | ASP.NET Core 10, panel Blazor Server, API, hub SignalR para dispositivos, Identity, EF Core |
| `src/OpControl.Parental.Contracts` | DTOs y nombres de métodos del hub, independientes de la plataforma |
| `src/OpControl.Parental.Client.Windows` | Servicio de Windows: enrolamiento, credenciales, SignalR, latido y caché de estado sin conexión |
| `src/OpControl.Parental.Client.Windows.Helper` | Ayudante de la sesión del usuario: ventana de bloqueo a pantalla completa guiada por la caché |
| `src/OpControl.Parental.Setup` | Arranque del instalador (asistente que incluye el MSI) |
| `src/OpControl.Parental.Localization` | Textos compartidos en inglés y español para el cliente y el instalador |
| `tests/OpControl.Parental.Tests` | Pruebas de modelo, enrolamiento, credenciales y evaluación de bloqueos |

Marco de trabajo: **.NET 10 (LTS), SDK 10.0.402, runtime 10.0.13**.

## Compilar y probar en local

```bash
dotnet restore
dotnet build
dotnet test
```

## Instalar en tu propio servidor

Necesitas una máquina Linux con Docker Engine y el complemento Docker Compose,
un dominio y HTTPS (un proxy inverso o túnel delante del puerto web).

1. Descarga `docker-compose.yml`, `.env.example` y `LICENSE` de la última
   versión: <https://github.com/ziur-vela/parental-opcontrol/releases>.
2. Colócalos en una carpeta, por ejemplo `/opt/stacks/opcontrol-parental`.
3. Copia `.env.example` a `.env` y completa:
   - `POSTGRES_PASSWORD`: una contraseña larga y aleatoria.
   - `UPDATER_SECRET`: ejecuta `openssl rand -hex 32`.
   - `STACK_DIR`: la ruta absoluta de esta carpeta.
   - `PUBLIC_BASE_URL`: la dirección que usarán las familias, por ejemplo
     `https://parental.example.com`.
4. Inicia: `docker compose up -d`.
5. Lee el token de configuración en el registro:
   `docker compose logs web | grep 'FIRST-RUN SETUP'`.
6. Abre `https://tu-direccion/setup`, ingresa el token, crea el primer
   administrador del servidor, escanea el código QR con una aplicación de
   autenticación y, si quieres, agrega SMTP.
7. Agrega un dispositivo desde **Agregar dispositivo** e instala el cliente de
   Windows.

La página de configuración desaparece después de crear el primer administrador.
Las demás familias se unen por invitación desde Administración del servidor. El
contenedor web no tiene acceso al socket de Docker; solo el contenedor de
actualizaciones lo tiene.

## Actualizar

Administración del servidor → **Actualizaciones** busca versiones estables en
GitHub cada seis horas. Cuando haya una versión nueva, pulsa **Actualizar ahora**
(con un código de dos factores si tu cuenta usa 2FA). El actualizador:

1. respalda la base de datos con `pg_dump` (conserva los últimos cinco respaldos),
2. descarga la imagen firmada por digest,
3. recrea el contenedor web,
4. espera hasta tres minutos a que `/health` informe la versión nueva,
5. vuelve a la imagen anterior si la verificación de salud falla.

Los dispositivos de los niños se actualizan desde su propio servidor con el
flujo de actualización existente. Las versiones preliminares nunca se ofrecen en
el canal estable. Las instalaciones sin conexión pueden desactivar la búsqueda en
GitHub en la misma página.

Las notas de despliegue de nuestro homelab están en
[`docs/HOMELAB-DEPLOY.md`](docs/HOMELAB-DEPLOY.md).

## Cliente de Windows

- Instalador MSI y asistente; los padres agregan un dispositivo con un enlace de instalación de un solo uso o un código QR.
- Actualizaciones automáticas con verificación SHA-256 y reversión automática.
- Servicio SYSTEM más un ayudante en la sesión del usuario, con detección de manipulación y aplicación sin conexión desde una caché local.
- El servicio guarda la credencial del dispositivo en `C:\\ProgramData\\OpControlParental\\private\\device.json`, con una ACL que solo permite acceso a SYSTEM y Administradores. El ayudante solo lee `state.json` y nunca ve la credencial.
- `deploy/install-legacy.ps1` y `deploy/uninstall-legacy.ps1` quedan como referencia; los dispositivos nuevos deben usar el MSI.

## Créditos

Parental OpControl fue diseñado, dirigido y probado por Ziur, y construido con
ayuda de IA: diseño, planeación y revisión de código con **Claude** de
Anthropic, y código escrito con **OpenCode**, el agente de programación con IA
de código abierto, usando **DeepSeek V4.1 Flash** de DeepSeek. Cada cambio fue
revisado y probado por una persona antes de publicarse.

Creado por Ziur · OpControl IT. Los componentes de terceros, las fuentes y las
fuentes de datos están en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licencia

Parental OpControl es gratis para uso personal y otros usos no comerciales bajo
la [PolyForm Noncommercial License 1.0.0](LICENSE). Puedes usarlo, modificarlo y
compartirlo con fines no comerciales, y debes conservar el aviso requerido:

`Required Notice: Copyright (c) 2026 Ziur / OpControl IT Solutions (https://opcontrol.net)`

El uso comercial (venderlo, ofrecerlo como servicio pagado o usarlo dentro de una
empresa) requiere permiso por escrito de OpControl IT Solutions. Contacto:
`hendry.ruiz@opcontrol.net`.

El proyecto podría pasar a una licencia de código abierto (AGPL-3.0) si recibe
contribuciones. Al enviar una contribución aceptas que pueda distribuirse bajo
esta licencia y bajo cualquier licencia futura elegida por el titular de los
derechos, incluida AGPL-3.0. Consulta [CONTRIBUTING.md](CONTRIBUTING.md).

Los componentes de terceros, las fuentes y las fuentes de datos están en
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Migraciones

Genera una migración:

```bash
dotnet ef migrations add <Nombre> --project src/OpControl.Parental.Server --startup-project src/OpControl.Parental.Server --output-dir Data/Migrations
```

El servidor aplica las migraciones pendientes al iniciar.
