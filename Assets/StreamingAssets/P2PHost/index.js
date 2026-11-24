const http = require('node:http');
const crypto = require('node:crypto');

const PORT = Number(process.env.PORT || 3080);
const MAX_TURNS = 60;
const ROOMS = new Map();
const LAND = ['farm', 'factory', 'mine', 'barracks'];
const PRODUCTION_NUMBERS = [2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 4, 5, 6, 8, 9, 3, 10, 5, 6, 8];

function json(res, status, data) {
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'access-control-allow-origin': '*',
    'access-control-allow-methods': 'GET,POST,OPTIONS',
    'access-control-allow-headers': 'content-type'
  });
  res.end(JSON.stringify(data));
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', chunk => { raw += chunk; if (raw.length > 16_384) reject(new Error('Cuerpo demasiado grande')); });
    req.on('end', () => {
      try { resolve(raw ? JSON.parse(raw) : {}); }
      catch { reject(new Error('JSON inválido')); }
    });
    req.on('error', reject);
  });
}

function makeRoom(name) {
  let code;
  do { code = crypto.randomBytes(3).toString('hex').toUpperCase(); } while (ROOMS.has(code));
  const board = [];
  const numbers = [...PRODUCTION_NUMBERS];
  for (let i = numbers.length - 1; i > 0; i--) { const j = crypto.randomInt(i + 1); [numbers[i], numbers[j]] = [numbers[j], numbers[i]]; }
  let numberIndex = 0;
  for (let x = 0; x < 5; x++) for (let y = 0; y < 4; y++) {
    board.push({ x, y, land: LAND[crypto.randomInt(LAND.length)], productionNumber: numbers[numberIndex++], owner: -1, troops: 0, hasBuilding: false, level: 0 });
  }
  const at = (x, y) => board.find(z => z.x === x && z.y === y);
  Object.assign(at(0, 1), { land: 'capital', productionNumber: 0, owner: 0, troops: 4, hasBuilding: true, level: 2 });
  Object.assign(at(4, 2), { land: 'capital', productionNumber: 0, owner: 1, troops: 4, hasBuilding: true, level: 2 });
  const room = {
    code, createdAt: Date.now(), status: 'waiting', board,
    players: [{ name: String(name || 'Jugador azul').slice(0, 18), token: crypto.randomBytes(16).toString('hex'), gold: 12, food: 8, materials: 6 }, null],
    activePlayer: 0, actionsLeft: 1, completedTurns: 0, turnEndsAt: 0, diceOne: 0, diceTwo: 0, diceTotal: 0,
    finished: false, winner: -1, message: 'Esperando al segundo jugador.', version: 1,
    waiters: new Set()
  };
  ROOMS.set(code, room);
  return room;
}

function safeState(room, playerIndex) {
  return {
    code: room.code, status: room.status, yourPlayer: playerIndex,
    players: room.players.map(p => p ? { name: p.name, gold: p.gold, food: p.food, materials: p.materials } : null),
    board: room.board, activePlayer: room.activePlayer, actionsLeft: room.actionsLeft,
    completedTurns: room.completedTurns, maxTurns: MAX_TURNS, turnEndsAt: room.turnEndsAt,
    diceOne: room.diceOne, diceTwo: room.diceTwo, diceTotal: room.diceTotal,
    remainingSeconds: 0,
    finished: room.finished, winner: room.winner, message: room.message, version: room.version
  };
}

function notify(room) {
  room.version++;
  for (const wake of room.waiters) wake();
  room.waiters.clear();
}

function territory(room, x, y) { return room.board.find(z => z.x === Number(x) && z.y === Number(y)); }
function adjacent(a, b) {
  if (!a || !b) return false;
  return Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;
}
function beginTurn(room) {
  room.actionsLeft = 1;
  room.diceOne = crypto.randomInt(1, 7);
  room.diceTwo = crypto.randomInt(1, 7);
  room.diceTotal = room.diceOne + room.diceTwo;
  let produced = 0;
  for (const z of room.board) {
    if (z.owner < 0 || !z.hasBuilding || z.land === 'capital' || z.productionNumber !== room.diceTotal || room.diceTotal === 7) continue;
    const p = room.players[z.owner];
    if (z.land === 'mine') p.gold += 3;
    else if (z.land === 'farm') p.food += 2;
    else if (z.land === 'factory') p.materials += 2;
    else if (z.land === 'barracks') z.troops++;
    produced++;
  }
  room.message = room.diceTotal === 7
    ? `Salió 7 (${room.diceOne} + ${room.diceTwo}): no se activa ningún terreno.`
    : `Salió ${room.diceTotal} (${room.diceOne} + ${room.diceTwo}). ${produced ? `${produced} terreno(s) producen recursos.` : 'Ningún edificio con ese número.'}`;
}

