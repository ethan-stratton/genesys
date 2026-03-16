using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace ArenaShooter;

public enum GameState { Title, Playing, Editing }

public class Game1 : Game
{
    private GameState _gameState = GameState.Title;
    private int _titleCursor;
    private bool _settingsFromTitle;
    private string[] _titleOptions => SaveData.Exists()
        ? new[] { "Continue", "New Game", "Settings", "Quit" }
        : new[] { "New Game", "Settings", "Quit" };

    private GraphicsDeviceManager _graphics;
    private SaveData _saveData;
    private SpriteBatch _spriteBatch;
    private Camera _camera;
    private Texture2D _pixel;
    private SpriteFont _font;
    private Song _bgm;
    private Player _player;

    private List<Bullet> _bullets;
    private List<Enemy> _enemies;

    private float _spawnTimer;
    private const float InitialSpawnInterval = 1.5f;
    private float _spawnInterval;
    private Random _rng;

    private bool _isDead;
    private KeyboardState _prevKb;

    // Level data (loaded from JSON)
    private LevelData _level;
    private const string DefaultLevel = "Content/levels/test-arena.json";

    // --- Settings menu ---
    private bool _menuOpen;
    private int _menuCursor;

    private struct SettingEntry
    {
        public string Label;
        public Func<bool> Get;
        public Action Toggle;
        public bool IsAction; // true = triggers action on Enter, not a toggle
    }

    private SettingEntry[] _settings;

    // Toggleable gameplay options
    private bool _spawnEnemies;
    private bool _enableSlide = true;
    private bool _enableCartwheel = true;
    private bool _enableDash = true;
    private bool _enableDoubleJump = true;
    private bool _enableWallClimb = true;
    private bool _enableRopeClimb = true;
    private bool _enableDropThrough = true;
    private bool _enableVaultKick = true;
    private bool _enableUppercut = true;
    private bool _enableSpinMelee = true;
    private bool _enableFlip = true;
    private bool _enableBladeDash = true;
    private bool _enableMusic;

    // --- Editor state ---
    private enum EditorTool { SolidFloor = 0, Platform = 1, Rope = 2, Wall = 3, Spike = 4, Exit = 5, Spawn = 6, WallSpike = 7, OverworldExit = 8, Ceiling = 9 }
    // Wall climbSide values: 0=both, 1=right face, -1=left face, 99=no climb (solid only)
    private EditorTool _editorTool = EditorTool.Platform;
    private Vector2 _editorCursor; // world position
    private bool _editorGridSnap = true;
    private int _editorGridSize = 16;
    private bool _editorDragging;
    private Vector2 _editorDragStart;
    private bool _editorMenuOpen;
    private int _editorMenuCursor;
    private MouseState _prevMouse;
    private string _editorSaveFile = "Content/levels/test-arena.json";
    private string _editorStatusMsg = "";
    private float _editorStatusTimer;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        _settings = new SettingEntry[]
        {
            new() { Label = "Enemies", Get = () => _spawnEnemies, Toggle = () => _spawnEnemies = !_spawnEnemies },
            new() { Label = "Slide", Get = () => _enableSlide, Toggle = () => _enableSlide = !_enableSlide },
            new() { Label = "Cartwheel", Get = () => _enableCartwheel, Toggle = () => _enableCartwheel = !_enableCartwheel },
            new() { Label = "Dash (run)", Get = () => _enableDash, Toggle = () => _enableDash = !_enableDash },
            new() { Label = "Double Jump", Get = () => _enableDoubleJump, Toggle = () => _enableDoubleJump = !_enableDoubleJump },
            new() { Label = "Wall Climb", Get = () => _enableWallClimb, Toggle = () => _enableWallClimb = !_enableWallClimb },
            new() { Label = "Rope Climb", Get = () => _enableRopeClimb, Toggle = () => _enableRopeClimb = !_enableRopeClimb },
            new() { Label = "Drop Through", Get = () => _enableDropThrough, Toggle = () => _enableDropThrough = !_enableDropThrough },
            new() { Label = "Vault Kick", Get = () => _enableVaultKick, Toggle = () => _enableVaultKick = !_enableVaultKick },
            new() { Label = "Uppercut", Get = () => _enableUppercut, Toggle = () => _enableUppercut = !_enableUppercut },
            new() { Label = "Spin Melee", Get = () => _enableSpinMelee, Toggle = () => _enableSpinMelee = !_enableSpinMelee },
            new() { Label = "Flip", Get = () => _enableFlip, Toggle = () => _enableFlip = !_enableFlip },
            new() { Label = "Blade Dash", Get = () => _enableBladeDash, Toggle = () => _enableBladeDash = !_enableBladeDash },
            new() { Label = "Music", Get = () => _enableMusic, Toggle = () => { _enableMusic = !_enableMusic; if (_enableMusic) { MediaPlayer.IsRepeating = true; MediaPlayer.Play(_bgm); } else { MediaPlayer.Stop(); } } },
            new() { Label = "Quit Game", Get = () => false, Toggle = () => Exit(), IsAction = true },
        };

