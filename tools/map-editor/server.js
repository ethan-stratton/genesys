const http = require('http');
const fs = require('fs');
const path = require('path');

const LEVELS_DIR = path.join(__dirname, '../../Content/levels');
const PORT = 4000;

function getLevels() {
  const files = fs.readdirSync(LEVELS_DIR).filter(f => f.endsWith('.json'));
  const levels = {};
  for (const file of files) {
    try {
      const data = JSON.parse(fs.readFileSync(path.join(LEVELS_DIR, file), 'utf8'));
      const id = path.basename(file, '.json');
      const tg = data.tileGrid || {};
      const tiles = tg.tiles || [];
      const w = tg.width || 0, h = tg.height || 0;
      
      // Build a compact tile summary (which rows have solid tiles)
      const rows = [];
      for (let r = 0; r < h; r++) {
        let row = '';
        for (let c = 0; c < w; c++) {
          const t = tiles[r * w + c];
          row += t === 0 ? '.' : t === 1 ? '#' : t === 2 ? ':' : '+';
        }
        rows.push(row);
      }
      
      levels[id] = {
        name: data.name || id,
        bounds: data.bounds || {},
        playerSpawn: data.playerSpawn || {},
        neighbors: data.neighbors || { left: '', right: '', up: '', down: '' },
        exits: data.exits || [],
        grid: { width: w, height: h, tileSize: tg.tileSize || 32, rows },
        enemies: (data.enemies || []).length,
        items: (data.items || []).length,
        ropes: (data.ropes || []).length,
        platforms: (data.platforms || []).length,
      };
    } catch (e) {
      console.error(`Error loading ${file}:`, e.message);
    }
  }
  return levels;
}

function saveNeighbors(levelId, neighbors) {
  const file = path.join(LEVELS_DIR, `${levelId}.json`);
  if (!fs.existsSync(file)) return false;
  const data = JSON.parse(fs.readFileSync(file, 'utf8'));
  data.neighbors = neighbors;
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
  return true;
}

function saveAllNeighbors(allNeighbors) {
  for (const [id, neighbors] of Object.entries(allNeighbors)) {
    saveNeighbors(id, neighbors);
  }
  return true;
}

const server = http.createServer((req, res) => {
  if (req.method === 'GET' && req.url === '/') {
    res.writeHead(200, { 'Content-Type': 'text/html' });
    res.end(fs.readFileSync(path.join(__dirname, 'index.html'), 'utf8'));
    return;
  }
  if (req.method === 'GET' && req.url === '/api/levels') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(getLevels()));
    return;
  }
  if (req.method === 'POST' && req.url === '/api/save') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      try {
        const data = JSON.parse(body);
        saveAllNeighbors(data);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ ok: true }));
      } catch (e) {
        res.writeHead(400);
        res.end(JSON.stringify({ error: e.message }));
      }
    });
    return;
  }
  res.writeHead(404);
  res.end('Not found');
});

server.listen(PORT, () => {
  console.log(`Map editor running at http://localhost:${PORT}`);
  console.log(`Levels dir: ${LEVELS_DIR}`);
});
