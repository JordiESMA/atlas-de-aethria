# Bitácora de desarrollo con IA

## Objetivo y requisitos de la actividad

El enunciado del trabajo pide un juego para varias personas, un tablero aleatorio, un objetivo de victoria, comunicación asíncrona entre equipos, un endpoint que determine el resultado, buena presentación/accesibilidad y documentación del uso de IA. La propuesta final es *Atlas de Aethria*: estrategia por turnos para dos jugadores, con 20 territorios, economía por edificios y conquista de la capital rival.

La modalidad online se diseñó como **P2P alojado por un jugador**: el anfitrión ejecuta el servidor de sala en su ordenador y comparte dirección de red, puerto y código de sala. No existe un servidor central. En una LAN el invitado usa la IPv4 local del anfitrión; por Internet hacen falta reglas de firewall y reenvío del puerto TCP 3080, y CGNAT puede bloquear conexiones entrantes. Se explica esta limitación en la guía porque un código de sala no resuelve la conectividad de red.

## Herramientas

- **OpenAI Codex**: apoyo para ideación, cambios de código, depuración, documentación y revisión. La conversación de Codex es la fuente para preparar una exposición de los intercambios; esta bitácora resume decisiones, no inventa respuestas literales ni transcripciones.
- **Unity 6.6.10f1**: motor y build de Windows.
- **Node.js 18 o posterior, módulos estándar**: servidor HTTP de la partida, sin paquetes externos.
- **PowerShell**: inspección y comandos locales de construcción/verificación.

## Evolución de la idea y cambios de dirección

1. Se exploró una propuesta inicial de minijuegos, pero el usuario pidió centrarse en un juego de estrategia por turnos inspirado en la expansión territorial de Catan.
2. El usuario fue afinando el aspecto: mapa de fantasía, territorios con forma reconocible y una construcción visible sobre su suelo (por ejemplo, sembrado y huerto), con números de producción legibles.
3. Se concretó una partida de dos reinos: producir recursos al tirar dados, comprar/mejorar territorios, entrenar/mover tropas y atacar la capital rival. El tablero actual es de 20 casillas en cuadrícula 5 × 4, con vecindad por lado compartido.
4. El usuario pidió expresamente una sola tirada al inicio de cada turno, que los terrenos activados permanezcan resaltados y que se pueda seguir actuando hasta pulsar **Terminar turno**. Se eliminaron del servidor las dos acciones máximas y el temporizador, y se retiró la expiración de turnos en local.
5. El último requisito de conexión del usuario fue “un p2p”: la solución se corrigió a conexión directa al ordenador anfitrión, sin servidor central. La app incluye un menú para crear/unirse con dirección y código; el lanzador `server/HOST-P2P.bat` inicia el proceso del anfitrión.

## Implementación

- `Assets/Scripts/Conquest/ConquestGame.cs`: reglas locales, producción, costes, niveles, movimiento, combate y puntuación.
- `Assets/Scripts/Conquest/ConquestPrototype.cs`: guía inicial, menús local/P2P, sala, tablero y controles.
- `Assets/Scripts/Conquest/ConquestNetworkClient.cs`: cliente HTTP asíncrono con long polling.
- `server/index.js`: servidor autoritativo que genera mapa/recursos, valida acciones, mantiene estado y expone resultado.
- `server/HOST-P2P.bat`: lanzador Windows del servidor local al anfitrión.
- `Docs/Guia_de_juego_Atlas_de_Aethria.pdf` y `.docx`: instrucciones, reglas y conexión directa.

## Tecnología y razonamiento

- Se usó HTTP long polling: el cliente mantiene un `GET` pendiente hasta que cambia la versión del estado y envía acciones mediante `POST`. Es una vía simple de comunicación asíncrona usando UnityWebRequest y Node estándar.
- El anfitrión valida turnos y acciones en el servidor que ejecuta localmente. El invitado no se conecta a un servicio desplegado por el proyecto.
- La rejilla de 5 × 4 usa adyacencia Manhattan (compartir lado) tanto en local como en servidor. La generación asigna 20 números de producción aleatorios y cuatro clases de terreno, con capitales fijas en extremos opuestos.
- No hay cuentas, base de datos, persistencia, cifrado TLS ni relay. Las salas y tokens viven en memoria. Es una versión para demostración en redes de confianza; la conectividad de Internet depende del router/ISP del anfitrión.

## Prompts y reformulaciones (resumen fiel, no citas literales)

- «Inspecciona el proyecto de Unity, encuentra errores y mejoras» llevó a revisar scripts y flujo de pantallas.
- «Quiero que el tablero sea más visual, de fantasía, con terrenos y edificios visibles» orientó la presentación territorial y el panel de datos.
- «Que se tire una vez, se resalten los hexágonos y pueda actuar hasta finalizar» fijó la regla de producción y los turnos sin límite de acciones.
- «Acaba el juego y deja una guía para jugar e instalar» motivó la documentación y el flujo inicial de ayuda.
- El enunciado del curso añadió juego en distintas máquinas y endpoint; el último ajuste del usuario concretó que el modo remoto debía ser P2P. Esto cambió la instrucción de conexión de “servidor online” a “anfitrión ejecutando su propio servidor”.

