// Simula 5.000 aperturas: J1 recluta en la capital y ataca por ruta directa.
// Compara el resultado con el 84,7 % que se obtenía sin AttackReadyTurn.
const { makeRoom, beginTurn, applyAction } = require('../index');

const TOTAL = 5000;
let j1Wins = 0;

for (let n = 0; n < TOTAL; n++) {
  const room = makeRoom('Azul');
  room.players[1] = { name: 'Rojo', gold: 12, food: 8, materials: 6 };
  room.status = 'playing';
  room.activePlayer = 0;
  beginTurn(room);

  // J1 recluta 4 veces en la capital (empieza con 4 tropas, +1 del refuerzo = 5, +4 reclutas = 9)
  const cap = room.board.find(z => z.land === 'capital' && z.owner === 0);
  for (let i = 0; i < 4; i++) {
    const err = applyAction(room, 0, { action: 'recruit', x: cap.x, y: cap.y });
    if (err) break;
  }

  // Ruta de ataque: (0,1) → (1,1) → (2,1) → (3,1) → (4,1) → (4,2)
  const route = [[1,1],[2,1],[3,1],[4,1],[4,2]];
  let pos = cap;
  let captured = true;
  for (const [tx, ty] of route) {
    if (room.finished) break;
    const target = room.board.find(z => z.x === tx && z.y === ty);
    // Intentar atacar hasta conquistar o quedarse con < 2 tropas
    let attempts = 0;
    while (!room.finished && pos.troops >= 2 && target.owner !== 0 && attempts < 20) {
      const err = applyAction(room, 0, { action: 'attack', x: pos.x, y: pos.y, toX: tx, toY: ty });
      if (err) { captured = false; break; }
      attempts++;
    }
    if (target.owner === 0) {
      pos = target;
    } else {
      captured = false;
      break;
    }
  }
  if (room.finished && room.winner === 0) j1Wins++;
}

const pct = (j1Wins / TOTAL * 100).toFixed(1);
console.log(`\nResultado de ${TOTAL} aperturas con AttackReadyTurn:`);
console.log(`  J1 gana antes del turno de J2: ${j1Wins} / ${TOTAL} (${pct} %)`);
console.log(`  Anterior sin restricción:       ~84,7 %`);
console.log(`  Diferencia:                     ${(pct - 84.7).toFixed(1)} pp\n`);

if (j1Wins / TOTAL < 0.1) {
  console.log('✅ La restricción AttackReadyTurn bloquea eficazmente el rush de apertura.');
} else {
  console.log('⚠️  J1 aún gana demasiado a menudo — revisar la restricción.');
}
