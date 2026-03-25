const http = require('http');
const fs = require('fs');
const path = require('path');

const LEVELS_DIR = path.join(__dirname, '../../Content/levels');
const LAYOUT_FILE = path.join(__dirname, 'layout.json');
const PORT = 4000;

function loadLayout() {
  try { return JSON.parse(fs.readFileSync(LAYOUT_FILE, 'utf8')); } catch { return {}; }
}

function saveLayout(positions) {
  fs.writeFileSync(LAYOUT_FILE, JSON.stringify(positions, null, 2));
}

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
    res.end(JSON.stringify({ levels: getLevels(), layout: loadLayout() }));
    return;
  }
  if (req.method === 'POST' && req.url === '/api/save') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      try {
        const data = JSON.parse(body);
        if (data.neighbors) saveAllNeighbors(data.neighbors);
        if (data.layout) saveLayout(data.layout);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ ok: true }));
      } catch (e) {
        res.writeHead(400);
        res.end(JSON.stringify({ error: e.message }));
      }
    });
    return;
  }
  
  // Delete a room
  if (req.method === 'POST' && req.url === '/api/delete') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      try {
        const { id } = JSON.parse(body);
        const file = path.join(LEVELS_DIR, `${id}.json`);
        const trashDir = path.join(__dirname, 'trash');
        if (!fs.existsSync(trashDir)) fs.mkdirSync(trashDir, { recursive: true });
        if (fs.existsSync(file)) {
          // Move to trash instead of deleting
          fs.renameSync(file, path.join(trashDir, `${id}.json`));
          // Remove from layout
          const layout = loadLayout();
          delete layout[id];
          saveLayout(layout);
          // Remove neighbor references to this room from other levels
          const files = fs.readdirSync(LEVELS_DIR).filter(f => f.endsWith('.json'));
          for (const f of files) {
            const fp = path.join(LEVELS_DIR, f);
            const d = JSON.parse(fs.readFileSync(fp, 'utf8'));
            if (d.neighbors) {
              let changed = false;
              for (const dir of ['left', 'right', 'up', 'down']) {
                const val = d.neighbors[dir];
                if (typeof val === 'string' && val === id) { d.neighbors[dir] = ''; changed = true; }
                else if (Array.isArray(val)) {
                  const filtered = val.filter(z => z.target !== id);
                  if (filtered.length !== val.length) { d.neighbors[dir] = filtered.length === 0 ? '' : filtered; changed = true; }
                }
              }
              if (changed) fs.writeFileSync(fp, JSON.stringify(d, null, 2));
            }
          }
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ ok: true }));
        } else {
          res.writeHead(404);
          res.end(JSON.stringify({ error: 'Room not found' }));
        }
      } catch (e) {
        res.writeHead(400);
        res.end(JSON.stringify({ error: e.message }));
      }
    });
    return;
  }
  
  // Create a new room
  if (req.method === 'POST' && req.url === '/api/create') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      try {
        const { id, name, width, height } = JSON.parse(body);
        if (!id || !id.match(/^[a-z0-9-]+$/)) throw new Error('Invalid id (lowercase letters, numbers, hyphens only)');
        const file = path.join(LEVELS_DIR, `${id}.json`);
        if (fs.existsSync(file)) throw new Error('Room already exists');
        const w = Math.max(10, Math.min(200, width || 30));
        const h = Math.max(10, Math.min(200, height || 20));
        const ts = 32;
        const data = {
          name: name || id,
          bounds: { left: 0, top: 0, right: w * ts, bottom: h * ts },
          playerSpawn: { x: w * ts / 2, y: (h - 2) * ts },
          neighbors: { left: '', right: '', up: '', down: '' },
          tileGrid: { width: w, height: h, tileSize: ts, tiles: new Array(w * h).fill(0) },
          enemies: [], items: [], ropes: [], platforms: [], exits: [], shelters: [], walls: [], solidFloors: [], ceilings: []
        };
        fs.writeFileSync(file, JSON.stringify(data, null, 2));
        // Add to layout
        const layout = loadLayout();
        layout[id] = { x: 0, y: 0 };
        saveLayout(layout);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ ok: true }));
      } catch (e) {
        res.writeHead(400);
        res.end(JSON.stringify({ error: e.message }));
      }
    });
    return;
  }
  
  // Resize a room
  if (req.method === 'POST' && req.url === '/api/resize') {
    let body = '';
    req.on('data', c => body += c);
    req.on('end', () => {
      try {
        const { id, width, height, anchor } = JSON.parse(body);
        // anchor: "top-left" (default), "top-right", "bottom-left", "bottom-right", "center"
        const file = path.join(LEVELS_DIR, `${id}.json`);
        if (!fs.existsSync(file)) throw new Error('Room not found');
        const data = JSON.parse(fs.readFileSync(file, 'utf8'));
        const tg = data.tileGrid || {};
        const oldW = tg.width || 0, oldH = tg.height || 0;
        const oldTiles = tg.tiles || [];
        const newW = Math.max(5, Math.min(200, width));
        const newH = Math.max(5, Math.min(200, height));
        const ts = tg.tileSize || 32;
        const anc = anchor || 'top-left';
        
        // Calculate offset based on anchor
        let ox = 0, oy = 0;
        if (anc.includes('right')) ox = newW - oldW;
        else if (anc === 'center') { ox = Math.floor((newW - oldW) / 2); oy = Math.floor((newH - oldH) / 2); }
        if (anc.includes('bottom')) oy = newH - oldH;
        else if (anc === 'center') oy = Math.floor((newH - oldH) / 2);
        
        // Copy tiles with offset
        const newTiles = new Array(newW * newH).fill(0);
        for (let r = 0; r < oldH; r++) {
          for (let c = 0; c < oldW; c++) {
            const nr = r + oy, nc = c + ox;
            if (nr >= 0 && nr < newH && nc >= 0 && nc < newW) {
              newTiles[nr * newW + nc] = oldTiles[r * oldW + c];
            }
          }
        }
        
        tg.width = newW;
        tg.height = newH;
        tg.tiles = newTiles;
        data.tileGrid = tg;
        data.bounds = { left: 0, top: 0, right: newW * ts, bottom: newH * ts };
        
        // Offset entities if anchor moved them
        if (ox !== 0 || oy !== 0) {
          const pxOx = ox * ts, pxOy = oy * ts;
          if (data.playerSpawn) { data.playerSpawn.x += pxOx; data.playerSpawn.y += pxOy; }
          for (const arr of [data.enemies, data.items, data.ropes, data.shelters, data.exits]) {
            if (Array.isArray(arr)) {
              for (const e of arr) {
                if (e.x != null) e.x += pxOx;
                if (e.y != null) e.y += pxOy;
              }
            }
          }
          for (const arr of [data.platforms, data.walls, data.solidFloors, data.ceilings]) {
            if (Array.isArray(arr)) {
              for (const e of arr) {
                if (e.X != null) e.X += pxOx;
                if (e.Y != null) e.Y += pxOy;
              }
            }
          }
        }
        
        fs.writeFileSync(file, JSON.stringify(data, null, 2));
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ ok: true, newWidth: newW, newHeight: newH }));
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