Para la exposición, conviene exportar la conversación desde Codex y seleccionar estos intercambios. La lista anterior es un resumen temático, no una transcripción literal de cada prompt.

## Problemas detectados y correcciones

- La primera versión de servidor limitaba el turno a dos acciones y a un minuto. No coincidía con la regla pedida de terminar el turno manualmente; ambas limitaciones se eliminaron.
- El servidor usaba vecindad hexagonal mientras el tablero del juego cuenta casillas cuadradas por lado; se alineó a adyacencia ortogonal.
- El modo online existía en scripts, pero no era accesible desde el menú principal; se añadió la pantalla P2P con entrada de dirección, nombre, código, creación y unión.
- Mostrar `127.0.0.1` al invitado no funcionaría en otra máquina; la sala ahora propone la IPv4 local del anfitrión y permite editar la dirección compartida.
- El nivel del territorio no se enviaba en el snapshot remoto; ahora el servidor lo incluye y el cliente lo aplica.

## Verificación y límites

Comprobaciones finales de esta entrega:

- `node --check server/index.js`: correcto.
- Prueba HTTP local: `/api/health`, sala y mapa de 20 territorios, incorporación del segundo jugador, tirada, dos compras seguidas en el mismo turno, rechazo de una compra diagonal, finalización manual del turno, rendición y resultado/ganador por `/result`: correcto.
- Build Windows final en Unity 6.6.10f1: `Build Finished, Result: Success`. Log en `Logs/P2PFinalBuild3.log`.
- Revisión visual de la build Windows a 1536 × 864: guía inicial, menú, conexión P2P, creación y espera de una sala, tablero, tirada de dados y resaltado. En la partida local se compró un territorio (nivel 1) y se construyó encima (nivel 2); el panel y las marcas N1/N2 mostraron el nivel esperado. Se ajustó contraste de títulos y la distribución del menú/código tras detectar problemas en la primera revisión.
- PDF regenerado a partir de la guía: dos páginas. Se renderizaron y revisaron ambas páginas; no se observaron textos cortados ni tabla desbordada.
- El render DOCX con LibreOffice no se pudo ejecutar porque `soffice.exe` no está instalado/disponible en el entorno. El DOCX sí se regeneró; su maquetación no queda verificada visualmente.
- El ZIP de entrega contiene el build, el servidor P2P, los scripts de apertura/cierre del firewall, la guía y la bitácora; se verificaron 167 entradas y los ficheros clave.
- Conexión desde un segundo ordenador por LAN e Internet: no verificada. Requiere ejecutar el anfitrión en una red real y, para Internet, configurar el router/firewall. CGNAT puede impedirla.
- No se hizo una partida larga con usuarios; la duración de 30 rondas y el balance de costes no se han validado.

La prueba HTTP en localhost comprueba reglas/endpoints, no demuestra que el juego sea accesible desde Internet ni que funcione en todos los tamaños de pantalla.

## Actualizacion visual y de invitaciones P2P - 24/11/2026

- Se incorporaron cinco texturas ilustradas de terreno para llanura, huerto, fábrica, mina y bosque. Cada terreno mantiene sus datos de producción, sus números, edificios 3D, tropas y niveles. La build se abrió a 1536 x 864 y se revisó el mapa de 20 casillas en partida local.
- El botón Crear sala inicia el Node incluido en StreamingAssets, espera la comprobación HTTP local y entonces crea el código. Node 24.13.0 se incluye con su licencia; no se requiere una instalación de Node separada. El proceso lo cierra el juego anfitrión.
- Al abrir Atlas en Windows se registra aethria:// para el usuario actual. El enlace de invitación lleva dirección y código; hay entrada manual como alternativa. Se comprobó la creación de una sala real y la respuesta GET /api/health en localhost. No se probó con un segundo ordenador ni por Internet.
- Windows mostró el aviso del firewall durante el arranque del servidor. El usuario indicó permitir solo redes privadas. En esta máquina las reglas que se observaron quedaron en perfil Public; el intento de cambiarlas requirió permisos de administrador y fue rechazado. Hay que corregir el perfil en Windows antes de usar esta build en una red pública.
- Una invitación no configura el router ni atraviesa NAT. La misma Wi-Fi puede conectarse usando la IPv4 local; fuera de la LAN hacen falta IP pública, TCP 3080 reenviado y una red/ISP que acepte conexiones entrantes. No hay relay, TLS ni cifrado de sala.
- Build Unity Windows en `Logs/P2PAutoInviteVisualBuild.log`: `ATLAS_BUILD_RESULT=Succeeded`. La primera compilación detectó que Unity no referencia Microsoft.Win32.Registry; se sustituyó por llamadas a la API nativa de Windows y la compilación posterior terminó correctamente.
- La guía PDF se regeneró a dos páginas y se inspeccionaron ambas imágenes. El render DOCX no se pudo verificar: el runtime del entorno no incluye LibreOffice y `render_docx.py` informa que falta `soffice.exe`.

