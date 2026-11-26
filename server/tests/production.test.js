const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { serve, makeRoom, beginTurn, applyAction } = require('../index');

function playingRoom() {
  const room = makeRoom('Azul');
  room.players[1] = { name: 'Rojo', gold: 12, food: 8, materials: 6 };
  room.status = 'playing';
  return room;
}

test('100 boards and 6000 rolls preserve production, ownership and capital garrisons', () => {
  const covered = new Set();
  let sevens = 0;
  for (let n = 0; n < 100; n++) {
    const room = playingRoom();
    const sites = room.board.filter(z => z.land !== 'capital');
    const capitals = room.board.filter(z => z.land === 'capital');
    for (const land of ['farm', 'factory', 'mine', 'barracks'])
      assert.equal(sites.filter(z => z.land === land).length, land === 'barracks' ? 6 : 4);
    assert.deepEqual(sites.map(z => z.productionNumber).sort((a,b) => a-b), [2,3,3,4,4,5,5,6,6,8,8,9,9,10,10,11,11,12]);
    for (const cap of capitals)
      assert.ok(sites.some(z => z.land === 'barracks' && Math.abs(z.x-cap.x) + Math.abs(z.y-cap.y) === 1));
    sites.forEach((z,i) => {
      z.owner = (i+n)%5 === 0 ? -1 : (i+n)%2;
      z.level = z.owner < 0 ? 0 : 1+(i+n)%3;
      z.hasBuilding = z.level >= 2;
    });
    for (let turn = 0; turn < 60; turn++) {
      room.activePlayer = turn%2;
      const troops = room.board.map(z => z.troops);
      const expected = room.players.map(p => ({ ...p }));
      beginTurn(room);
      if (room.diceTotal === 7) sevens++;
      room.board.forEach((z,i) => {
        if (z.land === 'capital' && z.owner === room.activePlayer) troops[i]++;
        if (z.owner >= 0 && z.productionNumber === room.diceTotal) {
          const amount = z.level + (z.land === 'mine' ? 2 : 1);
          covered.add(z.land + z.level);
          if (z.land === 'barracks') troops[i] += amount;
          if (z.land === 'farm') expected[z.owner].food += amount;
          if (z.land === 'factory') expected[z.owner].materials += amount;
          if (z.land === 'mine') expected[z.owner].gold += amount;
        }
        assert.equal(z.troops, troops[i]);
      });
      assert.deepEqual(room.players, expected);
    }
  }
  assert.equal(covered.size, 12);
  assert.ok(sevens > 0);
});

test('purchase, upgrade costs and limits, recruitment, movement and capture', () => {
  const room = playingRoom();
  beginTurn(room);
  const [cap, enemy] = room.board.filter(z => z.land === 'capital');
  const adjacent = (a,b) => Math.abs(a.x-b.x) + Math.abs(a.y-b.y) === 1;
  const barracks = room.board.find(z => adjacent(z, cap) && z.land === 'barracks');
  const act = (action, z, to) => applyAction(room, 0, { action, x:z.x, y:z.y, toX:to?.x, toY:to?.y });
  const p = room.players[0];
  assert.equal(act('buy', cap, barracks), null);
  assert.equal(barracks.level, 1);
  assert.ok(act('recruit', barracks));
  assert.equal(act('build', barracks), null);
  assert.deepEqual([barracks.level,p.gold,p.materials], [2,2,4]);
  assert.ok(act('build', barracks));
  assert.deepEqual([barracks.level,p.gold,p.materials], [2,2,4]);
  p.gold = 9;
  assert.equal(act('build', barracks), null);
  assert.deepEqual([barracks.level,p.gold,p.materials], [3,2,1]);
  assert.ok(act('build', barracks));
  assert.ok(act('build', cap));
  assert.equal(act('recruit', barracks), null);
  assert.equal(act('move', cap, barracks), null);
  const neutral = room.board.find(z => adjacent(z, cap) && z.owner < 0);
  cap.troops = 20;
  assert.equal(act('attack', cap, neutral), null);
  assert.equal(neutral.level, 1);
  const front = room.board.find(z => adjacent(z, enemy));
  front.owner = 0; front.troops = 20;
  assert.equal(act('attack', front, enemy), null);
  assert.equal(room.winner, 0);
  assert.ok(room.finished);
});

test('two HTTP clients play 60 turns, replayed actions grant no extra troops, and both see the result', async t => {
  const server = http.createServer(serve);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }));
  const base = 'http://127.0.0.1:' + server.address().port + '/api/rooms';
  const post = (url, body) => fetch(url, {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(body)});
  const host = await (await post(base, {name:'Azul'})).json();
  const url = base + '/' + host.state.code;
  const guest = await (await post(url+'/join', {name:'Rojo'})).json();
  const tokens = [host.token,guest.token];
  const read = token => fetch(url+'?token='+token).then(r => r.json());
  let state = guest.state;
  for (let turn = 0; turn < 60; turn++) {
    const oldCap = state.board.filter(z => z.land === 'capital').map(z => z.troops);
    const player = state.activePlayer;
    const payload = {token:tokens[player], expectedVersion:state.version, action:'end'};
    assert.equal((await post(url+'/actions', {...payload,token:tokens[1-player]})).status, 409);
    const response = await post(url+'/actions', payload);
    assert.equal(response.status, 200);
    state = (await response.json()).state;
    assert.equal((await post(url+'/actions', payload)).status, 409);
    const opponent = (await read(tokens[1-player])).state;
    assert.deepEqual(opponent.board, state.board);
    assert.equal(opponent.version, state.version);
    for (const z of state.board.filter(z => z.land === 'capital'))
      assert.equal(z.troops, oldCap[z.owner] + (!state.finished && z.owner === state.activePlayer ? 1 : 0));
  }
  assert.ok(state.finished);
  const results = await Promise.all(tokens.map(token => fetch(url+'/result?token='+token).then(r => r.json())));
  assert.deepEqual(results[0], results[1]);
  assert.equal(results[0].winner, -1);
  assert.deepEqual(results[0].scores, [42,42]);
});
