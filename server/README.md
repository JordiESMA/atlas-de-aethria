# Anfitrión P2P de Atlas de Aethria

Este servidor de partida se ejecuta en el ordenador de un jugador. No hay un servidor central, sistema de cuentas, matchmaking ni relay. El anfitrión mantiene el proceso abierto y el otro jugador se conecta directamente a su dirección.

## Requisitos

- Windows para el lanzador incluido.
- Para jugar, el anfitrión no necesita Node.js instalado: Atlas incluye su runtime y el juego inicia y cierra el servicio automáticamente.
- `HOST-P2P.bat` sigue disponible como alternativa de diagnóstico para desarrolladores; requiere Node.js 18 o posterior.
- Ambos jugadores ejecutan la misma versión del juego.

## Pasos del anfitrión

1. Abre el juego → **Jugar online · P2P** → **Crear sala**. El proceso de anfitrión se abre desde el juego y genera el código.
2. Pulsa **Copiar enlace para invitar** y comparte el enlace `aethria://...`.
3. En Windows, el invitado abre Atlas una vez para registrar el tipo de enlace. Después, abrir la invitación con el juego instalado intenta entrar directamente en la sala.
4. Como alternativa, puede introducir la dirección y el código manualmente en **Unirse**.

## Jugar por Internet

El juego no depende de una nube, pero una conexión directa desde otra red requiere que la red del anfitrión acepte conexiones entrantes:

1. En Configuración de Windows, marca como privada la Wi-Fi de confianza antes de permitir Node.js. No habilites conexiones entrantes en perfiles públicos.
2. En redes distintas, reenvía TCP 3080 en el router hacia la IPv4 local del anfitrión. Los scripts opcionales `ABRIR_PUERTO_TCP_3080.bat` y `CERRAR_PUERTO_TCP_3080.bat` pueden gestionar la regla del firewall privado, pero requieren administrador.
3. Cambia la dirección que aparece en la sala por la IP pública antes de copiar el enlace. El router debe aceptar ese reenvío.

Al terminar la partida, puedes retirar esa regla de Windows con `CERRAR_PUERTO_TCP_3080.bat`, también como administrador. Ninguno de los scripts configura el router.

El proveedor de Internet puede usar CGNAT o bloquear conexiones entrantes; en ese caso el reenvío no funcionará. Se necesitaría una VPN de malla (por ejemplo, una red privada compartida) o desplegar un relay/servidor accesible. No se configura nada de eso automáticamente.

El código identifica la sala, pero no es una clave de cifrado ni atraviesa routers. El protocolo actual usa HTTP sin TLS y el servidor guarda salas/tokens solo en memoria. Úsalo en una red de confianza; no está endurecido para exposición pública.

## API

- `GET /api/health`: comprobación del proceso.
- `POST /api/rooms` con `{ "name": "..." }`: crea una sala y devuelve código/token.
- `POST /api/rooms/CODIGO/join` con `{ "name": "..." }`: incorpora al segundo jugador.
- `GET /api/rooms/CODIGO?token=...&after=VERSION&wait=1`: long polling hasta un cambio (máximo 20 segundos).
- `POST /api/rooms/CODIGO/actions`: envía una acción validada por el anfitrión.
- `GET /api/rooms/CODIGO/result?token=...`: devuelve estado final, ganador y puntuaciones.

Las salas expiran a las seis horas y desaparecen al cerrar el servidor.