## Revisión de la interfaz y del tablero

La petición de acercar el juego al render llevó a una vista ilustrada en perspectiva. Unity coloca cada parcela según sus coordenadas actuales y escoge su ilustración según el terreno, el propietario y el edificio. Los números, las tropas y los niveles se dibujan por encima de las ilustraciones con los valores de la partida. La costa y los castillos proceden de ilustraciones generadas con IA.

La portada explica los turnos. Al elegir nombres, cada jugador ve su capital, su escudo y su reino. En el tablero, la cartela de marfil indica producción, el escudo N1 o N2 indica nivel y la ficha T indica tropas. La pantalla de resultado muestra los nombres, los territorios y la puntuación de ambos jugadores.

La revisión visual utiliza el mismo código de dibujo de la entrega. En el ejecutable de revisión, una secuencia local selecciona origen y destino, compra una casilla, construye su edificio y llega al resultado por puntuación. Para revisar la espera de la sala se usa un estado de ejemplo. Las capturas de esta revisión no certifican una conexión entre dos equipos.

## Revisión de cuarteles, capitales y producción · 1.1.0

Se pidió a Codex aumentar la presencia de cuarteles, reforzar la capital de quien tira y relacionar el nivel de cada territorio con su rendimiento. La revisión encontró dos comportamientos que la interfaz anterior ocultaba. Un terreno propio de nivel 1 todavía no producía y el servidor podía dejar en nivel 0 una casilla neutral conquistada por combate.

- Cada mapa contiene 6 cuarteles, 4 minas, 4 huertos, 4 fábricas y 2 capitales. Se reserva una casilla vecina de cada capital para un cuartel neutral. Las ubicaciones y los números varían.
- La capital de quien tira gana 1 tropa, también con un 7. El cuartel da 2, 3 o 4 tropas en su propia casilla según el nivel. La mejora lineal permite leer el beneficio antes de gastar. Se eligió un máximo de tres niveles y un coste de 7 de oro y 3 materiales para la última mejora.
- Se verificaron 100 mapas y 6.000 tiradas en Unity, y otros 100 mapas y 6.000 tiradas en el servidor. Las pruebas cubren ambos propietarios, casillas neutrales, los doce rendimientos, el 7, compras, mejoras, recursos insuficientes, refuerzos, movimiento y victoria por conquista o por puntos.
- Dos ejecutables de revisión de Unity completaron una partida mediante una invitación al servidor del paquete en el mismo ordenador. Tras 60 turnos, los dos clientes mostraron empate a 42. La prueba de la API rechazó los turnos fuera de orden y las peticiones repetidas. Falta una prueba entre dos ordenadores físicos.
- Se inspeccionaron el tablero y el inspector a 1280 × 720 y 1024 × 768. Las capturas de los niveles muestran el mismo cuartel. Para fotografiar N3, la partida de revisión recibe el oro necesario después de fotografiar N2. Las pruebas de reglas comprueban los costes sin esa ayuda.

**Problema de equilibrio prioritario.** Se simularon 5.000 aperturas con las reglas del servidor. J1 recibe el refuerzo, recluta cuatro veces en la capital y ataca por la ruta (0,1), (1,1), (2,1), (3,1), (4,1), (4,2), repitiendo un combate mientras conserva al menos dos tropas. Ganó 4.234 veces antes del primer turno de J2 (84,7 %).

**Correcciones aplicadas (v1.2.0):**

- Se añadió `AttackReadyTurn`: un territorio recién comprado o conquistado no puede lanzar ataques hasta dos turnos después. Esto impide el rush en cadena.
- Se repitió la prueba de 5.000 aperturas con la misma estrategia de reclutamiento y ruta directa. Resultado: **0 victorias de J1 en la apertura (0,0 %)**, frente al 84,7 % anterior. La restricción bloquea eficazmente la cadena de ataques inmediatos.
- Los cuarteles iniciales de cada capital reciben números de producción simétricos (5 y 9, o 9 y 5), equilibrando el acceso a la frecuencia de activación.
- Se añadió una señal visual (banderín dorado) para distinguir el nivel 3 desde el tablero y el inspector, sin necesidad de seleccionar la casilla.
- La interfaz del inspector muestra la producción real según el nivel y los costes de mejora escalonados (N2: 5 oro + 2 mat.; N3: 7 oro + 3 mat.).