function finishByScore(room) {
  const scores = [0, 0];
  for (const z of room.board) if (z.owner >= 0) scores[z.owner] += 1 + z.troops + (z.hasBuilding ? 2 : 0) + (z.land === 'capital' ? 5 : 0);
  room.finished = true; room.status = 'finished'; room.winner = scores[0] === scores[1] ? -1 : scores[0] > scores[1] ? 0 : 1;
  room.message = room.winner < 0 ? 'Empate: se completaron las 30 rondas.' : `${room.players[room.winner].name} gana por puntuación tras 30 rondas.`;
}

function endTurn(room, reason) {
  if (room.finished) return;
  if (reason) room.message = reason;
  room.completedTurns++;
  if (room.completedTurns >= MAX_TURNS) { finishByScore(room); notify(room); return; }
  room.activePlayer = 1 - room.activePlayer;
  const p = room.players[room.activePlayer];
  beginTurn(room);
  room.message = `${room.message} Turno de ${p.name}.`;
  notify(room);
}

function applyAction(room, player, body) {
  if (room.status !== 'playing') return 'La partida aún espera al segundo jugador.';
  if (room.finished) return 'La partida ya terminó.';
  if (body.action === 'surrender') {
    room.finished = true; room.status = 'finished'; room.winner = 1 - player;
    room.message = `${room.players[room.winner].name} gana porque el rival se rindió.`;
    notify(room); return null;
  }
  if (player !== room.activePlayer) return 'Es el turno del otro jugador.';
  if (room.actionsLeft < 1) return 'No quedan acciones: termina el turno.';
  const from = territory(room, body.x, body.y);
  const to = territory(room, body.toX, body.toY);
  const action = String(body.action || '');
  let message = '';
  if (action === 'buy') {
    if (!from || from.owner !== player || !adjacent(from, to) || to.owner >= 0) return 'Compra una zona neutral vecina a una zona tuya.';
    if (room.players[player].gold < 5) return 'Comprar una zona cuesta 5 monedas.';
    room.players[player].gold -= 5; to.owner = player; to.troops = 1; to.level = 1; message = 'Zona comprada por 5 monedas.';
  } else if (action === 'build') {
    if (!from || from.owner !== player || from.hasBuilding || from.land === 'capital') return 'Elige una zona propia libre para construir.';
    const p = room.players[player]; if (p.gold < 5 || p.materials < 2) return 'Construir cuesta 5 monedas y 2 materiales.';
    p.gold -= 5; p.materials -= 2; from.hasBuilding = true; from.level = 2; message = 'Construcción terminada.';
  } else if (action === 'recruit') {
    if (!from || from.owner !== player || (from.land !== 'capital' && !(from.land === 'barracks' && from.hasBuilding))) return 'Solo puedes entrenar en una capital o cuartel propio.';
    const p = room.players[player]; if (p.food < 2 || p.gold < 2) return 'Entrenar cuesta 2 alimentos y 2 monedas.';
    p.food -= 2; p.gold -= 2; from.troops++; message = 'Has entrenado una tropa.';
  } else if (action === 'move') {
    if (!from || from.owner !== player || !to || to.owner !== player || !adjacent(from, to) || from.troops < 2) return 'Mueve una tropa a una zona propia vecina.';
    from.troops--; to.troops++; message = 'Has movido una tropa.';
  } else if (action === 'attack') {
    if (!from || from.owner !== player || !to || to.owner === player || !adjacent(from, to) || from.troops < 2) return 'Ataca una zona vecina neutral o rival con 2 tropas.';
    const attack = from.troops + crypto.randomInt(1, 7);
    const defense = (to.owner < 0 ? Math.max(1, to.troops) : to.troops) + crypto.randomInt(1, 7);
    if (attack > defense) {
      const oldOwner = to.owner; to.owner = player; to.troops = Math.max(1, from.troops - 1); from.troops = 1;
      if (to.land === 'capital' && oldOwner !== player) {
        room.finished = true; room.status = 'finished'; room.winner = player; room.message = `${room.players[player].name} conquistó la capital y gana.`;
        notify(room); return null;
      }
      message = '¡Victoria! Has conquistado la zona.';
    } else {
      from.troops = Math.max(1, from.troops - 1); if (to.owner < 0) to.troops = 1; message = 'El ataque no prosperó. Pierdes una tropa.';
    }
  } else if (action === 'end') { endTurn(room, 'El jugador terminó su turno.'); return null; }
  else return 'Acción desconocida.';
  room.actionsLeft = 1;
  room.message = message;
  notify(room);
  return null;
}