        Restart();
        base.Initialize();
    }

    private void LoadLevel(string path)
    {
        _level = LevelData.Load(path);
        Player.WorldLeft = _level.Bounds.Left;
        Player.WorldRight = _level.Bounds.Right;
    }

    private void Restart()
    {
        if (_level == null)
        {
            if (System.IO.File.Exists(DefaultLevel))
                LoadLevel(DefaultLevel);
            else
            {
                // Generate empty level if none exist
                _level = new LevelData { Name = "empty" };
                _level.Build();
                if (!System.IO.Directory.Exists("Content/levels"))
                    System.IO.Directory.CreateDirectory("Content/levels");
                _editorSaveFile = DefaultLevel;
                SaveLevel();
            }
        }
        var spawn = _level.PlayerSpawn;
        _player = new Player(new Vector2(spawn.X, spawn.Y));
        _camera?.SnapTo(_player.Position, Player.Width, Player.Height);
        _bullets = new List<Bullet>();
        _enemies = new List<Enemy>();
        _spawnTimer = 0f;
        _spawnInterval = InitialSpawnInterval;
        _rng = new Random();

        _isDead = false;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = Content.Load<SpriteFont>("DefaultFont");
        _bgm = Content.Load<Song>("bgm");
    }

    private Vector2 PlayerCenter =>
        _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();

        if (_gameState == GameState.Title)
        {
            if (_titleCursor >= _titleOptions.Length) _titleCursor = 0;
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _titleCursor = (_titleCursor - 1 + _titleOptions.Length) % _titleOptions.Length;
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _titleCursor = (_titleCursor + 1) % _titleOptions.Length;
            
            bool confirm = (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                           (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter));
            if (confirm)
            {
                switch (_titleOptions[_titleCursor])
                {
                    case "Continue":
                        _saveData = SaveData.Load();
                        if (_saveData != null)
                        {
                            string path = $"Content/levels/{_saveData.CurrentLevel}.json";
                            if (System.IO.File.Exists(path))
                                LoadLevel(path);
                            else
                                LoadLevel(DefaultLevel);
                            _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                            _gameState = GameState.Playing;
                            _player = new Player(new Microsoft.Xna.Framework.Vector2(_saveData.SpawnX, _saveData.SpawnY));
                            _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                            _bullets = new List<Bullet>();
                            _enemies = new List<Enemy>();
                        }
                        break;
                    case "New Game":
                        SaveData.Delete();
                        _saveData = new SaveData();
                        LoadLevel(DefaultLevel);
                        _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                        _gameState = GameState.Playing;
                        Restart();
                        _saveData.CurrentLevel = System.IO.Path.GetFileNameWithoutExtension(DefaultLevel);
                        _saveData.SpawnX = _player.Position.X;
                        _saveData.SpawnY = _player.Position.Y;
                        _saveData.Save();
                        break;
                    case "Settings":
                        _menuOpen = true;
                        _menuCursor = 0;
                        _settingsFromTitle = true;
                        break;
                    case "Quit":
                        Exit();
                        break;
                }
            }

            // Handle settings menu while on title screen
            if (_menuOpen && _settingsFromTitle)
            {
                if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                {
                    _menuOpen = false;
                    _settingsFromTitle = false;
                }
                else
                {
                    UpdateMenu(kb);
                }
            }

            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

        // --- Editor state ---
        if (_gameState == GameState.Editing)
        {
            UpdateEditor(gameTime, kb);
            _prevKb = kb;
            _prevMouse = Mouse.GetState();
            base.Update(gameTime);
            return;
        }

        // Toggle editor with =
        if (kb.IsKeyDown(Keys.OemPlus) && _prevKb.IsKeyUp(Keys.OemPlus))
        {
            _gameState = GameState.Editing;
            _editorCursor = _player.Position;
            _editorMenuOpen = false;
            _prevKb = kb;
            _prevMouse = Mouse.GetState();
            return;
        }

        // Toggle menu with Escape
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _menuOpen = !_menuOpen;
            if (_menuOpen) _menuCursor = 0;
        }

        if (_menuOpen)
        {
            UpdateMenu(kb);
            _prevKb = kb;
            return; // game is paused while menu is open
        }

        if (_isDead)
        {
            if (kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R))
                Restart();
            _prevKb = kb;
            return;
        }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Pass enabled features to player
        _player.EnableSlide = _enableSlide;
        _player.EnableCartwheel = _enableCartwheel;
        _player.EnableDash = _enableDash;
        _player.EnableDoubleJump = _enableDoubleJump;
        _player.EnableDropThrough = _enableDropThrough;
        _player.EnableVaultKick = _enableVaultKick;
        _player.EnableUppercut = _enableUppercut;
        _player.EnableSpinMelee = _enableSpinMelee;
        _player.EnableFlip = _enableFlip;
        _player.EnableBladeDash = _enableBladeDash;

        var wallsToPass = _enableWallClimb ? _level.WallRects : null;
        var wallSidesToPass = _enableWallClimb ? _level.WallClimbSides : null;
        var ropesToPass = _enableRopeClimb ? _level.RopeXPositions : null;
        var ropeTopsToPass = _enableRopeClimb ? _level.RopeTops : null;
        var ropeBottomsToPass = _enableRopeClimb ? _level.RopeBottoms : null;

        _player.Update(dt, kb, _level.Floor.Y, _level.AllPlatforms, ropesToPass, ropeTopsToPass, ropeBottomsToPass, wallsToPass, wallSidesToPass, _level.WallRects, _level.CeilingRects, _level.SolidFloorRects);

        // Track play time
        if (_saveData != null) _saveData.PlayTime += dt;
        // Update camera
        _camera.Update(dt, _player.Position, Player.Width, Player.Height, _player.FacingDir, _player.IsGrounded, _player.Velocity.Y);

        // Shoot bullets
        if (_player.WantsToShoot)
        {
            _bullets.Add(new Bullet(PlayerCenter, _player.ShootDirection));
        }

        // Update bullets
        _bullets.ForEach(b => b.Update(dt));
        _bullets.RemoveAll(b => b.IsDead);

        // Spawn enemies
        if (_spawnEnemies)
        {
            _spawnTimer += dt;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0;
                _spawnInterval = MathF.Max(0.3f, _spawnInterval - 0.03f);

                var fromLeft = _rng.Next(2) == 0;
                var pos = new Vector2(
                    fromLeft ? _level.Bounds.Left - Enemy.Size : _level.Bounds.Right,
                    _level.Floor.Y - Enemy.Size
                );
                _enemies.Add(new Enemy(pos));
            }
        }

        // Update enemies
        _enemies.ForEach(e => e.Update(dt, PlayerCenter));

        // Check collisions
        foreach (var e in _enemies)
        {
            var eRect = new Rectangle((int)e.Position.X, (int)e.Position.Y, Enemy.Size, Enemy.Size);
            foreach (var b in _bullets)
            {
                var bRect = new Rectangle((int)b.Position.X, (int)b.Position.Y, Bullet.Size, Bullet.Size);
                if (bRect.Intersects(eRect)) { e.IsDead = true; b.IsDead = true; }
            }
            if (_player.MeleeTimer > 0 && _player.MeleeHitbox.Intersects(eRect))
            {
                e.IsDead = true;
            }
            if (_player.IsVaultKicking && _player.VaultKickHitbox.Intersects(eRect))
            {
                e.IsDead = true;
            }
            if (_player.IsUppercutting && _player.UppercutHitbox.Intersects(eRect))
            {
                e.IsDead = true;
            }
            if (_player.IsBladeDashing && _player.BladeDashHitbox.Intersects(eRect))
            {
                e.IsDead = true;
            }
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            if (!_player.IsSliding && !_player.IsCartwheeling && !_player.IsVaulting && !_player.IsVaultKicking && !_player.IsUppercutting && !_player.IsFlipping && !_player.IsBladeDashing && eRect.Intersects(pRect))
            {
                _isDead = true;
                break;
            }
        }

        _bullets.RemoveAll(b => b.IsDead);
        _enemies.RemoveAll(e => e.IsDead);

        // Spike collision
        if (!_isDead)
        {
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            foreach (var spike in _level.AllSpikeRects)            {
                if (pRect.Intersects(spike))
                {
                    _isDead = true;
                    break;
                }
            }
        }

        // Exit collision — load next level
        if (!_isDead)
        {
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            for (int i = 0; i < _level.ExitRects.Length; i++)
            {
                if (pRect.Intersects(_level.ExitRects[i]) && _level.ExitTargets[i] != "")
                {
                    string target = _level.ExitTargets[i];
                    if (target == "__overworld__")
                    {
                        // Placeholder: return to title screen (will be overworld later)
                        _gameState = GameState.Title;
                        _titleCursor = 0;
                        break;
                    }
                    string nextPath = $"Content/levels/{target}.json";
                    if (System.IO.File.Exists(nextPath))
                    {
                        LoadLevel(nextPath);
                        _editorSaveFile = nextPath;
                        _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                        Restart();
                        // Auto-save on level transition
                        if (_saveData != null)
                        {
                            _saveData.CurrentLevel = target;
                            _saveData.SpawnX = _player.Position.X;
                            _saveData.SpawnY = _player.Position.Y;
                            _saveData.Save();
                        }
                    }
                    break;
                }
            }
        }

        _prevKb = kb;
        base.Update(gameTime);
    }

    private string SafeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (_font.Characters.Contains(c))
                sb.Append(c);
            else
                sb.Append('?');
        }
        return sb.ToString();
    }

    // ===================== EDITOR =====================

    private void UpdateEditor(GameTime gameTime, KeyboardState kb)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var mouse = Mouse.GetState();

        // Status message countdown
        if (_editorStatusTimer > 0) _editorStatusTimer -= dt;

        // Editor menu (Esc)
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _editorMenuOpen = !_editorMenuOpen;
            _editorMenuCursor = 0;
            _editorMenuMode = EditorMenuMode.Main;
        }

        if (_editorMenuOpen)
        {
            UpdateEditorMenu(kb);
            return;
        }

        // Back to play mode with =
        if (kb.IsKeyDown(Keys.OemPlus) && _prevKb.IsKeyUp(Keys.OemPlus))
        {
            _gameState = GameState.Playing;
            // Rebuild level arrays
            _level.Build();
            _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
            Restart();
            return;
        }

        // Move cursor with WASD
        float speed = kb.IsKeyDown(Keys.LeftShift) ? 600f : 200f;
        if (kb.IsKeyDown(Keys.W)) _editorCursor.Y -= speed * dt;
        if (kb.IsKeyDown(Keys.S)) _editorCursor.Y += speed * dt;
        if (kb.IsKeyDown(Keys.A)) _editorCursor.X -= speed * dt;
        if (kb.IsKeyDown(Keys.D)) _editorCursor.X += speed * dt;

        // Tool select with number keys
        if (kb.IsKeyDown(Keys.D0) && _prevKb.IsKeyUp(Keys.D0)) _editorTool = EditorTool.SolidFloor;
        if (kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1)) _editorTool = EditorTool.Platform;
        if (kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2)) _editorTool = EditorTool.Rope;
        if (kb.IsKeyDown(Keys.D3) && _prevKb.IsKeyUp(Keys.D3)) _editorTool = EditorTool.Wall;
        if (kb.IsKeyDown(Keys.D4) && _prevKb.IsKeyUp(Keys.D4)) _editorTool = EditorTool.Spike;
        if (kb.IsKeyDown(Keys.D5) && _prevKb.IsKeyUp(Keys.D5)) _editorTool = EditorTool.Exit;
        if (kb.IsKeyDown(Keys.D6) && _prevKb.IsKeyUp(Keys.D6)) _editorTool = EditorTool.Spawn;

        if (kb.IsKeyDown(Keys.D7) && _prevKb.IsKeyUp(Keys.D7)) _editorTool = EditorTool.WallSpike;
        if (kb.IsKeyDown(Keys.D8) && _prevKb.IsKeyUp(Keys.D8)) _editorTool = EditorTool.OverworldExit;
        if (kb.IsKeyDown(Keys.D9) && _prevKb.IsKeyUp(Keys.D9)) _editorTool = EditorTool.Ceiling;

        // Grid snap toggle
        if (kb.IsKeyDown(Keys.G) && _prevKb.IsKeyUp(Keys.G))
            _editorGridSnap = !_editorGridSnap;

        // Camera follows cursor
        _camera.SnapTo(_editorCursor, 0, 0);

        // Convert mouse to world coordinates
        var worldMouse = Vector2.Transform(
            new Vector2(mouse.X, mouse.Y),
            Matrix.Invert(_camera.TransformMatrix));

        var snapped = _editorGridSnap
            ? new Vector2(
                MathF.Round(worldMouse.X / _editorGridSize) * _editorGridSize,
                MathF.Round(worldMouse.Y / _editorGridSize) * _editorGridSize)
            : worldMouse;

        // Left click — place / start drag
        if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
        {
            if (_editorTool == EditorTool.Spawn)
            {
                _level.PlayerSpawn = new PointData { X = (int)snapped.X, Y = (int)snapped.Y };
                SetEditorStatus("Spawn point set");
            }
            else
            {
                _editorDragging = true;
                _editorDragStart = snapped;
            }
        }

        // Left release — finish placing
        if (mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed && _editorDragging)
        {
            _editorDragging = false;
            var dragEnd = snapped;
            int x = (int)MathF.Min(_editorDragStart.X, dragEnd.X);
            int y = (int)MathF.Min(_editorDragStart.Y, dragEnd.Y);
            int w = (int)MathF.Abs(dragEnd.X - _editorDragStart.X);
            int h = (int)MathF.Abs(dragEnd.Y - _editorDragStart.Y);
            if (w < 8) w = 32; // minimum sizes
            if (h < 8) h = 12;

            switch (_editorTool)
            {
                case EditorTool.SolidFloor:
                    var sfList = new List<RectData>(_level.SolidFloors);
                    sfList.Add(new RectData { X = x, Y = y, W = w, H = h < 12 ? 24 : h });
                    _level.SolidFloors = sfList.ToArray();
                    _level.Build();
                    SetEditorStatus("Solid floor added");
                    break;
                case EditorTool.Platform:
                    var pList = new List<RectData>(_level.Platforms);
                    pList.Add(new RectData { X = x, Y = y, W = w, H = 12 });
                    _level.Platforms = pList.ToArray();
                    _level.Build();
                    SetEditorStatus("Platform added");
                    break;
                case EditorTool.Rope:
                    var rList = new List<RopeData>(_level.Ropes);
                    rList.Add(new RopeData { X = _editorDragStart.X, Top = MathF.Min(_editorDragStart.Y, dragEnd.Y), Bottom = MathF.Max(_editorDragStart.Y, dragEnd.Y) });
                    _level.Ropes = rList.ToArray();
                    _level.Build();
                    SetEditorStatus("Rope added");
                    break;
                case EditorTool.Wall:
                    // Clamp wall bottom to floor
                    int wallH = h;
                    if (y + wallH > _level.Floor.Y) wallH = _level.Floor.Y - y;
                    if (wallH < 16) wallH = 16;
                    var wList = new List<WallData>(_level.Walls);
                    wList.Add(new WallData { X = x, Y = y, W = w, H = wallH, ClimbSide = 0 });
                    _level.Walls = wList.ToArray();
                    _level.Build();
                    SetEditorStatus("Wall added (both sides, [F] to cycle)");
                    break;
                case EditorTool.Spike:
                    // Check if near a ceiling bottom — snap upward (hanging spikes)
                    int spikeY = y;
                    bool ceilingSpike = false;
                    foreach (var ceil in _level.Ceilings)
                    {
                        if (MathF.Abs(y - (ceil.Y + ceil.H)) < 20 &&
                            x + w > ceil.X && x < ceil.X + ceil.W)
                        {
                            spikeY = ceil.Y + ceil.H;
                            ceilingSpike = true;
                            break;
                        }
                    }
                    var sList = new List<RectData>(_level.Spikes);
                    sList.Add(new RectData { X = x, Y = spikeY, W = w, H = 12 });
                    _level.Spikes = sList.ToArray();
                    _level.Build();
                    SetEditorStatus(ceilingSpike ? "Ceiling spike added" : "Spike added");
                    break;
                case EditorTool.Exit:
                    var eList = new List<ExitData>(_level.Exits);
                    eList.Add(new ExitData { X = x, Y = y, W = w, H = h, TargetLevel = "" });
                    _level.Exits = eList.ToArray();
                    _level.Build();
                    SetEditorStatus("Exit added (Tab over exit to set target)");
                    break;
                case EditorTool.WallSpike:
                    // Find nearest wall to snap to
                    int bestWallIdx = -1;
                    float bestDist = float.MaxValue;
                    int snapSide = 1;
                    for (int wi = 0; wi < _level.Walls.Length; wi++)
                    {
                        var wall = _level.Walls[wi];
                        // Check distance to left face
                        float dLeft = MathF.Abs(x - wall.X);
                        // Check distance to right face
                        float dRight = MathF.Abs(x - (wall.X + wall.W));
                        if (dLeft < bestDist) { bestDist = dLeft; bestWallIdx = wi; snapSide = -1; }
                        if (dRight < bestDist) { bestDist = dRight; bestWallIdx = wi; snapSide = 1; }
                    }
                    if (bestWallIdx >= 0 && bestDist < 60)
                    {
                        var wall = _level.Walls[bestWallIdx];
                        int wsX = snapSide == 1 ? wall.X + wall.W : wall.X - 12;
                        int wsY = Math.Max(y, wall.Y);
                        int wsH = Math.Min(h, wall.Y + wall.H - wsY);
                        if (wsH < 12) wsH = 12;
                        var wsList = new List<WallSpikeData>(_level.WallSpikes);
                        wsList.Add(new WallSpikeData { X = wsX, Y = wsY, W = 12, H = wsH, Side = snapSide });
                        _level.WallSpikes = wsList.ToArray();
                        _level.Build();
                        SetEditorStatus($"Wall spike snapped to wall ({(snapSide == 1 ? "right" : "left")} face)");
                    }
                    else
                    {
                        SetEditorStatus("No wall nearby — place closer to a wall");
                    }
                    break;
                case EditorTool.OverworldExit:
                    var owList = new List<ExitData>(_level.Exits);
                    owList.Add(new ExitData { X = x, Y = y, W = w, H = h, TargetLevel = "__overworld__" });
                    _level.Exits = owList.ToArray();
                    _level.Build();
                    SetEditorStatus("Overworld exit added");
                    break;
                case EditorTool.Ceiling:
                    var cList = new List<RectData>(_level.Ceilings);
                    cList.Add(new RectData { X = x, Y = y, W = w, H = 12 });
                    _level.Ceilings = cList.ToArray();
                    _level.Build();
                    SetEditorStatus("Ceiling added");
                    break;
            }
        }

        // Right click — delete nearest object
        if (mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released)
        {
            var wp = new Point((int)worldMouse.X, (int)worldMouse.Y);
            if (TryDeleteAt(wp))
                SetEditorStatus("Deleted");
        }

        // F — cycle last wall's climb side: 0 (both) → 1 (right) → -1 (left)
        if (kb.IsKeyDown(Keys.F) && _prevKb.IsKeyUp(Keys.F) && _level.Walls.Length > 0)
        {
            var last = _level.Walls[_level.Walls.Length - 1];
            last.ClimbSide = last.ClimbSide switch { 0 => 1, 1 => -1, -1 => 99, _ => 0 };
            _level.Walls[_level.Walls.Length - 1] = last;
            _level.Build();
            string sideStr = last.ClimbSide switch { 0 => "both", 1 => "right", -1 => "left", _ => "none (solid)" };
            SetEditorStatus($"Wall climb side: {sideStr}");
        }

        // Tab — cycle exit target (hover mouse over an exit)
        if (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab))
        {
            var worldMouse2 = Vector2.Transform(new Vector2(mouse.X, mouse.Y), Matrix.Invert(_camera.TransformMatrix));
            var mp = new Point((int)worldMouse2.X, (int)worldMouse2.Y);
            for (int i = 0; i < _level.Exits.Length; i++)
            {
                var e = _level.Exits[i];
                if (new Rectangle(e.X, e.Y, e.W, e.H).Contains(mp))
                {
                    // Get available level files
                    var levelsDir = "Content/levels";
                    var files = System.IO.Directory.Exists(levelsDir)
                        ? System.IO.Directory.GetFiles(levelsDir, "*.json")
                        : Array.Empty<string>();
                    var names = new List<string> { "" }; // empty = no target
                    foreach (var f in files)
                        names.Add(System.IO.Path.GetFileNameWithoutExtension(f));
                    int idx = names.IndexOf(e.TargetLevel);
                    idx = (idx + 1) % names.Count;
                    _level.Exits[i].TargetLevel = names[idx];
                    _level.Build();
                    SetEditorStatus($"Exit target: {(names[idx] == "" ? "(none)" : names[idx])}");
                    break;
                }
            }
        }
    }

    private bool TryDeleteAt(Point p)
    {
        // Check platforms
        for (int i = _level.Platforms.Length - 1; i >= 0; i--)
        {
            var r = _level.Platforms[i];
            if (new Rectangle(r.X, r.Y, r.W, r.H).Contains(p))
            {
                var list = new List<RectData>(_level.Platforms);
                list.RemoveAt(i);
                _level.Platforms = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check spikes
        for (int i = _level.Spikes.Length - 1; i >= 0; i--)
        {
            var r = _level.Spikes[i];
            if (new Rectangle(r.X, r.Y, r.W, r.H).Contains(p))
            {
                var list = new List<RectData>(_level.Spikes);
                list.RemoveAt(i);
                _level.Spikes = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check walls
        for (int i = _level.Walls.Length - 1; i >= 0; i--)
        {
            var w = _level.Walls[i];
            if (new Rectangle(w.X, w.Y, w.W, w.H).Contains(p))
            {
                var list = new List<WallData>(_level.Walls);
                list.RemoveAt(i);
                _level.Walls = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check exits
        for (int i = _level.Exits.Length - 1; i >= 0; i--)
        {
            var e = _level.Exits[i];
            if (new Rectangle(e.X, e.Y, e.W, e.H).Contains(p))
            {
                var list = new List<ExitData>(_level.Exits);
                list.RemoveAt(i);
                _level.Exits = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check ropes (10px tolerance around X)
        for (int i = _level.Ropes.Length - 1; i >= 0; i--)
        {
            var r = _level.Ropes[i];
            if (MathF.Abs(p.X - r.X) < 10 && p.Y >= r.Top && p.Y <= r.Bottom)
            {
                var list = new List<RopeData>(_level.Ropes);
                list.RemoveAt(i);
                _level.Ropes = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check wall spikes
        for (int i = _level.WallSpikes.Length - 1; i >= 0; i--)
        {
            var ws = _level.WallSpikes[i];
            if (new Rectangle(ws.X, ws.Y, ws.W, ws.H).Contains(p))
            {
                var list = new List<WallSpikeData>(_level.WallSpikes);
                list.RemoveAt(i);
                _level.WallSpikes = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check ceilings
        for (int i = _level.Ceilings.Length - 1; i >= 0; i--)
        {
            var c = _level.Ceilings[i];
            if (new Rectangle(c.X, c.Y, c.W, c.H).Contains(p))
            {
                var list = new List<RectData>(_level.Ceilings);
                list.RemoveAt(i);
                _level.Ceilings = list.ToArray();
                _level.Build();
                return true;
            }
        }
        // Check solid floors
        for (int i = _level.SolidFloors.Length - 1; i >= 0; i--)
        {
            var sf = _level.SolidFloors[i];
            if (new Rectangle(sf.X, sf.Y, sf.W, sf.H).Contains(p))
            {
                var list = new List<RectData>(_level.SolidFloors);
                list.RemoveAt(i);
                _level.SolidFloors = list.ToArray();
                _level.Build();
                return true;
            }
        }
        return false;
    }

    private void SetEditorStatus(string msg)
    {
        _editorStatusMsg = msg;
        _editorStatusTimer = 2f;
    }

    // Editor menu sub-state
    private enum EditorMenuMode { Main, SaveAs, LoadBrowser }
    private EditorMenuMode _editorMenuMode;
    private string _editorSaveAsName = "";
    private string[] _editorLevelFiles = Array.Empty<string>();
    private int _editorBrowserCursor;

    private void UpdateEditorMenu(KeyboardState kb)
    {
        if (_editorMenuMode == EditorMenuMode.SaveAs)
        {
            UpdateEditorSaveAs(kb);
            return;
        }
        if (_editorMenuMode == EditorMenuMode.LoadBrowser)
        {
            UpdateEditorBrowser(kb);
            return;
        }

        string[] options = { "Save", "Save As...", "Load Level...", "New Level", "Delete Level", "Back to Game (=)", "Help" };
        int count = options.Length;

        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
            _editorMenuCursor = (_editorMenuCursor - 1 + count) % count;
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
            _editorMenuCursor = (_editorMenuCursor + 1) % count;

        bool confirm = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                       (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
        if (confirm)
        {
            switch (_editorMenuCursor)
            {
                case 0: // Save
                    SaveLevel();
                    _editorMenuOpen = false;
                    break;
                case 1: // Save As
                    _editorMenuMode = EditorMenuMode.SaveAs;
                    _editorSaveAsName = "";
                    break;
                case 2: // Load
                    _editorMenuMode = EditorMenuMode.LoadBrowser;
                    _editorBrowserCursor = 0;
                    var dir = "Content/levels";
                    _editorLevelFiles = System.IO.Directory.Exists(dir)
                        ? System.IO.Directory.GetFiles(dir, "*.json")
                        : Array.Empty<string>();
                    break;
                case 3: // New
                    _level = new LevelData();
                    _level.Build();
                    _editorCursor = Vector2.Zero;
                    SetEditorStatus("New empty level");
                    _editorMenuOpen = false;
                    break;
                case 4: // Delete Level
                    if (System.IO.File.Exists(_editorSaveFile))
                    {
                        System.IO.File.Delete(_editorSaveFile);
                        SetEditorStatus($"Deleted {System.IO.Path.GetFileName(_editorSaveFile)}");
                    }
                    // Load another level or generate empty
                    var remaining = System.IO.Directory.Exists("Content/levels")
                        ? System.IO.Directory.GetFiles("Content/levels", "*.json")
                        : Array.Empty<string>();
                    if (remaining.Length > 0)
                    {
                        LoadLevel(remaining[0]);
                        _editorSaveFile = remaining[0];
                    }
                    else
                    {
                        _level = new LevelData { Name = "empty" };
                        _level.Build();
                        _editorSaveFile = "Content/levels/empty.json";
                        SaveLevel();
                    }
                    _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                    _editorCursor = new Vector2(_level.PlayerSpawn.X, _level.PlayerSpawn.Y);
                    _editorMenuOpen = false;
                    break;
                case 5: // Back to game
                    _gameState = GameState.Playing;
                    _level.Build();
                    _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                    Restart();
                    _editorMenuOpen = false;
                    break;
                case 6: // Help
                    _editorMenuOpen = false;
                    break;
            }
        }
    }

    private void UpdateEditorSaveAs(KeyboardState kb)
    {
        // Simple text input: A-Z, 0-9, backspace, enter to confirm, escape to cancel
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _editorMenuMode = EditorMenuMode.Main;
            return;
        }
        if (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter) && _editorSaveAsName.Length > 0)
        {
            _editorSaveFile = $"Content/levels/{_editorSaveAsName}.json";
            _level.Name = _editorSaveAsName;
            SaveLevel();
            _editorMenuMode = EditorMenuMode.Main;
            _editorMenuOpen = false;
            return;
        }
        if (kb.IsKeyDown(Keys.Back) && _prevKb.IsKeyUp(Keys.Back) && _editorSaveAsName.Length > 0)
        {
            _editorSaveAsName = _editorSaveAsName.Substring(0, _editorSaveAsName.Length - 1);
            return;
        }
        // Letter/number input
        for (Keys k = Keys.A; k <= Keys.Z; k++)
        {
            if (kb.IsKeyDown(k) && _prevKb.IsKeyUp(k))
            {
                _editorSaveAsName += k.ToString().ToLower();
                break;
            }
        }
        for (Keys k = Keys.D0; k <= Keys.D9; k++)
        {
            if (kb.IsKeyDown(k) && _prevKb.IsKeyUp(k))
            {
                _editorSaveAsName += (k - Keys.D0).ToString();
                break;
            }
        }
        if (kb.IsKeyDown(Keys.OemMinus) && _prevKb.IsKeyUp(Keys.OemMinus))
            _editorSaveAsName += "-";
    }

    private void UpdateEditorBrowser(KeyboardState kb)
    {
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _editorMenuMode = EditorMenuMode.Main;
            return;
        }
        if (_editorLevelFiles.Length == 0) return;

        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
            _editorBrowserCursor = (_editorBrowserCursor - 1 + _editorLevelFiles.Length) % _editorLevelFiles.Length;
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
            _editorBrowserCursor = (_editorBrowserCursor + 1) % _editorLevelFiles.Length;

        bool confirm = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                       (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
        if (confirm)
        {
            var path = _editorLevelFiles[_editorBrowserCursor];
            LoadLevel(path);
            _editorSaveFile = path;
            _editorCursor = new Vector2(_level.PlayerSpawn.X, _level.PlayerSpawn.Y);
            SetEditorStatus($"Loaded {System.IO.Path.GetFileName(path)}");
            _editorMenuMode = EditorMenuMode.Main;
            _editorMenuOpen = false;
        }
    }

    private void SaveLevel()
    {
        var dir = System.IO.Path.GetDirectoryName(_editorSaveFile);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var json = System.Text.Json.JsonSerializer.Serialize(_level, opts);
        System.IO.File.WriteAllText(_editorSaveFile, json);
        SetEditorStatus($"Saved to {_editorSaveFile}");
    }

    private void DrawEditor()
    {
        var mouse = Mouse.GetState();

        // World-space rendering with camera
        _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix);

        // Draw floor
        int floorY = _level.Floor.Y;
        int floorH = _level.Floor.Height;
        int bL = _level.Bounds.Left;
        int bR = _level.Bounds.Right;
        _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, floorH), new Color(40, 40, 40) * 0.5f);

        // Draw grid
        if (_editorGridSnap)
        {
            var camInv = Matrix.Invert(_camera.TransformMatrix);
            var topLeft = Vector2.Transform(Vector2.Zero, camInv);
            var botRight = Vector2.Transform(new Vector2(800, 600), camInv);
            int gs = _editorGridSize;
            int startX = ((int)topLeft.X / gs) * gs;
            int startY = ((int)topLeft.Y / gs) * gs;
            for (int gx = startX; gx < (int)botRight.X; gx += gs)
                _spriteBatch.Draw(_pixel, new Rectangle(gx, (int)topLeft.Y, 1, (int)(botRight.Y - topLeft.Y)), Color.White * 0.04f);
            for (int gy = startY; gy < (int)botRight.Y; gy += gs)
                _spriteBatch.Draw(_pixel, new Rectangle((int)topLeft.X, gy, (int)(botRight.X - topLeft.X), 1), Color.White * 0.04f);
        }

        // Draw platforms
        foreach (var p in _level.Platforms)
            _spriteBatch.Draw(_pixel, new Rectangle(p.X, p.Y, p.W, p.H), new Color(50, 50, 50));

        // Draw spikes
        foreach (var s in _level.Spikes)
            _spriteBatch.Draw(_pixel, new Rectangle(s.X, s.Y, s.W, s.H), Color.Red * 0.6f);

        // Draw ceilings
        foreach (var c in _level.Ceilings)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y, c.W, c.H), new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y + c.H - 2, c.W, 2), new Color(90, 90, 90));
        }

        // Draw solid floors
        foreach (var sf in _level.SolidFloors)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(sf.X, sf.Y, sf.W, sf.H), new Color(70, 50, 30));
            _spriteBatch.Draw(_pixel, new Rectangle(sf.X, sf.Y, sf.W, 2), new Color(110, 80, 50));
        }

        // Draw walls (editor)
        foreach (var w in _level.Walls)
        {
            Color wallColor = w.ClimbSide == 99 ? new Color(80, 40, 40) : new Color(60, 60, 60);
            _spriteBatch.Draw(_pixel, new Rectangle(w.X, w.Y, w.W, w.H), wallColor);
            // Climb side indicators
            if (w.ClimbSide == 0) // both
            {
                _spriteBatch.Draw(_pixel, new Rectangle(w.X - 4, w.Y + w.H / 2 - 2, 4, 4), Color.Cyan * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle(w.X + w.W, w.Y + w.H / 2 - 2, 4, 4), Color.Cyan * 0.6f);
            }
            else if (w.ClimbSide == 1)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(w.X + w.W, w.Y + w.H / 2 - 2, 4, 4), Color.Cyan * 0.6f);
            }
            else if (w.ClimbSide == -1)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(w.X - 4, w.Y + w.H / 2 - 2, 4, 4), Color.Cyan * 0.6f);
            }
            // 99 = no indicator (solid, no climb)
        }

        // Draw ropes
        foreach (var r in _level.Ropes)
            _spriteBatch.Draw(_pixel, new Rectangle((int)r.X - 1, (int)r.Top, 3, (int)(r.Bottom - r.Top)), new Color(120, 80, 40));

        // Draw exits (with target labels)
        foreach (var e in _level.Exits)
        {
            bool isOw = e.TargetLevel == "__overworld__";
            _spriteBatch.Draw(_pixel, new Rectangle(e.X, e.Y, e.W, e.H), isOw ? Color.CornflowerBlue * 0.4f : Color.LimeGreen * 0.4f);
            string label = isOw ? "OVERWORLD" : (string.IsNullOrEmpty(e.TargetLevel) ? "(?)" : e.TargetLevel);
            _spriteBatch.DrawString(_font, SafeText(label), new Vector2(e.X, e.Y - 16), isOw ? Color.CornflowerBlue * 0.7f : Color.LimeGreen * 0.7f);
        }

        // Draw wall spikes
        foreach (var ws in _level.WallSpikes)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(ws.X, ws.Y, ws.W, ws.H), Color.Red * 0.6f);
            // Teeth
            int teeth = ws.H / 12;
            for (int t = 0; t < teeth; t++)
            {
                int ty = ws.Y + t * 12 + 2;
                int tipX = ws.Side == 1 ? ws.X + ws.W : ws.X - 4;
                _spriteBatch.Draw(_pixel, new Rectangle(tipX, ty, 4, 8), Color.Red * 0.4f);
                int tipX2 = ws.Side == 1 ? tipX + 4 : tipX - 4;
                _spriteBatch.Draw(_pixel, new Rectangle(tipX2, ty + 2, 4, 4), Color.Red * 0.3f);
            }
        }

        // Draw spawn point
        _spriteBatch.Draw(_pixel, new Rectangle(_level.PlayerSpawn.X - 2, _level.PlayerSpawn.Y - 2, Player.Width + 4, Player.Height + 4), Color.Cyan * 0.3f);
        _spriteBatch.Draw(_pixel, new Rectangle(_level.PlayerSpawn.X, _level.PlayerSpawn.Y, Player.Width, Player.Height), Color.Cyan * 0.5f);

        // Draw drag preview
        if (_editorDragging)
        {
            var worldMouse = Vector2.Transform(new Vector2(mouse.X, mouse.Y), Matrix.Invert(_camera.TransformMatrix));
            var dragEnd = _editorGridSnap
                ? new Vector2(MathF.Round(worldMouse.X / _editorGridSize) * _editorGridSize, MathF.Round(worldMouse.Y / _editorGridSize) * _editorGridSize)
                : worldMouse;

            int px = (int)MathF.Min(_editorDragStart.X, dragEnd.X);
            int py = (int)MathF.Min(_editorDragStart.Y, dragEnd.Y);
            int pw = (int)MathF.Abs(dragEnd.X - _editorDragStart.X);
            int ph = (int)MathF.Abs(dragEnd.Y - _editorDragStart.Y);
            if (pw < 8) pw = 32;
            if (ph < 8) ph = 12;

            Color previewColor = _editorTool switch
            {
                EditorTool.Platform => Color.White * 0.3f,
                EditorTool.Rope => new Color(120, 80, 40) * 0.5f,
                EditorTool.Wall => Color.Gray * 0.3f,
                EditorTool.Spike => Color.Red * 0.3f,
                EditorTool.Exit => Color.LimeGreen * 0.3f,
                EditorTool.WallSpike => Color.Red * 0.3f,
                EditorTool.SolidFloor => new Color(70, 50, 30) * 0.3f,
                EditorTool.Ceiling => Color.Gray * 0.3f,
                _ => Color.White * 0.2f
            };

            if (_editorTool == EditorTool.Rope)
                _spriteBatch.Draw(_pixel, new Rectangle((int)_editorDragStart.X - 1, py, 3, ph), previewColor);
            else if (_editorTool == EditorTool.Platform || _editorTool == EditorTool.Spike || _editorTool == EditorTool.Ceiling)
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, 12), previewColor);
            else
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), previewColor);
        }

        // Draw world bounds outline
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Top, bR - bL, 1), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Bottom, bR - bL, 1), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Top, 1, _level.Bounds.Bottom - _level.Bounds.Top), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bR, _level.Bounds.Top, 1, _level.Bounds.Bottom - _level.Bounds.Top), Color.Yellow * 0.2f);

        _spriteBatch.End();

        // Screen-space UI
        _spriteBatch.Begin();

        // Toolbar
        string[] toolNames = { "0:Floor", "1:Plat", "2:Rope", "3:Wall", "4:Spike", "5:Exit", "6:Spawn", "7:WSpike", "8:Overworld", "9:Ceiling" };
        float tx = 10;
        for (int i = 0; i < toolNames.Length; i++)
        {
            bool active = (int)_editorTool == i + 1;
            _spriteBatch.DrawString(_font, toolNames[i], new Vector2(tx, 10), active ? Color.Yellow : Color.Gray * 0.6f);
            tx += _font.MeasureString(toolNames[i]).X + 15;
        }

        // Grid snap indicator
        _spriteBatch.DrawString(_font, $"Grid: {(_editorGridSnap ? "ON" : "OFF")} [G]", new Vector2(10, 30), _editorGridSnap ? Color.LightGreen : Color.Gray * 0.5f);

        // Level name
        _spriteBatch.DrawString(_font, SafeText($"Level: {_level.Name}"), new Vector2(10, 50), Color.White * 0.6f);

        // Cursor world position
        _spriteBatch.DrawString(_font, $"Pos: {(int)_editorCursor.X}, {(int)_editorCursor.Y}", new Vector2(10, 70), Color.White * 0.4f);

        // Status message
        if (_editorStatusTimer > 0)
            _spriteBatch.DrawString(_font, SafeText(_editorStatusMsg), new Vector2(10, 570), Color.Yellow);

        // Controls hint
        _spriteBatch.DrawString(_font, "[=] Play  [Esc] Menu  [Drag] Place  [RClick] Delete  [Tab] Exit target  [F] Wall side", new Vector2(10, 550), Color.Gray * 0.35f);

        // Editor menu overlay
        if (_editorMenuOpen)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), Color.Black * 0.8f);

            if (_editorMenuMode == EditorMenuMode.SaveAs)
            {
                _spriteBatch.DrawString(_font, "SAVE AS  [Esc to cancel]", new Vector2(280, 200), Color.White);
                _spriteBatch.DrawString(_font, SafeText("Filename: " + _editorSaveAsName + "_"), new Vector2(280, 240), Color.Yellow);
                _spriteBatch.DrawString(_font, "(a-z, 0-9, dash. Enter to save)", new Vector2(280, 270), Color.Gray * 0.5f);
            }
            else if (_editorMenuMode == EditorMenuMode.LoadBrowser)
            {
                _spriteBatch.DrawString(_font, "LOAD LEVEL  [Esc to cancel]", new Vector2(280, 120), Color.White);
                if (_editorLevelFiles.Length == 0)
                {
                    _spriteBatch.DrawString(_font, "No levels found", new Vector2(300, 160), Color.Gray);
                }
                else
                {
                    for (int i = 0; i < _editorLevelFiles.Length; i++)
                    {
                        bool sel = i == _editorBrowserCursor;
                        var name = System.IO.Path.GetFileNameWithoutExtension(_editorLevelFiles[i]);
                        _spriteBatch.DrawString(_font, SafeText((sel ? "> " : "  ") + name),
                            new Vector2(300, 160 + i * 28), sel ? Color.Yellow : Color.Gray);
                    }
                }
            }
            else
            {
                string[] options = { "Save", "Save As...", "Load Level...", "New Level", "Delete Level", "Back to Game (=)", "Help" };
                float startY = 150f;
                _spriteBatch.DrawString(_font, "EDITOR MENU  [Esc to close]", new Vector2(280, startY - 40), Color.White);
                for (int i = 0; i < options.Length; i++)
                {
                    bool sel = i == _editorMenuCursor;
                    _spriteBatch.DrawString(_font, (sel ? "> " : "  ") + options[i],
                        new Vector2(300, startY + i * 30), sel ? Color.Yellow : Color.Gray);
                }

                // Current save file
                _spriteBatch.DrawString(_font, $"Current file: {_editorSaveFile}", new Vector2(280, startY + options.Length * 30 + 15), Color.Gray * 0.4f);

                // Help text
                float hy = startY + options.Length * 30 + 50;
                string[] help = {
                    "WASD = Move cursor (Shift = fast)",
                    "1-7 = Select tool",
                    "Click + Drag = Place object",
                    "Right Click = Delete object under cursor",
                    "G = Toggle grid snap",
                    "F = Cycle last wall's climb side",
                    "Tab = Cycle exit target (hover mouse over exit)",
                    "= = Switch to play mode (test level)",
                    "Esc = This menu",
                };
                foreach (var line in help)
                {
                    _spriteBatch.DrawString(_font, line, new Vector2(200, hy), Color.Gray * 0.6f);
                    hy += 22;
                }
            }
        }

        _spriteBatch.End();
    }

    // ===================== END EDITOR =====================

    private void DrawSettingsMenu()
    {
        float startY = 150f;
        float lineHeight = 30f;
        _spriteBatch.DrawString(_font, "SETTINGS  [Esc to close]", new Vector2(280, startY - 40), Color.White);

        for (int i = 0; i < _settings.Length; i++)
        {
            var s = _settings[i];
            bool selected = i == _menuCursor;
            string prefix = selected ? "> " : "  ";
            string value = s.IsAction ? "" : (s.Get() ? "  ON" : "  OFF");
            var color = selected ? Color.Yellow : Color.Gray;
            if (!s.IsAction && s.Get()) color = selected ? Color.Yellow : Color.LightGreen;
            if (s.IsAction) color = selected ? Color.Red : Color.DarkGray;

            _spriteBatch.DrawString(_font, $"{prefix}{s.Label}{value}", new Vector2(280, startY + i * lineHeight), color);
        }

        _spriteBatch.DrawString(_font, "[Space/Enter] Toggle  [W/S] Navigate", new Vector2(230, startY + _settings.Length * lineHeight + 20), Color.Gray * 0.6f);
    }

    private void UpdateMenu(KeyboardState kb)
    {
        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
            _menuCursor = (_menuCursor - 1 + _settings.Length) % _settings.Length;
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
            _menuCursor = (_menuCursor + 1) % _settings.Length;
        if (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up))
            _menuCursor = (_menuCursor - 1 + _settings.Length) % _settings.Length;
        if (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down))
            _menuCursor = (_menuCursor + 1) % _settings.Length;

        if ((kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
            (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)))
        {
            _settings[_menuCursor].Toggle();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 20, 20));

        // Editor draw
        if (_gameState == GameState.Editing)
        {
            DrawEditor();
            base.Draw(gameTime);
            return;
        }

        if (_gameState == GameState.Title)
        {
            _spriteBatch.Begin();

            // Title
            string title = "GENESYS";
            var titleSize = _font.MeasureString(title);
            _spriteBatch.DrawString(_font, title, new Vector2(400 - titleSize.X / 2, 180), Color.White);

            // Subtitle
            string sub = "Admin & Eve";
            var subSize = _font.MeasureString(sub);
            _spriteBatch.DrawString(_font, sub, new Vector2(400 - subSize.X / 2, 210), Color.Gray * 0.6f);

            // Menu options
            float startY = 300;
            float lineH = 35;
            for (int i = 0; i < _titleOptions.Length; i++)
            {
                bool selected = i == _titleCursor;
                string prefix = selected ? "> " : "  ";
                var color = selected ? Color.Yellow : Color.Gray;
                var text = $"{prefix}{_titleOptions[i]}";
                var size = _font.MeasureString(text);
                _spriteBatch.DrawString(_font, text, new Vector2(400 - size.X / 2, startY + i * lineH), color);
            }

            _spriteBatch.DrawString(_font, "[W/S] Navigate  [Space/Enter] Select", new Vector2(200, 480), Color.Gray * 0.4f);

            // Settings overlay on title screen
            if (_menuOpen && _settingsFromTitle)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), new Color(20, 20, 20));
                DrawSettingsMenu();
            }

            _spriteBatch.End();
            base.Draw(gameTime);
            return;
        }

        if (_isDead) GraphicsDevice.Clear(Color.DarkRed);

        // --- World rendering (camera transform) ---
        _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix);

        // Draw floor (extended across world)
        int floorY = _level.Floor.Y;
        int floorH = _level.Floor.Height;
        int bL = _level.Bounds.Left;
        int bR = _level.Bounds.Right;
        _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, floorH), new Color(40, 40, 40));
        _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, 2), new Color(80, 80, 80));

        // Draw platforms
        foreach (var plat in _level.PlatformRects)
        {
            _spriteBatch.Draw(_pixel, plat, new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(plat.X, plat.Y, plat.Width, 2), new Color(90, 90, 90));
        }

        // Draw spikes
        foreach (var spike in _level.SpikeRects)
        {
            _spriteBatch.Draw(_pixel, spike, Color.Red * 0.8f);
            // Triangle-ish look: draw smaller rects stacked
            int teeth = spike.Width / 12;
            for (int t = 0; t < teeth; t++)
            {
                int tx = spike.X + t * 12 + 2;
                _spriteBatch.Draw(_pixel, new Rectangle(tx, spike.Y - 4, 8, 4), Color.Red * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle(tx + 2, spike.Y - 8, 4, 4), Color.Red * 0.4f);
            }
        }

        // Draw ceilings
        foreach (var ceil in _level.CeilingRects)
        {
            _spriteBatch.Draw(_pixel, ceil, new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(ceil.X, ceil.Bottom - 2, ceil.Width, 2), new Color(90, 90, 90));
        }

        // Draw solid floors
        foreach (var sf in _level.SolidFloorRects)
        {
            _spriteBatch.Draw(_pixel, sf, new Color(70, 50, 30));
            _spriteBatch.Draw(_pixel, new Rectangle(sf.X, sf.Y, sf.Width, 2), new Color(110, 80, 50));
        }

        // Draw wall spikes
        foreach (var ws in _level.WallSpikes)
        {
            var wsRect = new Rectangle(ws.X, ws.Y, ws.W, ws.H);
            _spriteBatch.Draw(_pixel, wsRect, Color.Red * 0.8f);
            int teeth = ws.H / 12;
            for (int t = 0; t < teeth; t++)
            {
                int ty = ws.Y + t * 12 + 2;
                int tipX = ws.Side == 1 ? ws.X + ws.W : ws.X - 4;
                _spriteBatch.Draw(_pixel, new Rectangle(tipX, ty, 4, 8), Color.Red * 0.6f);
                int tipX2 = ws.Side == 1 ? tipX + 4 : tipX - 4;
                _spriteBatch.Draw(_pixel, new Rectangle(tipX2, ty + 2, 4, 4), Color.Red * 0.4f);
            }
        }

        // Draw walls
        foreach (var wall in _level.WallRects)
        {
            _spriteBatch.Draw(_pixel, wall, _enableWallClimb ? new Color(60, 60, 60) : new Color(45, 45, 45));
        }
        foreach (var ledge in _level.WallLedges)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(ledge.X, ledge.Y, ledge.Width, 2), new Color(100, 100, 100));
        }

        // Draw ropes
        if (_enableRopeClimb)
        {
            for (int i = 0; i < _level.RopeXPositions.Length; i++)
            {
                int rx = (int)_level.RopeXPositions[i];
                int rt = (int)_level.RopeTops[i];
                int rb = (int)_level.RopeBottoms[i];
                _spriteBatch.Draw(_pixel, new Rectangle(rx - 1, rt, 3, rb - rt), new Color(120, 80, 40));
            }
        }

        // Draw exits
        for (int i = 0; i < _level.ExitRects.Length; i++)
        {
            bool isOverworld = _level.ExitTargets.Length > i && _level.ExitTargets[i] == "__overworld__";
            _spriteBatch.Draw(_pixel, _level.ExitRects[i], isOverworld ? Color.CornflowerBlue * 0.5f : Color.LimeGreen * 0.5f);
        }

        // Draw player
        if (!_isDead) _player.Draw(_spriteBatch, _pixel);

        // Draw bullets
        _bullets.ForEach(b => b.Draw(_spriteBatch, _pixel));

        // Draw enemies
        _enemies.ForEach(e => e.Draw(_spriteBatch, _pixel));

        _spriteBatch.End();

        // --- UI rendering (no camera transform, screen-space) ---
        _spriteBatch.Begin();

        // Death overlay
        if (_isDead)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), Color.Black * 0.6f);
        }

        // --- Settings menu overlay ---
        if (_menuOpen)
        {
            // Semi-transparent dim when in-game (gameplay visible behind)
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), Color.Black * 0.7f);
            DrawSettingsMenu();
        }
        else if (!_isDead)
        {
            // Minimal HUD
            _spriteBatch.DrawString(_font, "[Esc] Menu", new Vector2(10, 10), Color.Gray * 0.5f);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
