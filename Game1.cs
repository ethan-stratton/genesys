using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace ArenaShooter;

public enum GameState { Title, Playing, Editing, Overworld }

public class Game1 : Game
{
    private GameState _gameState = GameState.Title;
    private int _titleCursor;
    private bool _settingsFromTitle;
    private string[] _titleOptions => SaveData.Exists()
        ? new[] { "Continue", "New Game", "Settings", "Quit" }
        : new[] { "New Game", "Settings", "Quit" };

    private OverworldData _overworld;
    private int _overworldCursor;
    private string _currentNodeId;
    private const string OverworldPath = "Content/overworld.json";

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
    private float _spawnInvincibility;
    private bool[] _prevInExit = Array.Empty<bool>();
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

    // --- Dialogue state ---
    private bool _dialogueOpen;
    private int _dialogueNpcIndex = -1;
    private int _dialogueLine;

    // --- EVE orb ---
    private float _totalTime;
    private bool _eveOrbActive;
    #pragma warning disable CS0414
    private bool _eveDialogueExhausted; // used later for quest tracking
    #pragma warning restore CS0414

    // --- Editor state ---
    private enum EditorTool { SolidFloor = 0, Platform = 1, Rope = 2, Wall = 3, Spike = 4, Exit = 5, Spawn = 6, WallSpike = 7, OverworldExit = 8, Ceiling = 9 }
    // Wall climbSide values: 0=both, 1=right face, -1=left face, 99=no climb (solid only)
    private EditorTool _editorTool = EditorTool.Platform;
    private bool _toolPaletteOpen;
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
            new() { Label = "EVE Orb", Get = () => _eveOrbActive, Toggle = () => _eveOrbActive = !_eveOrbActive },
            new() { Label = "Music", Get = () => _enableMusic, Toggle = () => { _enableMusic = !_enableMusic; if (_enableMusic) { MediaPlayer.IsRepeating = true; MediaPlayer.Play(_bgm); } else { MediaPlayer.Stop(); } } },
            new() { Label = "Quit Game", Get = () => false, Toggle = () => Exit(), IsAction = true },
        };

        Restart();

        if (System.IO.File.Exists(OverworldPath))
            _overworld = OverworldData.Load(OverworldPath);
        else
            _overworld = new OverworldData();
        _currentNodeId = _overworld.StartNode;

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
                // Find any level file
                if (!System.IO.Directory.Exists("Content/levels"))
                    System.IO.Directory.CreateDirectory("Content/levels");
                var files = System.IO.Directory.GetFiles("Content/levels", "*.json");
                if (files.Length > 0)
                {
                    LoadLevel(files[0]);
                    _editorSaveFile = files[0];
                }
                else
                {
                    _level = new LevelData { Name = "untitled" };
                    _level.Build();
                    _editorSaveFile = "Content/levels/untitled.json";
                    SaveLevel();
                }
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
        _spawnInvincibility = 1.0f;
        _prevInExit = Array.Empty<bool>();
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

        if (_gameState == GameState.Overworld)
        {
            UpdateOverworld(kb);
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

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
                            _prevInExit = new bool[_level.ExitRects.Length];
                            for (int k = 0; k < _prevInExit.Length; k++)
                                _prevInExit[k] = true;
                        }
                        break;
                    case "New Game":
                        SaveData.Delete();
                        _saveData = new SaveData();
                        LoadLevel(DefaultLevel);
                        _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                        _gameState = GameState.Playing;
                        Restart();
                        _prevInExit = new bool[_level.ExitRects.Length];
                        for (int k = 0; k < _prevInExit.Length; k++)
                            _prevInExit[k] = true;
                        _saveData.CurrentLevel = System.IO.Path.GetFileNameWithoutExtension(DefaultLevel);
                        _saveData.SpawnX = _player.Position.X;
                        _saveData.SpawnY = _player.Position.Y;
                        _saveData.Save();
                        // Reset overworld
                        if (System.IO.File.Exists(OverworldPath))
                            _overworld = OverworldData.Load(OverworldPath);
                        else
                            _overworld = new OverworldData();
                        _currentNodeId = _overworld.StartNode;
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

        // M key — open overworld map
        if (kb.IsKeyDown(Keys.M) && _prevKb.IsKeyUp(Keys.M) && !_menuOpen && !_dialogueOpen)
        {
            if (_overworld != null)
            {
                _overworldCursor = Array.IndexOf(_overworld.Nodes, _overworld.FindNode(
                    System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile)));
                if (_overworldCursor < 0) _overworldCursor = 0;
                _currentNodeId = _overworld.Nodes[_overworldCursor].Id;
                _gameState = GameState.Overworld;
                _prevKb = kb;
                return;
            }
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
        _totalTime += dt;

        // Spawn invincibility countdown
        if (_spawnInvincibility > 0f)
            _spawnInvincibility -= dt;

        // Exit re-entry cooldown
        // (exit enter-trigger tracking happens in exit collision section below)

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

        // --- Dialogue system ---
        if (_dialogueOpen)
        {
            bool advance = (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                           (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                           (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W));
            bool close = kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape);
            if (close)
            {
                _dialogueOpen = false;
                _dialogueNpcIndex = -1;
            }
            else if (advance)
            {
                _dialogueLine++;
                if (_dialogueNpcIndex >= 0 && _dialogueLine >= _level.Npcs[_dialogueNpcIndex].Dialogue.Length)
                {
                    // Check if EVE dialogue exhausted
                    if (_level.Npcs[_dialogueNpcIndex].Id == "eve")
                    {
                        _eveDialogueExhausted = true;
                        _eveOrbActive = true;
                    }
                    _dialogueOpen = false;
                    _dialogueNpcIndex = -1;
                }
            }
            _prevKb = kb;
            base.Update(gameTime);
            return; // block all other input
        }

        // Check for NPC interaction (W freshly pressed, grounded, near NPC)
        if (_player.IsGrounded && MathF.Abs(_player.Velocity.Y) < 1f &&
            kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
        {
            var pRect = new Rectangle((int)_player.Position.X - 40, (int)_player.Position.Y,
                Player.Width + 80, Player.Height);
            for (int i = 0; i < _level.NpcRects.Length; i++)
            {
                if (pRect.Intersects(_level.NpcRects[i]) && _level.Npcs[i].Dialogue.Length > 0)
                {
                    _dialogueOpen = true;
                    _dialogueNpcIndex = i;
                    _dialogueLine = 0;
                    _prevKb = kb;
                    base.Update(gameTime);
                    return;
                }
            }
        }

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
                if (_spawnInvincibility <= 0f)
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
                    if (_spawnInvincibility <= 0f)
                        _isDead = true;
                    break;
                }
            }
        }

        // Exit collision — enter-trigger (only fires on transition from outside → inside)
        if (!_isDead)
        {
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            if (_prevInExit.Length != _level.ExitRects.Length)
                _prevInExit = new bool[_level.ExitRects.Length];
            bool[] curInExit = new bool[_level.ExitRects.Length];
            bool transitioned = false;
            for (int i = 0; i < _level.ExitRects.Length; i++)
            {
                curInExit[i] = pRect.Intersects(_level.ExitRects[i]);
                if (curInExit[i] && !_prevInExit[i] && _level.ExitTargets[i] != "")
                {
                    string target = _level.ExitTargets[i];
                    string targetExitId = _level.ExitTargetExitIds[i];
                    if (target == "__overworld__")
                    {
                        // Mark current level's node as cleared and discover connected nodes
                        if (_overworld != null)
                        {
                            var node = _overworld.FindNode(System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile));
                            if (node != null)
                            {
                                node.Cleared = true;
                                node.Discovered = true;
                                _currentNodeId = node.Id;
                                foreach (var connId in node.Connections)
                                {
                                    var conn = _overworld.FindNode(connId);
                                    if (conn != null) conn.Discovered = true;
                                }
                            }
                            _overworldCursor = Array.IndexOf(_overworld.Nodes, _overworld.FindNode(_currentNodeId));
                            if (_overworldCursor < 0) _overworldCursor = 0;
                            _overworld.Save(OverworldPath);
                        }
                        _gameState = GameState.Overworld;
                        break;
                    }
                    string nextPath = $"Content/levels/{target}.json";
                    string _sourceLevel = System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile);
                    if (System.IO.File.Exists(nextPath))
                    {
                        LoadLevel(nextPath);
                        _editorSaveFile = nextPath;
                        _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                        Restart();

                        // Tunnel: reposition at target exit
                        if (!string.IsNullOrEmpty(targetExitId))
                        {
                            for (int j = 0; j < _level.ExitIds.Length; j++)
                            {
                                if (_level.ExitIds[j] == targetExitId)
                                {
                                    var exitRect = _level.ExitRects[j];
                                    float px = exitRect.X + (exitRect.Width - Player.Width) / 2f;
                                    float py = exitRect.Y + exitRect.Height - Player.Height;
                                    _player.Position = new Vector2(px, py);
                                    _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                                    break;
                                }
                            }
                        }

                        // Mark all exits in NEW level as "already inside" so enter-trigger doesn't re-fire
                        _prevInExit = new bool[_level.ExitRects.Length];
                        for (int k = 0; k < _prevInExit.Length; k++)
                            _prevInExit[k] = true;
                        transitioned = true;

                        if (_saveData != null)
                        {
                            _saveData.CurrentLevel = target;
                            _saveData.SpawnX = _player.Position.X;
                            _saveData.SpawnY = _player.Position.Y;
                            _saveData.Save();
                        }
                        if (_overworld != null)
                        {
                            // Mark the level we just LEFT as cleared
                            var srcNode = _overworld.FindNode(System.IO.Path.GetFileNameWithoutExtension(
                                _editorSaveFile.Replace($"Content/levels/{target}.json", "")));
                            // Actually, _editorSaveFile is already updated. Use the source level name.
                            // We need to track it before LoadLevel overwrites _editorSaveFile.
                            // (handled by _sourceLevel below)
                            if (_sourceLevel != null)
                            {
                                var srcN = _overworld.FindNode(_sourceLevel);
                                if (srcN != null)
                                {
                                    srcN.Cleared = true;
                                    srcN.Discovered = true;
                                    foreach (var connId in srcN.Connections)
                                    {
                                        var conn = _overworld.FindNode(connId);
                                        if (conn != null) conn.Discovered = true;
                                    }
                                }
                            }
                            var destNode = _overworld.FindNode(target);
                            if (destNode != null)
                            {
                                destNode.Discovered = true;
                            }
                            _overworld.Save(OverworldPath);
                        }
                    }
                    break;
                }
            }
            if (!transitioned)
                _prevInExit = curInExit;
        }

        _prevKb = kb;
        base.Update(gameTime);
    }

    private static Color ParseNpcColor(string name)
    {
        return name switch
        {
            "Purple" => Color.Purple,
            "Blue" => Color.Blue,
            "Red" => Color.Red,
            "Green" => Color.Green,
            "Yellow" => Color.Yellow,
            "White" => Color.White,
            "Orange" => Color.Orange,
            "Cyan" => Color.Cyan,
            "Pink" => Color.Pink,
            "Magenta" => Color.Magenta,
            "SaddleBrown" => new Color(139, 69, 19),
            _ => Color.Purple,
        };
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

        // Editor menu (Esc) — if palette is open, close palette instead
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            if (_toolPaletteOpen)
            {
                _toolPaletteOpen = false;
            }
            else
            {
                _editorMenuOpen = !_editorMenuOpen;
                _editorMenuCursor = 0;
                _editorMenuMode = EditorMenuMode.Main;
            }
        }

        if (_editorMenuOpen)
        {
            UpdateEditorMenu(kb);
            return;
        }

        // Tool palette toggle with Q (only when not hovering an exit)
        if (kb.IsKeyDown(Keys.Q) && _prevKb.IsKeyUp(Keys.Q))
        {
            _toolPaletteOpen = !_toolPaletteOpen;
        }

        // Tool palette input
        if (_toolPaletteOpen)
        {
            int toolCount = Enum.GetValues<EditorTool>().Length;
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _editorTool = (EditorTool)(((int)_editorTool - 1 + toolCount) % toolCount);
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _editorTool = (EditorTool)(((int)_editorTool + 1) % toolCount);

            // Number keys select and close
            if (kb.IsKeyDown(Keys.D0) && _prevKb.IsKeyUp(Keys.D0)) { _editorTool = EditorTool.SolidFloor; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1)) { _editorTool = EditorTool.Platform; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2)) { _editorTool = EditorTool.Rope; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D3) && _prevKb.IsKeyUp(Keys.D3)) { _editorTool = EditorTool.Wall; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D4) && _prevKb.IsKeyUp(Keys.D4)) { _editorTool = EditorTool.Spike; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D5) && _prevKb.IsKeyUp(Keys.D5)) { _editorTool = EditorTool.Exit; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D6) && _prevKb.IsKeyUp(Keys.D6)) { _editorTool = EditorTool.Spawn; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D7) && _prevKb.IsKeyUp(Keys.D7)) { _editorTool = EditorTool.WallSpike; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D8) && _prevKb.IsKeyUp(Keys.D8)) { _editorTool = EditorTool.OverworldExit; _toolPaletteOpen = false; }
            if (kb.IsKeyDown(Keys.D9) && _prevKb.IsKeyUp(Keys.D9)) { _editorTool = EditorTool.Ceiling; _toolPaletteOpen = false; }

            // Space/Enter confirm and close
            if ((kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)))
                _toolPaletteOpen = false;

            // Esc closes without changing (but Esc is already handled above for editor menu, so only if menu didn't open)
            // Actually Esc was handled above and would toggle editor menu. We handle it here as a special close.
            // Since Esc above toggles _editorMenuOpen, we need to undo that if palette was open.
            // Simpler: just return here to skip all other input.
            return;
        }

        // Back to play mode with =
        if (kb.IsKeyDown(Keys.OemPlus) && _prevKb.IsKeyUp(Keys.OemPlus))
        {
            SaveLevel(); // auto-save on exit editor
            _gameState = GameState.Playing;
            // Rebuild level arrays
            _level.Build();
            _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
            Restart();
            // Suppress exit triggers so spawning on a loading zone doesn't teleport you
            _prevInExit = new bool[_level.ExitRects.Length];
            for (int k = 0; k < _prevInExit.Length; k++)
                _prevInExit[k] = true;
            // Update save data
            if (_saveData != null)
            {
                _saveData.CurrentLevel = System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile);
                _saveData.SpawnX = _player.Position.X;
                _saveData.SpawnY = _player.Position.Y;
                _saveData.Save();
            }
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
                    var exitId = GenerateExitId(x, y);
                    var eList = new List<ExitData>(_level.Exits);
                    eList.Add(new ExitData { X = x, Y = y, W = w, H = h, TargetLevel = "", Id = exitId });
                    _level.Exits = eList.ToArray();
                    _level.Build();
                    SetEditorStatus($"Exit added (ID: {exitId}, Tab to set target)");
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
                    var owExitId = GenerateExitId(x, y);
                    var owList = new List<ExitData>(_level.Exits);
                    owList.Add(new ExitData { X = x, Y = y, W = w, H = h, TargetLevel = "__overworld__", Id = owExitId });
                    _level.Exits = owList.ToArray();
                    _level.Build();
                    SetEditorStatus($"Overworld exit added (ID: {owExitId})");
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
        // Shift+Tab — cycle targetExitId
        // Ctrl+Tab — cycle exit's own id
        if (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab))
        {
            var worldMouse2 = Vector2.Transform(new Vector2(mouse.X, mouse.Y), Matrix.Invert(_camera.TransformMatrix));
            var mp = new Point((int)worldMouse2.X, (int)worldMouse2.Y);
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);

            for (int i = 0; i < _level.Exits.Length; i++)
            {
                var e = _level.Exits[i];
                if (new Rectangle(e.X, e.Y, e.W, e.H).Contains(mp))
                {
                    if (ctrl)
                    {
                        // Cycle exit's own id
                        string[] suggestions = { "exit-left", "exit-right", "exit-top", "exit-bottom", "exit-overworld",
                            $"exit-{i}", $"exit-{i + 1}" };
                        var options = new List<string>(suggestions);
                        if (!options.Contains(e.Id) && !string.IsNullOrEmpty(e.Id))
                            options.Insert(0, e.Id);
                        int idx = options.IndexOf(e.Id);
                        idx = (idx + 1) % options.Count;
                        _level.Exits[i].Id = options[idx];
                        _level.Build();
                        SetEditorStatus($"Exit ID: {options[idx]}");
                    }
                    else if (shift)
                    {
                        // Cycle targetExitId from target level's exits
                        if (!string.IsNullOrEmpty(e.TargetLevel) && e.TargetLevel != "__overworld__")
                        {
                            string targetPath = $"Content/levels/{e.TargetLevel}.json";
                            if (System.IO.File.Exists(targetPath))
                            {
                                try
                                {
                                    var targetLevel = LevelData.Load(targetPath);
                                    var exitIds = new List<string> { "" }; // "" = none
                                    foreach (var te in targetLevel.Exits)
                                    {
                                        if (!string.IsNullOrEmpty(te.Id))
                                            exitIds.Add(te.Id);
                                    }
                                    int idx = exitIds.IndexOf(e.TargetExitId);
                                    idx = (idx + 1) % exitIds.Count;
                                    _level.Exits[i].TargetExitId = exitIds[idx];
                                    _level.Build();
                                    SetEditorStatus($"Target exit: {(exitIds[idx] == "" ? "(none)" : exitIds[idx])}");
                                }
                                catch
                                {
                                    SetEditorStatus("Failed to read target level");
                                }
                            }
                            else
                            {
                                SetEditorStatus("Target level file not found");
                            }
                        }
                        else
                        {
                            SetEditorStatus("Set a target level first (Tab)");
                        }
                    }
                    else
                    {
                        // Cycle target level
                        var levelsDir = "Content/levels";
                        var files = System.IO.Directory.Exists(levelsDir)
                            ? System.IO.Directory.GetFiles(levelsDir, "*.json")
                            : Array.Empty<string>();
                        var names = new List<string> { "" };
                        foreach (var f in files)
                            names.Add(System.IO.Path.GetFileNameWithoutExtension(f));
                        int idx = names.IndexOf(e.TargetLevel);
                        idx = (idx + 1) % names.Count;
                        _level.Exits[i].TargetLevel = names[idx];
                        _level.Build();
                        SetEditorStatus($"Exit target: {(names[idx] == "" ? "(none)" : names[idx])}");
                    }
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

    private string GenerateExitId(int x, int y)
    {
        int boundsW = _level.Bounds.Right - _level.Bounds.Left;
        int boundsH = _level.Bounds.Bottom - _level.Bounds.Top;
        string baseName;
        if (x < _level.Bounds.Left + boundsW * 0.2f)
            baseName = "exit-left";
        else if (x > _level.Bounds.Right - boundsW * 0.2f)
            baseName = "exit-right";
        else if (y < _level.Bounds.Top + boundsH * 0.2f)
            baseName = "exit-top";
        else
            baseName = $"exit-{_level.Exits.Length}";

        // Deduplicate
        var existing = new HashSet<string>();
        foreach (var e in _level.Exits) existing.Add(e.Id);
        if (!existing.Contains(baseName)) return baseName;
        for (int n = 2; ; n++)
        {
            string candidate = $"{baseName}-{n}";
            if (!existing.Contains(candidate)) return candidate;
        }
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
                        _level = new LevelData { Name = "untitled" };
                        _level.Build();
                        _editorSaveFile = "Content/levels/untitled.json";
                        SaveLevel();
                    }
                    _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                    _editorCursor = new Vector2(_level.PlayerSpawn.X, _level.PlayerSpawn.Y);
                    _editorMenuOpen = false;
                    break;
                case 5: // Back to game
                    SaveLevel(); // auto-save level edits
                    _gameState = GameState.Playing;
                    _level.Build();
                    _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                    Restart();
                    // Suppress exit triggers so spawning on a loading zone doesn't teleport you
                    _prevInExit = new bool[_level.ExitRects.Length];
                    for (int k = 0; k < _prevInExit.Length; k++)
                        _prevInExit[k] = true;
                    // Update save data with current level
                    if (_saveData != null)
                    {
                        _saveData.CurrentLevel = System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile);
                        _saveData.SpawnX = _player.Position.X;
                        _saveData.SpawnY = _player.Position.Y;
                        _saveData.Save();
                    }
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

        // Draw exits (with target labels and ids)
        foreach (var e in _level.Exits)
        {
            bool isOw = e.TargetLevel == "__overworld__";
            Color exitColor;
            if (isOw)
            {
                exitColor = Color.CornflowerBlue;
            }
            else if (!string.IsNullOrEmpty(e.TargetLevel) && !string.IsNullOrEmpty(e.TargetExitId)
                     && System.IO.File.Exists($"Content/levels/{e.TargetLevel}.json"))
            {
                exitColor = Color.LimeGreen;
            }
            else if (!string.IsNullOrEmpty(e.TargetLevel))
            {
                exitColor = Color.Yellow;
            }
            else
            {
                exitColor = Color.OrangeRed;
            }

            _spriteBatch.Draw(_pixel, new Rectangle(e.X, e.Y, e.W, e.H), exitColor * 0.4f);

            // Exit's own ID above
            if (!string.IsNullOrEmpty(e.Id))
                _spriteBatch.DrawString(_font, SafeText(e.Id), new Vector2(e.X, e.Y - 30), exitColor * 0.6f);

            // Target level name
            string label = isOw ? "OVERWORLD" : (string.IsNullOrEmpty(e.TargetLevel) ? "(?)" : e.TargetLevel);
            _spriteBatch.DrawString(_font, SafeText(label), new Vector2(e.X, e.Y - 16), exitColor * 0.7f);

            // Target exit id below target level name
            if (!string.IsNullOrEmpty(e.TargetExitId) && !isOw)
                _spriteBatch.DrawString(_font, SafeText($"-> {e.TargetExitId}"), new Vector2(e.X, e.Y + e.H + 2), exitColor * 0.5f);
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

        // Draw NPCs in editor
        for (int i = 0; i < _level.Npcs.Length; i++)
        {
            var npc = _level.Npcs[i];
            if (npc.Id == "eve")
            {
                float cx = npc.X + npc.W / 2f;
                float cy = npc.Y + npc.H / 2f;
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 11), (int)(cy - 11), 22, 22), Color.Cyan * 0.1f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 8), (int)(cy - 8), 16, 16), Color.Cyan * 0.25f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 6), (int)(cy - 6), 12, 12), Color.Cyan * 0.7f);
                _spriteBatch.DrawString(_font, SafeText(npc.Name), new Vector2(npc.X, npc.Y - 16), Color.Cyan * 0.8f);
            }
            else
            {
                Color npcColor = ParseNpcColor(npc.Color);
                _spriteBatch.Draw(_pixel, new Rectangle(npc.X, npc.Y, npc.W, npc.H), npcColor * 0.7f);
                _spriteBatch.DrawString(_font, SafeText(npc.Name), new Vector2(npc.X, npc.Y - 16), npcColor * 0.8f);
            }
        }

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
            bool active = (int)_editorTool == i;
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
        _spriteBatch.DrawString(_font, "[=] Play  [Esc] Menu  [Q] Tools  [Drag] Place  [RClick] Delete  [Tab] Target", new Vector2(10, 550), Color.Gray * 0.35f);

        // Tool palette overlay
        if (_toolPaletteOpen)
        {
            // Semi-transparent background
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), Color.Black * 0.7f);

            string[] paletteNames = { "Solid Floor", "Platform", "Rope", "Wall", "Spike", "Exit", "Spawn", "Wall Spike", "Overworld Exit", "Ceiling" };
            int paletteCount = paletteNames.Length;
            float palW = 260f;
            float palLineH = 24f;
            float palH = paletteCount * palLineH + 60f;
            float palX = (800 - palW) / 2f;
            float palY = (600 - palH) / 2f;

            // Background box
            _spriteBatch.Draw(_pixel, new Rectangle((int)palX, (int)palY, (int)palW, (int)palH), new Color(30, 30, 30) * 0.95f);
            // Border
            _spriteBatch.Draw(_pixel, new Rectangle((int)palX, (int)palY, (int)palW, 2), Color.Gray * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)palX, (int)(palY + palH - 2), (int)palW, 2), Color.Gray * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)palX, (int)palY, 2, (int)palH), Color.Gray * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(palX + palW - 2), (int)palY, 2, (int)palH), Color.Gray * 0.6f);

            // Title
            _spriteBatch.DrawString(_font, "TOOL PALETTE", new Vector2(palX + 10, palY + 8), Color.White);

            // Tool list
            for (int i = 0; i < paletteCount; i++)
            {
                bool active = (int)_editorTool == i;
                float itemY = palY + 32 + i * palLineH;
                if (active)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(palX + 4), (int)itemY, (int)(palW - 8), (int)palLineH), Color.Yellow * 0.15f);
                string label = $"[{i}] {paletteNames[i]}";
                _spriteBatch.DrawString(_font, label, new Vector2(palX + 14, itemY + 3), active ? Color.Yellow : Color.Gray);
            }

            // Footer
            _spriteBatch.DrawString(_font, "Q to close  W/S navigate", new Vector2(palX + 10, palY + palH - 24), Color.Gray * 0.5f);
        }

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

        int itemCount = _settings.Length - 1; // exclude Quit
        int half = (itemCount + 1) / 2;
        for (int i = 0; i < itemCount; i++)
        {
            var s = _settings[i];
            bool selected = i == _menuCursor;
            string prefix = selected ? "> " : "  ";
            string value = s.IsAction ? "" : (s.Get() ? "  ON" : "  OFF");
            var color = selected ? Color.Yellow : Color.Gray;
            if (!s.IsAction && s.Get()) color = selected ? Color.Yellow : Color.LightGreen;

            int col = i < half ? 0 : 1;
            int row = i < half ? i : i - half;
            float x = col == 0 ? 180f : 450f;
            float y = startY + row * lineHeight;

            _spriteBatch.DrawString(_font, $"{prefix}{s.Label}{value}", new Vector2(x, y), color);
        }

        // Draw Quit centered below both columns
        {
            int quitIdx = _settings.Length - 1;
            var s = _settings[quitIdx];
            bool selected = _menuCursor == quitIdx;
            string prefix = selected ? "> " : "  ";
            var color = selected ? Color.Red : Color.DarkGray;
            float quitY = startY + half * lineHeight + 10;
            _spriteBatch.DrawString(_font, $"{prefix}{s.Label}", new Vector2(315f, quitY), color);
        }

        _spriteBatch.DrawString(_font, "[Space/Enter] Toggle  [W/S] Navigate  [A/D] Column", new Vector2(180, startY + half * lineHeight + 50), Color.Gray * 0.6f);
    }

    private void UpdateMenu(KeyboardState kb)
    {
        int quitIdx = _settings.Length - 1;
        int itemCount = _settings.Length - 1; // exclude Quit from columns
        int half = (itemCount + 1) / 2;
        int rightCount = itemCount - half;

        bool onQuit = _menuCursor == quitIdx;

        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
        {
            if (onQuit)
            {
                // Go to last item of left column
                _menuCursor = half - 1;
            }
            else
            {
                int col = _menuCursor < half ? 0 : 1;
                int row = col == 0 ? _menuCursor : _menuCursor - half;
                if (row == 0)
                {
                    // Wrap up to Quit
                    _menuCursor = quitIdx;
                }
                else
                {
                    row--;
                    _menuCursor = col == 0 ? row : row + half;
                }
            }
        }
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
        {
            if (onQuit)
            {
                _menuCursor = 0;
            }
            else
            {
                int col = _menuCursor < half ? 0 : 1;
                int row = col == 0 ? _menuCursor : _menuCursor - half;
                int colSize = col == 0 ? half : rightCount;
                if (row == colSize - 1)
                {
                    _menuCursor = quitIdx;
                }
                else
                {
                    row++;
                    _menuCursor = col == 0 ? row : row + half;
                }
            }
        }
        if (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up))
        {
            if (onQuit)
            {
                _menuCursor = half - 1;
            }
            else
            {
                int col = _menuCursor < half ? 0 : 1;
                int row = col == 0 ? _menuCursor : _menuCursor - half;
                if (row == 0)
                    _menuCursor = quitIdx;
                else
                {
                    row--;
                    _menuCursor = col == 0 ? row : row + half;
                }
            }
        }
        if (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down))
        {
            if (onQuit)
            {
                _menuCursor = 0;
            }
            else
            {
                int col = _menuCursor < half ? 0 : 1;
                int row = col == 0 ? _menuCursor : _menuCursor - half;
                int colSize = col == 0 ? half : rightCount;
                if (row == colSize - 1)
                    _menuCursor = quitIdx;
                else
                {
                    row++;
                    _menuCursor = col == 0 ? row : row + half;
                }
            }
        }

        // A/D or Left/Right to switch columns (no-op on Quit)
        if (!onQuit)
        {
            if ((kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D)) ||
                (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right)))
            {
                if (_menuCursor < half && _menuCursor + half < itemCount)
                    _menuCursor += half;
            }
            if ((kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A)) ||
                (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left)))
            {
                if (_menuCursor >= half)
                    _menuCursor -= half;
            }
        }

        if ((kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
            (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)))
        {
            _settings[_menuCursor].Toggle();
        }
    }

    private void UpdateOverworld(KeyboardState kb)
    {
        if (_overworld == null || _overworld.Nodes.Length == 0) return;

        var currentNode = _overworld.Nodes[_overworldCursor];

        if ((kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D)) ||
            (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right)))
            MoveOverworldCursor(currentNode, 1, 0);
        if ((kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A)) ||
            (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left)))
            MoveOverworldCursor(currentNode, -1, 0);
        if ((kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W)) ||
            (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up)))
            MoveOverworldCursor(currentNode, 0, -1);
        if ((kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S)) ||
            (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down)))
            MoveOverworldCursor(currentNode, 0, 1);

        if ((kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
            (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)))
        {
            var node = _overworld.Nodes[_overworldCursor];
            if (node.Discovered && !string.IsNullOrEmpty(node.Level))
            {
                string path = $"Content/levels/{node.Level}.json";
                if (System.IO.File.Exists(path))
                {
                    node.Discovered = true;
                    _overworld.Save(OverworldPath);
                    LoadLevel(path);
                    _editorSaveFile = path;
                    _camera = new Camera(800, 600, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);
                    _gameState = GameState.Playing;
                    Restart();
                    _prevInExit = new bool[_level.ExitRects.Length];
                    for (int k = 0; k < _prevInExit.Length; k++)
                        _prevInExit[k] = true;
                }
            }
        }

        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _gameState = GameState.Title;
            _titleCursor = 0;
        }
    }

    private void MoveOverworldCursor(OverworldNode current, int dirX, int dirY)
    {
        OverworldNode best = null;
        int bestIdx = -1;
        float bestDist = float.MaxValue;

        foreach (var connId in current.Connections)
        {
            var conn = _overworld.FindNode(connId);
            if (conn == null || !conn.Discovered) continue;

            int dx = conn.X - current.X;
            int dy = conn.Y - current.Y;

            bool valid = false;
            if (dirX > 0 && dx > 0) valid = true;
            if (dirX < 0 && dx < 0) valid = true;
            if (dirY > 0 && dy > 0) valid = true;
            if (dirY < 0 && dy < 0) valid = true;

            if (!valid) continue;

            float dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = conn;
                bestIdx = Array.IndexOf(_overworld.Nodes, conn);
            }
        }

        if (best != null && bestIdx >= 0)
        {
            _overworldCursor = bestIdx;
            _currentNodeId = best.Id;
        }
    }

    private void DrawOverworld()
    {
        GraphicsDevice.Clear(new Color(8, 8, 16));

        _spriteBatch.Begin();

        _spriteBatch.DrawString(_font, SafeText("OVERWORLD"), new Vector2(340, 30), Color.White);

        // Draw connections
        foreach (var node in _overworld.Nodes)
        {
            if (!node.Discovered) continue;
            foreach (var connId in node.Connections)
            {
                var conn = _overworld.FindNode(connId);
                if (conn == null || !conn.Discovered) continue;
                DrawLine(node.X, node.Y, conn.X, conn.Y, node.Cleared && conn.Cleared ? Color.White * 0.6f : Color.Gray * 0.3f);
            }
        }

        // Draw nodes
        for (int i = 0; i < _overworld.Nodes.Length; i++)
        {
            var node = _overworld.Nodes[i];
            bool selected = i == _overworldCursor;

            if (!node.Discovered)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(node.X - 4, node.Y - 4, 8, 8), Color.DarkGray * 0.3f);
                continue;
            }

            Color nodeColor = node.Cleared ? Color.LimeGreen : Color.Gold;
            int nodeSize = selected ? 14 : 10;

            if (selected)
                _spriteBatch.Draw(_pixel, new Rectangle(node.X - nodeSize, node.Y - nodeSize, nodeSize * 2, nodeSize * 2), Color.White * 0.15f);

            _spriteBatch.Draw(_pixel, new Rectangle(node.X - nodeSize / 2, node.Y - nodeSize / 2, nodeSize, nodeSize), nodeColor);

            string name = SafeText(node.ShownName);
            var nameSize = _font.MeasureString(name);
            float nameX = node.X - nameSize.X / 2f;
            float nameY = node.Y - nodeSize / 2f - 20;
            _spriteBatch.DrawString(_font, name, new Vector2(nameX, nameY), selected ? Color.White : Color.Gray * 0.8f);

            if (node.Cleared)
            {
                string cleared = SafeText("CLEARED");
                var clearedSize = _font.MeasureString(cleared);
                _spriteBatch.DrawString(_font, cleared, new Vector2(node.X - clearedSize.X / 2f, node.Y + nodeSize / 2f + 5), Color.LimeGreen * 0.6f);
            }
        }

        if (_overworldCursor >= 0 && _overworldCursor < _overworld.Nodes.Length)
        {
            var sel = _overworld.Nodes[_overworldCursor];
            if (sel.Discovered)
            {
                string info = SafeText($"[Space/Enter] Enter  |  {sel.ShownName}");
                _spriteBatch.DrawString(_font, info, new Vector2(200, 540), Color.White * 0.7f);
            }
        }

        _spriteBatch.DrawString(_font, SafeText("[Esc] Back to Title  [WASD] Navigate"), new Vector2(220, 570), Color.Gray * 0.4f);

        _spriteBatch.End();
    }

    private void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathF.Atan2(dy, dx);
        _spriteBatch.Draw(_pixel, new Rectangle(x1, y1, (int)length, 2), null, color, angle, Vector2.Zero, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
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

        if (_gameState == GameState.Overworld)
        {
            DrawOverworld();
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

        // Draw NPCs
        for (int i = 0; i < _level.Npcs.Length; i++)
        {
            var npc = _level.Npcs[i];
            // Hide EVE from NPC spot if orb is active
            if (npc.Id == "eve" && _eveOrbActive) continue;

            if (npc.Id == "eve")
            {
                // EVE floating orb: layered squares with bob
                float bobY = MathF.Sin(_totalTime * 2.5f) * 6f;
                float cx = npc.X + npc.W / 2f;
                float cy = npc.Y + npc.H / 2f + bobY;
                // Outer glow
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 11), (int)(cy - 11), 22, 22), Color.Cyan * 0.15f);
                // Mid glow
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 8), (int)(cy - 8), 16, 16), Color.Cyan * 0.35f);
                // Core
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 6), (int)(cy - 6), 12, 12), Color.Cyan);
                // Name above
                var nameSize = _font.MeasureString("EVE");
                _spriteBatch.DrawString(_font, "EVE",
                    new Vector2(cx - nameSize.X / 2f, cy - 24), Color.Cyan * 0.9f);
            }
            else
            {
                Color npcColor = ParseNpcColor(npc.Color);
                _spriteBatch.Draw(_pixel, _level.NpcRects[i], npcColor);
                var nameSize = _font.MeasureString(SafeText(npc.Name));
                _spriteBatch.DrawString(_font, SafeText(npc.Name),
                    new Vector2(npc.X + npc.W / 2f - nameSize.X / 2f, npc.Y - 16), Color.White * 0.8f);
            }
        }

        // Draw player
        if (!_isDead)
        {
            bool visible = _spawnInvincibility <= 0f || MathF.Sin(_spawnInvincibility * 20f) > 0;
            if (visible)
                _player.Draw(_spriteBatch, _pixel);
        }

        // Draw EVE orbiting companion
        if (_eveOrbActive && !_isDead)
        {
            float angle = _totalTime * 2f;
            float orbRadius = 30f;
            var playerCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            float orbX = playerCenter.X + MathF.Cos(angle) * orbRadius;
            float orbY = playerCenter.Y + MathF.Sin(angle) * orbRadius;
            // Outer glow
            _spriteBatch.Draw(_pixel, new Rectangle((int)(orbX - 6), (int)(orbY - 6), 12, 12), Color.CornflowerBlue * 0.4f);
            // Core
            _spriteBatch.Draw(_pixel, new Rectangle((int)(orbX - 4), (int)(orbY - 4), 8, 8), Color.Cyan);
        }

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

        // Dialogue box
        if (_dialogueOpen && _dialogueNpcIndex >= 0 && _dialogueNpcIndex < _level.Npcs.Length)
        {
            var npc = _level.Npcs[_dialogueNpcIndex];
            _spriteBatch.Draw(_pixel, new Rectangle(50, 450, 700, 120), Color.Black * 0.85f);
            _spriteBatch.Draw(_pixel, new Rectangle(50, 450, 700, 2), Color.Gray * 0.5f);

            // Speaker attribution
            string speaker = "";
            Color speakerColor = Color.Yellow;
            if (_dialogueLine < npc.DialogueSpeakers.Length && _dialogueLine < npc.Dialogue.Length)
                speaker = npc.DialogueSpeakers[_dialogueLine];
            else if (_dialogueLine < npc.Dialogue.Length)
                speaker = npc.Name;

            if (!string.IsNullOrEmpty(speaker))
            {
                speakerColor = speaker switch
                {
                    "Admin" => Color.LightGreen,
                    "EVE" => Color.Cyan,
                    _ => Color.Yellow,
                };
                _spriteBatch.DrawString(_font, SafeText(speaker), new Vector2(70, 460), speakerColor);
            }

            if (_dialogueLine < npc.Dialogue.Length)
            {
                _spriteBatch.DrawString(_font, SafeText(npc.Dialogue[_dialogueLine]), new Vector2(70, 490), Color.White);
                if (_dialogueLine < npc.Dialogue.Length - 1)
                    _spriteBatch.DrawString(_font, "[W/Space]", new Vector2(670, 548), Color.Gray * 0.6f);
                else
                    _spriteBatch.DrawString(_font, "[End]", new Vector2(695, 548), Color.Gray * 0.6f);
            }
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