async function handle(req, res) {
  if (req.method === 'OPTIONS') return json(res, 204, {});
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  if (req.method === 'GET' && url.pathname === '/api/health') return json(res, 200, { ok: true, service: 'islas-en-disputa', rooms: ROOMS.size });
  if (req.method === 'POST' && url.pathname === '/api/rooms') {
    const body = await readBody(req); const room = makeRoom(body.name);
    return json(res, 201, { token: room.players[0].token, state: safeState(room, 0) });
  }
  const match = url.pathname.match(/^\/api\/rooms\/([A-Z0-9]{6})(?:\/(join|actions|result))?$/);
  if (!match) return json(res, 404, { error: 'Ruta no encontrada.' });
  const room = ROOMS.get(match[1]); if (!room) return json(res, 404, { error: 'No existe esa sala.' });
  const path = match[2] || '';
  if (req.method === 'POST' && path === 'join') {
    if (room.status !== 'waiting') return json(res, 409, { error: 'La sala ya está completa.' });
    const body = await readBody(req); room.players[1] = { name: String(body.name || 'Jugador rojo').slice(0, 18), token: crypto.randomBytes(16).toString('hex'), gold: 12, food: 8, materials: 6 };
    room.status = 'playing'; room.activePlayer = crypto.randomInt(2); room.message = `${room.players[room.activePlayer].name} empieza.`;
    beginTurn(room); notify(room);
    return json(res, 200, { token: room.players[1].token, state: safeState(room, 1) });
  }
  const body = req.method === 'POST' ? await readBody(req) : {};
  const actualToken = body.token || url.searchParams.get('token') || '';
  const player = room.players.findIndex(p => p && p.token === actualToken);
  if (player < 0) return json(res, 401, { error: 'Código de jugador no válido.' });
  if (req.method === 'GET' && path === '') {
    const after = Number(url.searchParams.get('after') || -1);
    if (url.searchParams.get('wait') === '1' && after === room.version && room.status !== 'finished') {
      await new Promise(resolve => {
        const done = () => { clearTimeout(timer); room.waiters.delete(done); resolve(); };
        const timer = setTimeout(done, 20_000); room.waiters.add(done);
      });
    }
    return json(res, 200, { state: safeState(room, player) });
  }
  if (req.method === 'GET' && path === 'result') {
    const scores = [0, 1].map(player => room.board.reduce((sum, z) => sum + (z.owner === player ? 1 + z.troops + (z.hasBuilding ? 2 : 0) + (z.land === 'capital' ? 5 : 0) : 0), 0));
    return json(res, 200, { finished: room.finished, winner: room.finished ? room.winner : -1, players: room.players.map(p => p && p.name), scores, message: room.finished ? room.message : 'La partida sigue en curso.' });
  }
  if (req.method === 'POST' && path === 'actions') {
    if (body.expectedVersion !== undefined && Number(body.expectedVersion) !== room.version) return json(res, 409, { error: 'La partida cambió. Actualiza el tablero.', state: safeState(room, player) });
    const error = applyAction(room, player, body);
    if (error) return json(res, 409, { error, state: safeState(room, player) });
    return json(res, 200, { state: safeState(room, player) });
  }
  return json(res, 405, { error: 'Método no permitido.' });
}

http.createServer((req, res) => {
  Promise.resolve(handle(req, res)).catch(error => {
    if (!res.headersSent) json(res, 400, { error: error.message || 'Solicitud inválida.' });
    else res.destroy();
  });
}).listen(PORT, '0.0.0.0', () => console.log(`Islas en disputa: servidor listo en http://0.0.0.0:${PORT}`));

setInterval(() => {
  const cutoff = Date.now() - 6 * 60 * 60 * 1000;
  for (const [code, room] of ROOMS) if (room.createdAt < cutoff) ROOMS.delete(code);
}, 60 * 60 * 1000).unref?.();
