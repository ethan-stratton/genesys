using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.IO;
using FontStashSharp;

namespace ArenaShooter;

public enum WeaponType { None, Stick, Sword, Axe, Sling, Bow, Gun }

public class ItemPickup
{
    public string Id;
    public float X, Y;
    public int W = 20, H = 20;
    public string ItemType;
    public bool Collected;
    public float VelY; // for gravity on dropped items
    public bool HasGravity;
    public Rectangle Rect => new((int)X, (int)Y, W, H);
}

public enum GameState { Title, Playing, Editing, Overworld, SimMode }

public class Game1 : Game
{
    private GameState _gameState = GameState.Title;
    private int _titleCursor;
    private bool _settingsFromTitle;
    private string[] _titleOptions => SaveData.Exists()
        ? new[] { "Continue", "New Game", "Settings", "Quit" }
        : new[] { "New Game", "Settings", "Quit" };

    private OverworldData _overworld;

    // Sim mode
    private SimRegion _simRegion;
    private int _simCursorX, _simCursorY;
    private string _simNodeId;

    // World map
    private WorldMapData _worldMap;
    private bool _worldMapGridVisible = false;
    private bool _worldMapLegendVisible = false;
    private float _worldMapZoom = 1f;
    private string _worldMapBiomeMenuId; // non-null = showing biome level list
    private int _worldMapBiomeMenuCursor;
    private int _overworldCursor;
    private string _currentNodeId;
    private const string OverworldPath = "Content/overworld.json";

    private GraphicsDeviceManager _graphics;
    private SaveData _saveData;
    private SpriteBatch _spriteBatch;
    private Camera _camera;
    private Texture2D _pixel;
    private FontSystem _fontSystem;
    private SpriteFontBase _font;
    private SpriteFontBase _fontSmall;
    private SpriteFontBase _fontLarge;
    private Song _bgm;
    private Player _player;

    // Hit feedback
    private float _hitStopTimer;
    private float _shakeTimer;
    private float _shakeIntensity;
    private Random _shakeRng = new();

    private List<Bullet> _bullets;
    private List<InsectSwarm> _swarms = new();
    private List<Crawler> _crawlers = new();
    private List<Hopper> _hoppers = new();
    private List<Thornback> _thornbacks = new();
    private List<Bird> _birds = new();

    private Random _rng;

    private bool _isDead;
    private float _spawnInvincibility;
    private bool[] _prevInExit = Array.Empty<bool>();
    private KeyboardState _prevKb;

    // Room transition effect
    private float _transitionTimer;
    private float _transitionDuration = 0.5f; // total fade time (fade out + fade in)
    private bool _transitionActive;
    private float _transitionAlpha; // 0 = clear, 1 = black

    // Level data (loaded from JSON)
    private LevelData _level;
    private const string DefaultLevel = "Content/levels/test-arena.json";

    // --- Settings menu ---
    private bool _menuOpen;

    private struct SettingEntry
    {
        public string Label;
        public Func<bool> Get;
        public Action Toggle;
        #pragma warning disable CS0649
        public bool IsAction; // true = triggers action on Enter, not a toggle
        #pragma warning restore CS0649
    }

    private SettingEntry[] _audioSettings;
    private SettingEntry[] _debugSettings;
    private SettingEntry[] _graphicsSettings;
    private static readonly (int w, int h, string label)[] WindowSizes = {
        (800, 600, "800x600"),
        (1024, 768, "1024x768"),
        (1280, 720, "1280x720"),
        (1280, 960, "1280x960"),
        (1440, 900, "1440x900"),
        (1600, 900, "1600x900"),
        (1920, 1080, "1920x1080"),
    };
    private int _windowSizeIndex = 0;

    private enum SettingsCategory { Audio, Graphics, Debug }
    private int _settingsCategoryCursor;
    private SettingsCategory? _settingsActiveCategory; // null = top level
    private int _settingsItemCursor;

    // Toggleable gameplay options
    private bool _enemiesEnabled = true;
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

    // --- Inventory state ---
    private bool _inventoryOpen;
    private int _inventorySection; // 0=ranged, 1=melee
    private int _inventoryIndex; // index within current section

    // Spawn weapon menu (P key)
    private bool _spawnMenuOpen;
    private int _spawnMenuCursor;
    private static readonly string[] SpawnMenuItems = { "Stick", "Sword", "Axe", "Gun", "Bow", "Sling" };

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

    // --- Weapon system ---


    private WeaponType[] _meleeInventory = Array.Empty<WeaponType>();
    private int _meleeIndex = -1;

    private WeaponType[] _rangedInventory = Array.Empty<WeaponType>();
    private int _rangedIndex = -1;

    private bool _debugSword;
    private bool _debugGun;

    private List<ItemPickup> _itemPickups = new();
    private HashSet<(int col, int row)> _destroyedBreakables = new();

    // --- Editor state ---
    private enum EditorTool { SolidFloor = 0, Platform = 1, Rope = 2, Wall = 3, Spike = 4, Exit = 5, Spawn = 6, WallSpike = 7, OverworldExit = 8, Ceiling = 9, TilePaint = 10 }
    // Wall climbSide values: 0=both, 1=right face, -1=left face, 99=no climb (solid only)
    private EditorTool _editorTool = EditorTool.Platform;
    private bool _toolPaletteOpen;
    private Vector2 _editorCursor; // world position
    private bool _editorGridSnap = true;
    private int _editorGridSize = 32;
    private bool _editorDragging;
    private Vector2 _editorDragStart;
    private bool _editorMenuOpen;
    private int _editorMenuCursor;
    private MouseState _prevMouse;
    private string _editorSaveFile = "Content/levels/test-arena.json";
    private string _editorStatusMsg = "";
    private float _editorStatusTimer;
    // Entity drag-move state
    private object _editorMovingEntity; // reference to the entity being moved (EnemySpawnData, EnvObjectData, NpcData, ItemData)
    private Vector2 _editorMoveOffset; // offset from entity origin to grab point
    private bool _entityPaletteOpen;
    private int _entityPaletteCursor;
    private enum EntityType { Swarm, Crawler, Thornback, Hopper, Tree, Bird }

    // Tile paint state
    private int _tilePaletteCursor;
    private TileType _selectedTileType = TileType.Dirt;

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

        _audioSettings = new SettingEntry[]
        {
            new() { Label = "Music", Get = () => _enableMusic, Toggle = () => { _enableMusic = !_enableMusic; if (_enableMusic) { MediaPlayer.IsRepeating = true; MediaPlayer.Play(_bgm); } else { MediaPlayer.Stop(); } } },
        };

        _debugSettings = new SettingEntry[]
        {
            new() { Label = "Enemies", Get = () => _enemiesEnabled, Toggle = () => _enemiesEnabled = !_enemiesEnabled },
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
            new() { Label = "Debug Sword", Get = () => _debugSword, Toggle = () => {
                _debugSword = !_debugSword;
                if (_debugSword) EquipMelee(WeaponType.Sword);
                else UnequipMelee(WeaponType.Sword);
            }},
            new() { Label = "Debug Gun", Get = () => _debugGun, Toggle = () => {
                _debugGun = !_debugGun;
                if (_debugGun) EquipRanged(WeaponType.Gun);
                else UnequipRanged(WeaponType.Gun);
            }},
            new() { Label = "EVE Orb", Get = () => _eveOrbActive, Toggle = () => _eveOrbActive = !_eveOrbActive },
        };

        _graphicsSettings = new SettingEntry[]
        {
            new() { Label = "Window Size", Get = () => true, Toggle = () => ApplyWindowSize(_windowSizeIndex + 1) },
        };

        Restart();

        // Load saved window size
        var tempSave = SaveData.Load();
        if (tempSave != null && tempSave.WindowSizeIndex > 0 && tempSave.WindowSizeIndex < WindowSizes.Length)
        {
            _windowSizeIndex = tempSave.WindowSizeIndex;
            var (w, h, _) = WindowSizes[_windowSizeIndex];
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            _graphics.ApplyChanges();
            if (_level != null)
                _camera = MakeCamera();
        }

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

        // Clear enemies (SpawnEnemiesFromLevel re-populates if needed)
        _swarms.Clear();
        _crawlers.Clear();
        _hoppers.Clear();
        _thornbacks.Clear();
        _birds.Clear();

        // Load item pickups
        _itemPickups.Clear();
        _destroyedBreakables.Clear();
        foreach (var item in _level.Items)
        {
            bool alreadyCollected = _saveData?.CollectedItems?.Contains(item.Id) == true;
            _itemPickups.Add(new ItemPickup { Id = item.Id, X = item.X, Y = item.Y, W = item.W, H = item.H, ItemType = item.Type, Collected = alreadyCollected });
        }

        // Enemies are spawned in SpawnEnemiesFromLevel(), called by Restart()
    }

    private int ViewW => _graphics.PreferredBackBufferWidth;
    private int ViewH => _graphics.PreferredBackBufferHeight;

    private void ApplyWindowSize(int index)
    {
        _windowSizeIndex = index % WindowSizes.Length;
        var (w, h, _) = WindowSizes[_windowSizeIndex];
        _graphics.PreferredBackBufferWidth = w;
        _graphics.PreferredBackBufferHeight = h;
        _graphics.ApplyChanges();
        if (_level != null)
        {
            var oldPos = _camera?.Position ?? Vector2.Zero;
            _camera = MakeCamera();
            _camera.SnapTo(_player.Position, Player.Width, _player.CurrentHeight);
        }
        // Persist
        if (_saveData != null) { _saveData.WindowSizeIndex = _windowSizeIndex; _saveData.Save(); }
    }

    private Camera MakeCamera() => new Camera(ViewW, ViewH, _level.Bounds.Left, _level.Bounds.Right, _level.Bounds.Top, _level.Bounds.Bottom);

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
        _rng = new Random();

        _isDead = false;
        _spawnInvincibility = 1.0f;
        _prevInExit = Array.Empty<bool>();
        if (_player != null) _player.Hp = _player.MaxHp;
        SpawnEnemiesFromLevel();
    }

    private void SpawnEnemiesFromLevel()
    {
        _swarms.Clear();
        _crawlers.Clear();
        _hoppers.Clear();
        _thornbacks.Clear();
        _birds.Clear();
        if (_rng == null) _rng = new Random();
        foreach (var e in _level.Enemies)
        {
            switch (e.Type)
            {
                case "swarm":
                    _swarms.Add(new InsectSwarm(new Vector2(e.X, e.Y), e.Count > 0 ? e.Count : 10, _rng));
                    break;
                case "crawler":
                    float snapY = SnapToSurface(e.X, e.Y, Crawler.Width, Crawler.Height);
                    var surfaceEdges = FindSurfaceEdges(e.X, snapY + Crawler.Height);
                    _crawlers.Add(new Crawler(new Vector2(e.X, snapY), e.X - 100, e.X + 100, surfaceEdges.Item1, surfaceEdges.Item2));
                    break;
                case "thornback":
                    float tSnapY = SnapToSurface(e.X, e.Y, Thornback.Width, Thornback.Height);
                    _thornbacks.Add(new Thornback(new Vector2(e.X, tSnapY)));
                    break;
                case "hopper":
                    float hSnapY = SnapToSurface(e.X, e.Y, Hopper.Width, Hopper.Height);
                    _hoppers.Add(new Hopper(new Vector2(e.X, hSnapY), hSnapY + Hopper.Height));
                    break;
                case "bird":
                    float bSnapY = SnapToSurface(e.X, e.Y, Bird.Width, Bird.Height);
                    var bEdges = FindSurfaceEdges(e.X, bSnapY + Bird.Height);
                    _birds.Add(new Bird(new Vector2(e.X, bSnapY), bEdges.Item1, bEdges.Item2, _rng));
                    break;
            }
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _camera = MakeCamera();
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(File.ReadAllBytes("Content/Fonts/main.ttf"));
        _font = _fontSystem.GetFont(12);       // main text (was 16, too large for Press Start 2P)
        _fontSmall = _fontSystem.GetFont(9);   // small UI hints
        _fontLarge = _fontSystem.GetFont(22);  // titles, boss names
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

        if (_gameState == GameState.SimMode)
        {
            UpdateSimMode(kb);
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }
        if (_gameState == GameState.Title)
        {
            // Handle settings menu first (consumes input)
            if (_menuOpen && _settingsFromTitle)
            {
                UpdateMenu(kb);
                _prevKb = kb;
                base.Update(gameTime);
                return;
            }

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
                            {
                                LoadLevel(path);
                                _editorSaveFile = path;
                            }
                            else
                            {
                                LoadLevel(DefaultLevel);
                                _editorSaveFile = DefaultLevel;
                            }
                            _camera = MakeCamera();
                            _gameState = GameState.Playing;
                            _player = new Player(new Microsoft.Xna.Framework.Vector2(_saveData.SpawnX, _saveData.SpawnY));
                            _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                            _bullets = new List<Bullet>();
                            _prevInExit = new bool[_level.ExitRects.Length];
                            for (int k = 0; k < _prevInExit.Length; k++)
                                _prevInExit[k] = true;
                            // Restore EVE orb state
                            if (_saveData.Flags.ContainsKey("eveOrbActive"))
                                _eveOrbActive = _saveData.Flags["eveOrbActive"];
                            // Restore inventory
                            _meleeInventory = _saveData.MeleeInventory.ConvertAll(s => Enum.Parse<WeaponType>(s)).ToArray();
                            _meleeIndex = _saveData.MeleeIndex;
                            _rangedInventory = _saveData.RangedInventory.ConvertAll(s => Enum.Parse<WeaponType>(s)).ToArray();
                            _rangedIndex = _saveData.RangedIndex;
                            if (_meleeIndex >= _meleeInventory.Length) _meleeIndex = _meleeInventory.Length > 0 ? 0 : -1;
                            if (_rangedIndex >= _rangedInventory.Length) _rangedIndex = _rangedInventory.Length > 0 ? 0 : -1;
                        }
                        break;
                    case "New Game":
                        SaveData.Delete();
                        _saveData = new SaveData();
                        _eveOrbActive = false;
                        _eveDialogueExhausted = false;
                        _meleeInventory = Array.Empty<WeaponType>();
                        _meleeIndex = -1;
                        _rangedInventory = Array.Empty<WeaponType>();
                        _rangedIndex = -1;
                        _debugSword = false;
                        _debugGun = false;
                        LoadLevel(DefaultLevel);
                        _camera = MakeCamera();
                        _gameState = GameState.Playing;
                        Restart();
                        _prevInExit = new bool[_level.ExitRects.Length];
                        for (int k = 0; k < _prevInExit.Length; k++)
                            _prevInExit[k] = true;
                        _saveData.CurrentLevel = System.IO.Path.GetFileNameWithoutExtension(DefaultLevel);
                        _saveData.SpawnX = _player.Position.X;
                        _saveData.SpawnY = _player.Position.Y;
                        SyncInventoryToSave(); _saveData.Save();
                        // Reset overworld to fresh state
                        if (System.IO.File.Exists(OverworldPath))
                            _overworld = OverworldData.Load(OverworldPath);
                        else
                            _overworld = new OverworldData();
                        // Reset all nodes: only start node discovered, nothing cleared
                        foreach (var n in _overworld.Nodes)
                        {
                            n.Discovered = n.Id == _overworld.StartNode;
                            n.Cleared = false;
                        }
                        _overworld.Save(OverworldPath);
                        _currentNodeId = _overworld.StartNode;
                        // Reset world map
                        if (System.IO.File.Exists("Content/worldmap.json"))
                            System.IO.File.Delete("Content/worldmap.json");
                        _worldMap = null;
                        // Clean sim data
                        if (System.IO.Directory.Exists("Content/sim"))
                            foreach (var f in System.IO.Directory.GetFiles("Content/sim", "*.json"))
                                System.IO.File.Delete(f);
                        break;
                    case "Settings":
                        _menuOpen = true;
                        _settingsCategoryCursor = 0;
                        _settingsActiveCategory = null;
                        _settingsItemCursor = 0;
                        _settingsFromTitle = true;
                        _prevKb = kb; // consume the Enter press so UpdateMenu doesn't see it
                        break;
                    case "Quit":
                        Exit();
                        break;
                }
            }

            // Handle settings menu while on title screen

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

        // Toggle menu with Escape (only open — closing is handled inside UpdateMenu)
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape) && !_menuOpen)
        {
            _menuOpen = true;
            _settingsCategoryCursor = 0;
            _settingsActiveCategory = null;
            _settingsItemCursor = 0;
            _prevKb = kb; // consume the Escape press so UpdateMenu doesn't immediately close
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

        // Toggle inventory with Tab
        if (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab) && !_dialogueOpen)
        {
            _inventoryOpen = !_inventoryOpen;
            if (_inventoryOpen) { _inventorySection = 0; _inventoryIndex = 0; }
        }

        if (_inventoryOpen)
        {
            UpdateInventory(kb);
            _prevKb = kb;
            base.Update(gameTime);
            return; // game is paused while inventory is open
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

        // Room transition fade
        if (_transitionActive)
        {
            _transitionTimer -= dt;
            if (_transitionTimer > _transitionDuration * 0.5f)
                _transitionAlpha = 1f - (_transitionTimer - _transitionDuration * 0.5f) / (_transitionDuration * 0.5f);
            else if (_transitionTimer > 0)
                _transitionAlpha = _transitionTimer / (_transitionDuration * 0.5f);
            else
            {
                _transitionActive = false;
                _transitionAlpha = 0f;
            }
        }

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

        // Weapon system: set weapon availability and melee range
        _player.HasMeleeWeapon = true; // always have fists at minimum
        _player.HasRangedWeapon = CurrentRanged != WeaponType.None;
        _player.CurrentWeapon = CurrentMelee;
        _player.MeleeRangeOverride = CurrentMelee switch
        {
            WeaponType.Sword => 60,
            WeaponType.Stick => 30,
            WeaponType.None => 28, // fists: fast but short range
            _ => Player.MeleeRange
        };

        // Weapon cycling (only during gameplay, not editor)
        if (!_menuOpen && !_dialogueOpen && _gameState == GameState.Playing)
        {
            if (kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1) && _rangedInventory.Length > 0)
            {
                _rangedIndex = (_rangedIndex + 1) % _rangedInventory.Length;
            }
            if (kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2) && _meleeInventory.Length > 0)
            {
                _meleeIndex = (_meleeIndex + 1) % _meleeInventory.Length;
            }
        }

        var wallsToPass = _enableWallClimb ? _level.WallRects : null;
        var wallSidesToPass = _enableWallClimb ? _level.WallClimbSides : null;
        var ropesToPass = _enableRopeClimb ? _level.RopeXPositions : null;
        var ropeTopsToPass = _enableRopeClimb ? _level.RopeTops : null;
        var ropeBottomsToPass = _enableRopeClimb ? _level.RopeBottoms : null;

        // Hitstop: decrement timer and skip entity updates if active
        if (_hitStopTimer > 0) _hitStopTimer -= dt;
        if (_shakeTimer > 0) _shakeTimer -= dt;

        bool hitStopped = _hitStopTimer > 0;

        if (!hitStopped)
        {
        _player.Update(dt, kb, _level.Floor.Y, _level.AllPlatforms, ropesToPass, ropeTopsToPass, ropeBottomsToPass, wallsToPass, wallSidesToPass, _level.WallRects, _level.CeilingRects, _level.SolidFloorRects, _level.TileGridInstance);
        _player.UpdateRegen(dt);
        }

        // Track play time
        if (_saveData != null) _saveData.PlayTime += dt;
        // Update camera
        _camera.Update(dt, _player.Position, Player.Width, Player.Height, _player.FacingDir, _player.IsGrounded, _player.Velocity.Y);

        // --- Dialogue system ---
        if (_dialogueOpen)
        {
            // Close dialogue if player walks too far from NPC (120px range)
            if (_dialogueNpcIndex >= 0 && _dialogueNpcIndex < _level.NpcRects.Length)
            {
                var pCenter = _player.Position.X + Player.Width / 2f;
                var npcCenter = _level.NpcRects[_dialogueNpcIndex].X + _level.NpcRects[_dialogueNpcIndex].Width / 2f;
                if (MathF.Abs(pCenter - npcCenter) > 120f)
                {
                    _dialogueOpen = false;
                    // Keep _dialogueNpcIndex and _dialogueLine so it resumes where you left off
                }
            }
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
                    // Save EVE orb state
                    if (_saveData != null)
                    {
                        _saveData.Flags["eveOrbActive"] = _eveOrbActive;
                        SyncInventoryToSave(); _saveData.Save();
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
                    // Skip EVE's NPC spot once she's become the companion orb
                    if (_level.Npcs[i].Id == "eve" && _eveOrbActive) continue;

                    _dialogueOpen = true;
                    // Resume if same NPC, otherwise start fresh
                    if (_dialogueNpcIndex != i)
                    {
                        _dialogueNpcIndex = i;
                        _dialogueLine = 0;
                    }
                    // If we already exhausted this NPC's dialogue, restart from beginning
                    if (_dialogueLine >= _level.Npcs[i].Dialogue.Length)
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

        // Update all enemies (swarms, crawlers, thornbacks)
        if (_enemiesEnabled && !hitStopped)
        {
        var playerCenter2 = new Vector2(_player.Position.X + Player.Width / 2f, _player.Position.Y + Player.Height / 2f);
        var playerRect2 = _player.CollisionRect;
        foreach (var swarm in _swarms)
        {
            swarm.Update(dt, playerCenter2, _rng);

            if (_spawnInvincibility <= 0 && !_isDead)
            {
                int dmg = swarm.CheckPlayerDamage(playerRect2);
                if (dmg > 0) _player.TakeDamage(dmg, _player.Position.X - swarm.HomePosition.X);
            }

            if (_player.MeleeTimer > 0)
            {
                swarm.CheckMeleeHit(_player.MeleeHitbox);
            }
        }

        // Bullets vs swarms
        foreach (var b in _bullets)
        {
            var bRect = new Rectangle((int)b.Position.X, (int)b.Position.Y, Bullet.Size, Bullet.Size);
            foreach (var swarm in _swarms)
            {
                if (swarm.CheckBulletHit(bRect))
                {
                    b.IsDead = true;
                    break;
                }
            }
        }

        // Update crawlers
        foreach (var c in _crawlers)
        {
            c.Update(dt, playerCenter2);
            if (_spawnInvincibility <= 0 && !_isDead)
            {
                int dmg = c.CheckPlayerDamage(playerRect2);
                if (dmg > 0) _player.TakeDamage(dmg, _player.Position.X - c.Position.X);
            }
            if (_player.MeleeTimer > 0 && c.Alive)
            {
                if (_player.MeleeHitbox.Intersects(c.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    int prevHp = c.Hp;
                    int dmg = finisher ? 2 : 1;
                    bool killed = c.TakeHit(dmg);
                    bool didHit = c.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) c.MeleeHitCooldown = 0.055f; // short cooldown so next phase can hit
                        if (killed) { _hitStopTimer = 0.06f; _shakeTimer = 0.15f; _shakeIntensity = 8f; }
                        else if (finisher) { _hitStopTimer = 0.05f; _shakeTimer = 0.12f; _shakeIntensity = 6f; }
                        else { _hitStopTimer = 0.03f; _shakeTimer = 0.1f; _shakeIntensity = 5f; }
                    }
                }
            }
        }

        // Update thornbacks
        foreach (var t in _thornbacks)
        {
            t.Update(dt);
            if (_spawnInvincibility <= 0 && !_isDead)
            {
                int dmg = t.CheckPlayerDamage(playerRect2);
                if (dmg > 0) _player.TakeDamage(dmg, _player.Position.X - t.Position.X);
            }
            if (_player.MeleeTimer > 0 && t.Alive)
            {
                if (_player.MeleeHitbox.Intersects(t.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    int prevHp = t.Hp;
                    int dmg = finisher ? 2 : 1;
                    bool killed = t.TakeHit(dmg);
                    bool didHit = t.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) t.MeleeHitCooldown = 0.055f;
                        if (killed) { _hitStopTimer = 0.06f; _shakeTimer = 0.15f; _shakeIntensity = 8f; }
                        else if (finisher) { _hitStopTimer = 0.05f; _shakeTimer = 0.12f; _shakeIntensity = 6f; }
                        else { _hitStopTimer = 0.03f; _shakeTimer = 0.1f; _shakeIntensity = 5f; }
                    }
                }
            }
        }

        // Update hoppers
        foreach (var h in _hoppers)
        {
            h.Update(dt, playerCenter2, _level.SolidFloorRects, _level.AllPlatforms, _level.Floor.Y);
            if (_spawnInvincibility <= 0 && !_isDead)
            {
                int dmg = h.CheckPlayerDamage(playerRect2);
                if (dmg > 0) _player.TakeDamage(dmg, _player.Position.X - h.Position.X);
            }
            if (_player.MeleeTimer > 0 && h.Alive)
            {
                if (_player.MeleeHitbox.Intersects(h.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    int prevHp = h.Hp;
                    int dmg = finisher ? 2 : 1;
                    bool killed = h.TakeHit(dmg);
                    bool didHit = h.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) h.MeleeHitCooldown = 0.055f;
                        if (killed) { _hitStopTimer = 0.06f; _shakeTimer = 0.15f; _shakeIntensity = 8f; }
                        else if (finisher) { _hitStopTimer = 0.05f; _shakeTimer = 0.12f; _shakeIntensity = 6f; }
                        else { _hitStopTimer = 0.03f; _shakeTimer = 0.1f; _shakeIntensity = 5f; }
                    }
                }
            }
        }

        // Update birds (non-hostile ambient creatures)
        foreach (var bird in _birds)
            bird.Update(dt, playerCenter2);
        _birds.RemoveAll(b => !b.Alive);

        // Bullets vs crawlers and thornbacks
        foreach (var b in _bullets)
        {
            if (b.IsDead) continue;
            var bRect = new Rectangle((int)b.Position.X, (int)b.Position.Y, Bullet.Size, Bullet.Size);
            foreach (var c in _crawlers)
            {
                if (c.Alive && bRect.Intersects(c.Rect))
                { c.TakeHit(2); b.IsDead = true; break; }
            }
            if (b.IsDead) continue;
            foreach (var t in _thornbacks)
            {
                if (t.Alive && bRect.Intersects(t.Rect))
                { t.TakeHit(1); b.IsDead = true; break; }
            }
            if (b.IsDead) continue;
            foreach (var h in _hoppers)
            {
                if (h.Alive && bRect.Intersects(h.Rect))
                { h.TakeHit(2); b.IsDead = true; break; }
            }
            // Bullet vs breakable tiles
            if (!b.IsDead && _level.TileGridInstance != null)
            {
                var tgi = _level.TileGridInstance;
                int ts = tgi.TileSize;
                int ox = tgi.OriginX, oy = tgi.OriginY;
                int bc = ((int)b.Position.X + Bullet.Size / 2 - ox) / ts;
                int br2 = ((int)b.Position.Y + Bullet.Size / 2 - oy) / ts;
                if (bc >= 0 && bc < tgi.Width && br2 >= 0 && br2 < tgi.Height)
                {
                    if (tgi.GetTileAt(bc, br2) == TileType.Breakable)
                    {
                        _destroyedBreakables.Add((bc, br2));
                        tgi.SetTileAt(bc, br2, TileType.Empty);
                        _level.RebuildTileCollision();
                        int twx = ox + bc * ts, twy = oy + br2 * ts;
                        _itemPickups.Add(new ItemPickup
                        {
                            Id = $"heart-break-{twx}-{twy}",
                            X = twx + ts / 2f - 8,
                            Y = twy,
                            W = 16, H = 16,
                            ItemType = "heart",
                            Collected = false,
                            HasGravity = true,
                            VelY = -150f
                        });
                        b.IsDead = true;
                    }
                }
            }
        }
        } // end _enemiesEnabled

        // Check HP death
        if (_player.Hp <= 0 && !_isDead)
        {
            _isDead = true;
        }

        // Update item physics (gravity for dropped items)
        foreach (var item in _itemPickups)
        {
            if (item.HasGravity && !item.Collected)
            {
                item.VelY += 600f * dt; // gravity
                item.Y += item.VelY * dt;
                // Floor collision: level floor
                float floorY = _level.Floor.Y - item.H;
                if (item.Y >= floorY) { item.Y = floorY; item.VelY = 0; item.HasGravity = false; }
                // Solid rects
                foreach (var s in _level.SolidFloorRects)
                {
                    if (item.Rect.Intersects(s) && item.Y + item.H > s.Top && item.Y + item.H < s.Top + 16)
                    {
                        item.Y = s.Top - item.H;
                        item.VelY = 0;
                        item.HasGravity = false;
                        break;
                    }
                }
                // Platforms (land on top)
                if (item.HasGravity && item.VelY > 0)
                {
                    foreach (var p in _level.AllPlatforms)
                    {
                        if (item.X + item.W > p.X && item.X < p.Right &&
                            item.Y + item.H >= p.Y && item.Y + item.H <= p.Y + item.VelY * dt + 8)
                        {
                            item.Y = p.Y - item.H;
                            item.VelY = 0;
                            item.HasGravity = false;
                            break;
                        }
                    }
                }
                // Tile grid slopes and solids
                var tgi = _level.TileGridInstance;
                if (tgi != null && item.HasGravity)
                {
                    float slopeY = tgi.GetSlopeFloorY(item.X, item.Y + item.H, item.W, item.H);
                    if (slopeY < item.Y + item.H && item.Y + item.H - slopeY < 20f)
                    {
                        item.Y = slopeY - item.H;
                        item.VelY = 0;
                        item.HasGravity = false;
                    }
                }
            }
        }

        // Auto-collect hearts on touch
        if (!_isDead)
        {
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            for (int i = 0; i < _itemPickups.Count; i++)
            {
                var item = _itemPickups[i];
                if (!item.Collected && item.ItemType == "heart" && pRect.Intersects(item.Rect))
                {
                    item.Collected = true;
                    _player.Hp = Math.Min(_player.Hp + 25, _player.MaxHp);
                }
            }
        }

        // Item pickups (W key, near item, grounded)
        if (_player.IsGrounded && kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W) && !_dialogueOpen)
        {
            var playerRect = new Rectangle((int)_player.Position.X - 20, (int)_player.Position.Y, Player.Width + 40, Player.Height);
            for (int i = 0; i < _itemPickups.Count; i++)
            {
                var item = _itemPickups[i];
                if (!item.Collected && playerRect.Intersects(item.Rect))
                {
                    item.Collected = true;
                    if (_saveData != null && !string.IsNullOrEmpty(item.Id))
                        _saveData.CollectedItems.Add(item.Id);
                    switch (item.ItemType)
                    {
                        case "stick": EquipMelee(WeaponType.Stick); break;
                        case "sword": EquipMelee(WeaponType.Sword); break;
                        case "axe": EquipMelee(WeaponType.Axe); break;
                        case "sling": EquipRanged(WeaponType.Sling); break;
                        case "bow": EquipRanged(WeaponType.Bow); break;
                        case "gun": EquipRanged(WeaponType.Gun); break;
                        case "heart":
                            _player.Hp = Math.Min(_player.Hp + 25, _player.MaxHp);
                            break;
                    }
                    break; // pick up one at a time
                }
            }
        }

        // Spike collision
        if (!_isDead)
        {
            var pRect = _player.CollisionRect;
            foreach (var spike in _level.AllSpikeRects)            {
                if (pRect.Intersects(spike))
                {
                    if (_spawnInvincibility <= 0f)
                    {
                        _player.TakeDamage(33, _player.Position.X - spike.Center.X);
                        if (_player.Hp <= 0) _isDead = true;
                    }
                    break;
                }
            }
        }

        // Tile-based spike collision
        if (!_isDead && _spawnInvincibility <= 0f && _level.TileGridInstance != null)
        {
            var pRect = _player.CollisionRect;
            var tgi = _level.TileGridInstance;
            int ts = tgi.TileSize;
            int ox = tgi.OriginX, oy = tgi.OriginY;
            int startCol = Math.Max(0, (pRect.Left - ox) / ts);
            int endCol = Math.Min(tgi.Width - 1, (pRect.Right - ox) / ts);
            int startRow = Math.Max(0, (pRect.Top - oy) / ts);
            int endRow = Math.Min(tgi.Height - 1, (pRect.Bottom - oy) / ts);
            bool hit = false;
            for (int row = startRow; row <= endRow && !hit; row++)
            {
                for (int col = startCol; col <= endCol && !hit; col++)
                {
                    var tile = tgi.GetTileAt(col, row);
                    if (!TileProperties.IsHazard(tile)) continue;
                    
                    // Build hitbox based on tile type
                    int twx = ox + col * ts, twy = oy + row * ts;
                    Rectangle spikeRect;
                    switch (tile)
                    {
                        case TileType.Spikes:       spikeRect = new Rectangle(twx, twy, ts, ts); break;
                        case TileType.SpikesDown:   spikeRect = new Rectangle(twx, twy, ts, ts); break;
                        case TileType.SpikesLeft:   spikeRect = new Rectangle(twx, twy, ts, ts); break;
                        case TileType.SpikesRight:  spikeRect = new Rectangle(twx, twy, ts, ts); break;
                        case TileType.HalfSpikesUp:    spikeRect = new Rectangle(twx, twy + ts / 2, ts, ts / 2); break;
                        case TileType.HalfSpikesDown:  spikeRect = new Rectangle(twx, twy, ts, ts / 2); break;
                        case TileType.HalfSpikesLeft:  spikeRect = new Rectangle(twx + ts / 2, twy, ts / 2, ts); break;
                        case TileType.HalfSpikesRight: spikeRect = new Rectangle(twx, twy, ts / 2, ts); break;
                        default: continue;
                    }
                    
                    if (pRect.Intersects(spikeRect))
                    {
                        _player.TakeDamage(33, _player.Position.X - spikeRect.Center.X);
                        if (_player.Hp <= 0) _isDead = true;
                        hit = true;
                    }
                }
            }
        }

        // Effect tile collision (damage, knockback, speed boost, float)
        if (!_isDead)
        {
            var pRect = _player.CollisionRect;
            var tgi = _level.TileGridInstance;
            if (tgi != null)
            {
                int ts = tgi.TileSize;
                int ox = tgi.OriginX, oy = tgi.OriginY;
                int startCol = Math.Max(0, (pRect.Left - ox) / ts);
                int endCol = Math.Min(tgi.Width - 1, (pRect.Right - ox) / ts);
                int startRow = Math.Max(0, (pRect.Top - oy) / ts);
                int endRow = Math.Min(tgi.Height - 1, (pRect.Bottom - oy) / ts);
                for (int row = startRow; row <= endRow; row++)
                {
                    for (int col = startCol; col <= endCol; col++)
                    {
                        var tile = tgi.GetTileAt(col, row);
                        int twx = ox + col * ts, twy = oy + row * ts;
                        var tileRect = new Rectangle(twx, twy, ts, ts);
                        if (!pRect.Intersects(tileRect)) continue;
                        
                        switch (tile)
                        {
                            case TileType.DamageTile:
                                if (_spawnInvincibility <= 0f)
                                {
                                    _player.TakeDamage(5, _player.Position.X - tileRect.Center.X);
                                    if (_player.Hp <= 0) _isDead = true;
                                }
                                break;
                            case TileType.DamageNoKBTile:
                            case TileType.DamageFloorTile:
                                if (_spawnInvincibility <= 0f)
                                {
                                    // Damage without knockback — just reduce HP directly
                                    if (_player.DamageCooldown <= 0)
                                    {
                                        _player.Hp -= 5;
                                        _player.DamageCooldown = 1.0f;
                                        if (_player.Hp <= 0) { _player.Hp = 0; _isDead = true; }
                                    }
                                }
                                break;
                            case TileType.KnockbackTile:
                                {
                                    // Rubber bounce: reflect incoming velocity with boost
                                    var vel = _player.Velocity;
                                    float px = _player.Position.X + Player.Width / 2f;
                                    float py = _player.Position.Y + Player.Height / 2f;
                                    float dx = px - tileRect.Center.X;
                                    float dy = py - tileRect.Center.Y;
                                    float overlapX = (Player.Width / 2f + ts / 2f) - MathF.Abs(dx);
                                    float overlapY = (Player.Height / 2f + ts / 2f) - MathF.Abs(dy);
                                    
                                    float minBounce = 300f; // minimum bounce speed
                                    float bounceMult = 1.5f; // velocity multiplier
                                    
                                    if (overlapX < overlapY)
                                    {
                                        // Side hit — reflect horizontal, keep vertical
                                        float bounceX = MathF.Max(MathF.Abs(vel.X) * bounceMult, minBounce);
                                        vel.X = MathF.Sign(dx) * bounceX;
                                        // Push player out of tile
                                        var pos = _player.Position;
                                        pos.X = dx > 0 ? tileRect.Right : tileRect.Left - Player.Width;
                                        _player.Position = pos;
                                    }
                                    else
                                    {
                                        // Top/bottom hit — reflect vertical, keep horizontal
                                        float bounceY = MathF.Max(MathF.Abs(vel.Y) * bounceMult, minBounce);
                                        vel.Y = MathF.Sign(dy) * bounceY;
                                        var pos = _player.Position;
                                        pos.Y = dy > 0 ? tileRect.Bottom : tileRect.Top - Player.Height;
                                        _player.Position = pos;
                                    }
                                    _player.Velocity = vel;
                                    // Use knockback timer to prevent movement override
                                    _player.TriggerKnockbackTimer(0.15f);
                                }
                                break;
                            case TileType.SpeedBoostTile:
                                _player.SpeedBoostTimer = 3.0f;
                                break;
                            case TileType.FloatTile:
                                _player.FloatTimer = 2.0f;
                                break;
                        }
                    }
                }
            }
        }

        // Breakable tile destruction (melee hits)
        if (_player.MeleeTimer > 0)
        {
            var meleeRect = _player.MeleeHitbox;
            var tgi = _level.TileGridInstance;
            if (tgi != null)
            {
                int ts = tgi.TileSize;
                int ox = tgi.OriginX, oy = tgi.OriginY;
                int startCol = Math.Max(0, (meleeRect.Left - ox) / ts);
                int endCol = Math.Min(tgi.Width - 1, (meleeRect.Right - ox) / ts);
                int startRow = Math.Max(0, (meleeRect.Top - oy) / ts);
                int endRow = Math.Min(tgi.Height - 1, (meleeRect.Bottom - oy) / ts);
                for (int row = startRow; row <= endRow; row++)
                {
                    for (int col = startCol; col <= endCol; col++)
                    {
                        if (tgi.GetTileAt(col, row) == TileType.Breakable)
                        {
                            _destroyedBreakables.Add((col, row));
                            tgi.SetTileAt(col, row, TileType.Empty);
                            _level.RebuildTileCollision();
                            int twx = ox + col * ts, twy = oy + row * ts;
                            _itemPickups.Add(new ItemPickup
                            {
                                Id = $"heart-break-{twx}-{twy}",
                                X = twx + ts / 2f - 8,
                                Y = twy,
                                W = 16, H = 16,
                                ItemType = "heart",
                                Collected = false,
                                HasGravity = true,
                                VelY = -150f // pop upward then fall
                            });
                        }
                    }
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
                        // Also mark cleared in world map biome data
                        if (_worldMap == null) _worldMap = WorldMapData.LoadOrCreate();
                        _worldMap.MarkLevelCleared(System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile));
                        _worldMap.Save();
                        _gameState = GameState.Overworld;
                        break;
                    }
                    string nextPath = $"Content/levels/{target}.json";
                    string _sourceLevel = System.IO.Path.GetFileNameWithoutExtension(_editorSaveFile);
                    if (System.IO.File.Exists(nextPath))
                    {
                        LoadLevel(nextPath);
                        _editorSaveFile = nextPath;
                        _camera = MakeCamera();
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
                        _transitionActive = true;
                        _transitionTimer = _transitionDuration;

                        if (_saveData != null)
                        {
                            _saveData.CurrentLevel = target;
                            _saveData.SpawnX = _player.Position.X;
                            _saveData.SpawnY = _player.Position.Y;
                            SyncInventoryToSave(); _saveData.Save();
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
                        // Also mark in world map
                        if (_worldMap == null) _worldMap = WorldMapData.LoadOrCreate();
                        if (_sourceLevel != null) _worldMap.MarkLevelCleared(_sourceLevel);
                        _worldMap.Save();
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

    // --- Weapon helpers ---
    private void EquipMelee(WeaponType w)
    {
        var list = new List<WeaponType>(_meleeInventory);
        if (!list.Contains(w)) list.Add(w);
        _meleeInventory = list.ToArray();
        _meleeIndex = Array.IndexOf(_meleeInventory, w);
    }

    private void UnequipMelee(WeaponType w)
    {
        var list = new List<WeaponType>(_meleeInventory);
        list.Remove(w);
        _meleeInventory = list.ToArray();
        _meleeIndex = _meleeInventory.Length > 0 ? 0 : -1;
    }

    private void EquipRanged(WeaponType w)
    {
        var list = new List<WeaponType>(_rangedInventory);
        if (!list.Contains(w)) list.Add(w);
        _rangedInventory = list.ToArray();
        _rangedIndex = Array.IndexOf(_rangedInventory, w);
    }

    private void UnequipRanged(WeaponType w)
    {
        var list = new List<WeaponType>(_rangedInventory);
        list.Remove(w);
        _rangedInventory = list.ToArray();
        _rangedIndex = _rangedInventory.Length > 0 ? 0 : -1;
    }

    private WeaponType CurrentMelee => _meleeIndex >= 0 && _meleeIndex < _meleeInventory.Length ? _meleeInventory[_meleeIndex] : WeaponType.None;
    private WeaponType CurrentRanged => _rangedIndex >= 0 && _rangedIndex < _rangedInventory.Length ? _rangedInventory[_rangedIndex] : WeaponType.None;

    private void SpawnItemAtPlayer(string itemType)
    {
        _itemPickups.Add(new ItemPickup
        {
            Id = $"debug_{itemType}_{System.DateTime.UtcNow.Ticks}",
            X = _player.Position.X + Player.Width / 2f - 12,
            Y = _player.Position.Y - 20,
            W = 24, H = 12,
            ItemType = itemType,
            HasGravity = true,
            VelY = -100f // pop up slightly
        });
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
        return text;
    }

    private void DrawWrappedText(SpriteFontBase font, string text, Vector2 position, Color color, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return;
        var words = text.Split(' ');
        string currentLine = "";
        float lineHeight = font.MeasureString("A").Y + 4;
        float y = position.Y;

        foreach (var word in words)
        {
            string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (font.MeasureString(testLine).X > maxWidth && currentLine.Length > 0)
            {
                _spriteBatch.DrawString(font, SafeText(currentLine), new Vector2(position.X, y), color);
                y += lineHeight;
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }
        if (currentLine.Length > 0)
            _spriteBatch.DrawString(font, SafeText(currentLine), new Vector2(position.X, y), color);
    }

    // ===================== EDITOR =====================

    private void UpdateEditor(GameTime gameTime, KeyboardState kb)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var mouse = Mouse.GetState();

        // --- Spawn weapon menu (P key) ---
        if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P))
        {
            _spawnMenuOpen = !_spawnMenuOpen;
            if (_spawnMenuOpen) _spawnMenuCursor = 0;
            return;
        }

        if (_spawnMenuOpen)
        {
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _spawnMenuCursor = (_spawnMenuCursor - 1 + SpawnMenuItems.Length) % SpawnMenuItems.Length;
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _spawnMenuCursor = (_spawnMenuCursor + 1) % SpawnMenuItems.Length;
            if (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter) ||
                kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space))
            {
                SpawnItemAtPlayer(SpawnMenuItems[_spawnMenuCursor].ToLower());
                _spawnMenuOpen = false;
            }
            if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                _spawnMenuOpen = false;
            return;
        }

        // Status message countdown
        if (_editorStatusTimer > 0) _editorStatusTimer -= dt;

        // Editor menu (Esc) — if palette is open, close palette instead
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            if (_toolPaletteOpen)
            {
                _toolPaletteOpen = false;
            }
            else if (_entityPaletteOpen)
            {
                _entityPaletteOpen = false;
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
            // TilePaint: select via Q palette only (T key is grab/move)

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

        // Entity palette toggle with E
        if (kb.IsKeyDown(Keys.E) && _prevKb.IsKeyUp(Keys.E) && !_editorMenuOpen)
        {
            _entityPaletteOpen = !_entityPaletteOpen;
            _entityPaletteCursor = 0;
        }

        // Entity palette input
        if (_entityPaletteOpen)
        {
            var entityTypes = Enum.GetValues<EntityType>();
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _entityPaletteCursor = (_entityPaletteCursor - 1 + entityTypes.Length) % entityTypes.Length;
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _entityPaletteCursor = (_entityPaletteCursor + 1) % entityTypes.Length;

            bool place = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                         (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
            if (place)
            {
                var selectedType = entityTypes[_entityPaletteCursor];
                float cx = _editorGridSnap ? MathF.Round(_editorCursor.X / 32) * 32 : _editorCursor.X;
                float cy = _editorGridSnap ? MathF.Round(_editorCursor.Y / 32) * 32 : _editorCursor.Y;

                switch (selectedType)
                {
                    case EntityType.Swarm:
                        var enemyList = new List<EnemySpawnData>(_level.Enemies);
                        // Swarms don't snap — they float freely
                        enemyList.Add(new EnemySpawnData { Id = $"swarm-{enemyList.Count}", Type = "swarm", X = _editorCursor.X, Y = _editorCursor.Y, Count = 10 });
                        _level.Enemies = enemyList.ToArray();
                        SetEditorStatus($"Placed swarm at ({(int)cx}, {(int)cy})");
                        break;
                    case EntityType.Crawler:
                        var cList = new List<EnemySpawnData>(_level.Enemies);
                        cList.Add(new EnemySpawnData { Id = $"crawler-{cList.Count}", Type = "crawler", X = cx, Y = cy });
                        _level.Enemies = cList.ToArray();
                        SetEditorStatus($"Placed crawler at ({(int)cx}, {(int)cy})");
                        break;
                    case EntityType.Thornback:
                        var tList = new List<EnemySpawnData>(_level.Enemies);
                        tList.Add(new EnemySpawnData { Id = $"thornback-{tList.Count}", Type = "thornback", X = cx, Y = cy });
                        _level.Enemies = tList.ToArray();
                        SetEditorStatus($"Placed thornback at ({(int)cx}, {(int)cy})");
                        break;
                    case EntityType.Hopper:
                        var hList = new List<EnemySpawnData>(_level.Enemies);
                        hList.Add(new EnemySpawnData { Id = $"hopper-{hList.Count}", Type = "hopper", X = cx, Y = cy });
                        _level.Enemies = hList.ToArray();
                        SetEditorStatus($"Placed hopper at ({(int)cx}, {(int)cy})");
                        break;
                    case EntityType.Tree:
                        var oList = new List<EnvObjectData>(_level.Objects);
                        float treeSnapY = SnapToSurface(cx, cy, 40, 80);
                        oList.Add(new EnvObjectData { Id = $"tree-{oList.Count}", Type = "tree", X = cx, Y = treeSnapY, W = 40, H = 80 });
                        _level.Objects = oList.ToArray();
                        SetEditorStatus($"Placed tree at ({(int)cx}, {(int)treeSnapY})");
                        break;
                    case EntityType.Bird:
                        var birdList = new List<EnemySpawnData>(_level.Enemies);
                        birdList.Add(new EnemySpawnData { Id = $"bird-{birdList.Count}", Type = "bird", X = cx, Y = cy });
                        _level.Enemies = birdList.ToArray();
                        SetEditorStatus($"Placed bird at ({(int)cx}, {(int)cy})");
                        break;
                }
            }

            if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                _entityPaletteOpen = false;

            return;
        }

        // Back to play mode with =
        if (kb.IsKeyDown(Keys.OemPlus) && _prevKb.IsKeyUp(Keys.OemPlus))
        {
            SaveLevel(); // auto-save on exit editor
            _gameState = GameState.Playing;
            // Rebuild level arrays (sync tile grid data)
            if (_level.TileGridInstance != null)
                _level.TileGrid = _level.TileGridInstance.ToData();
            _level.Build();
            // Save camera position before rebuilding
            float prevCamX = _camera?.Position.X ?? 0f;
            float prevCamY = _camera?.Position.Y ?? 0f;
            _camera = MakeCamera();
            // Keep player at current camera center instead of respawning at spawn point
            float camCX = prevCamX + 400f;
            float camCY = prevCamY + 300f;
            _player.Position = new Vector2(camCX - Player.Width / 2f, camCY - Player.Height / 2f);
            _camera.SnapTo(_player.Position, Player.Width, Player.Height);
            _spawnInvincibility = 2.0f; // extra invincibility in case we land in danger
            _isDead = false;
            _bullets = new List<Bullet>();
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
                SyncInventoryToSave(); _saveData.Save();
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

        // Initialize tile grid if needed when entering tile paint mode
        if (_editorTool == EditorTool.TilePaint && _level.TileGridInstance == null)
        {
            int gridW = (_level.Bounds.Right - _level.Bounds.Left) / 32 + 2;
            int gridH = (_level.Bounds.Bottom - _level.Bounds.Top) / 32 + 2;
            _level.TileGridInstance = new TileGrid(gridW, gridH, 32, _level.Bounds.Left, _level.Bounds.Top);
        }

        // Tile paint mode — continuous paint/erase with mouse
        if (_editorTool == EditorTool.TilePaint && _level.TileGridInstance != null)
        {
            var tg = _level.TileGridInstance;
            // Snap to 32x32 tile grid
            int tileSnappedX = (int)MathF.Floor(worldMouse.X / 32f) * 32;
            int tileSnappedY = (int)MathF.Floor(worldMouse.Y / 32f) * 32;

            // Left click/hold = paint
            if (mouse.LeftButton == ButtonState.Pressed && !kb.IsKeyDown(Keys.T))
            {
                var (tx, ty) = tg.WorldToTile(tileSnappedX, tileSnappedY);
                if (tx >= 0 && ty >= 0)
                {
                    tg.SetTileAt(tx, ty, _selectedTileType);
                }
            }
            // Right click/hold = erase
            if (mouse.RightButton == ButtonState.Pressed)
            {
                var (tx, ty) = tg.WorldToTile(tileSnappedX, tileSnappedY);
                if (tx >= 0 && ty >= 0)
                {
                    tg.SetTileAt(tx, ty, TileType.Empty);
                }
            }

            // Cycle tile type with [ and ]
            if (kb.IsKeyDown(Keys.OemOpenBrackets) && _prevKb.IsKeyUp(Keys.OemOpenBrackets))
            {
                _tilePaletteCursor = (_tilePaletteCursor - 1 + TileProperties.PaletteTiles.Length) % TileProperties.PaletteTiles.Length;
                _selectedTileType = TileProperties.PaletteTiles[_tilePaletteCursor];
                SetEditorStatus($"Tile: {_selectedTileType}");
            }
            if (kb.IsKeyDown(Keys.OemCloseBrackets) && _prevKb.IsKeyUp(Keys.OemCloseBrackets))
            {
                _tilePaletteCursor = (_tilePaletteCursor + 1) % TileProperties.PaletteTiles.Length;
                _selectedTileType = TileProperties.PaletteTiles[_tilePaletteCursor];
                SetEditorStatus($"Tile: {_selectedTileType}");
            }

            // Skip normal drag placement when in tile paint mode
        }

        if (_editorTool == EditorTool.TilePaint)
            goto editorEnd;

        // Left click — place / start drag (not when G is held for grab)
        if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released && !kb.IsKeyDown(Keys.T))
        {
            if (_editorTool == EditorTool.Spawn)
            {
                _level.PlayerSpawn = new PointData { X = (int)snapped.X, Y = (int)snapped.Y };
                SetEditorStatus("Spawn point set");
            }
            else
            {
                _editorDragging = true;
                // Exits don't snap to grid (sub-grid width)
                _editorDragStart = (_editorTool == EditorTool.Exit) ? worldMouse : snapped;
            }
        }

        // Left release — finish placing
        if (mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed && _editorDragging)
        {
            _editorDragging = false;
            var dragEnd = (_editorTool == EditorTool.Exit) ? worldMouse : snapped;
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

        // G + Left click — grab and drag entities/objects
        if (kb.IsKeyDown(Keys.T) && mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
        {
            _editorMovingEntity = null;
            var mp = new Point((int)worldMouse.X, (int)worldMouse.Y);
            // Check platforms
            if (_editorMovingEntity == null)
                foreach (var p in _level.Platforms)
                    if (new Rectangle(p.X, p.Y, p.W, p.H).Contains(mp))
                    { _editorMovingEntity = p; _editorMoveOffset = new Vector2(worldMouse.X - p.X, worldMouse.Y - p.Y); SetEditorStatus("Grabbed platform"); break; }
            // Check walls
            if (_editorMovingEntity == null)
                foreach (var w in _level.Walls)
                    if (new Rectangle(w.X, w.Y, w.W, w.H).Contains(mp))
                    { _editorMovingEntity = w; _editorMoveOffset = new Vector2(worldMouse.X - w.X, worldMouse.Y - w.Y); SetEditorStatus("Grabbed wall"); break; }
            // Check spikes
            if (_editorMovingEntity == null)
                foreach (var s in _level.Spikes)
                    if (new Rectangle(s.X, s.Y, s.W, s.H).Contains(mp))
                    { _editorMovingEntity = s; _editorMoveOffset = new Vector2(worldMouse.X - s.X, worldMouse.Y - s.Y); SetEditorStatus("Grabbed spike"); break; }
            // Check solid floors
            if (_editorMovingEntity == null)
                foreach (var sf in _level.SolidFloors)
                    if (new Rectangle(sf.X, sf.Y, sf.W, sf.H).Contains(mp))
                    { _editorMovingEntity = sf; _editorMoveOffset = new Vector2(worldMouse.X - sf.X, worldMouse.Y - sf.Y); SetEditorStatus("Grabbed solid floor"); break; }
            // Check ceilings
            if (_editorMovingEntity == null)
                foreach (var c in _level.Ceilings)
                    if (new Rectangle(c.X, c.Y, c.W, c.H).Contains(mp))
                    { _editorMovingEntity = c; _editorMoveOffset = new Vector2(worldMouse.X - c.X, worldMouse.Y - c.Y); SetEditorStatus("Grabbed ceiling"); break; }
            // Check ropes (10px tolerance)
            if (_editorMovingEntity == null)
                foreach (var r in _level.Ropes)
                    if (MathF.Abs(mp.X - r.X) < 10 && mp.Y >= r.Top && mp.Y <= r.Bottom)
                    { _editorMovingEntity = r; _editorMoveOffset = new Vector2(worldMouse.X - r.X, worldMouse.Y - r.Top); SetEditorStatus("Grabbed rope"); break; }
            // Check exits
            if (_editorMovingEntity == null)
                foreach (var e in _level.Exits)
                    if (new Rectangle(e.X, e.Y, e.W, e.H).Contains(mp))
                    { _editorMovingEntity = e; _editorMoveOffset = new Vector2(worldMouse.X - e.X, worldMouse.Y - e.Y); SetEditorStatus($"Grabbed exit {e.Id}"); break; }
            // Check wall spikes
            if (_editorMovingEntity == null)
                foreach (var ws in _level.WallSpikes)
                    if (new Rectangle(ws.X, ws.Y, ws.W, ws.H).Contains(mp))
                    { _editorMovingEntity = ws; _editorMoveOffset = new Vector2(worldMouse.X - ws.X, worldMouse.Y - ws.Y); SetEditorStatus("Grabbed wall spike"); break; }
            // Check enemies
            if (_editorMovingEntity == null)
                foreach (var e in _level.Enemies)
                {
                    int sz = e.Type == "thornback" ? 32 : (e.Type == "hopper" ? 20 : (e.Type == "swarm" ? 20 : 16));
                    int h = e.Type == "thornback" ? 28 : (e.Type == "hopper" ? 16 : (e.Type == "swarm" ? 20 : 10));
                    if (new Rectangle((int)e.X, (int)e.Y, sz, h).Contains(mp))
                    { _editorMovingEntity = e; _editorMoveOffset = new Vector2(worldMouse.X - e.X, worldMouse.Y - e.Y); SetEditorStatus($"Grabbed {e.Type}"); break; }
                }
            // Check env objects
            if (_editorMovingEntity == null)
                foreach (var o in _level.Objects)
                    if (new Rectangle((int)o.X, (int)o.Y, o.W, o.H).Contains(mp))
                    { _editorMovingEntity = o; _editorMoveOffset = new Vector2(worldMouse.X - o.X, worldMouse.Y - o.Y); SetEditorStatus($"Grabbed {o.Type}"); break; }
            // Check NPCs
            if (_editorMovingEntity == null)
                foreach (var n in _level.Npcs)
                    if (new Rectangle(n.X, n.Y, n.W, n.H).Contains(mp))
                    { _editorMovingEntity = n; _editorMoveOffset = new Vector2(worldMouse.X - n.X, worldMouse.Y - n.Y); SetEditorStatus($"Grabbed NPC {n.Name}"); break; }
            // Check items
            if (_editorMovingEntity == null)
                foreach (var it in _level.Items)
                    if (new Rectangle((int)it.X, (int)it.Y, it.W, it.H).Contains(mp))
                    { _editorMovingEntity = it; _editorMoveOffset = new Vector2(worldMouse.X - it.X, worldMouse.Y - it.Y); SetEditorStatus($"Grabbed item {it.Type}"); break; }
        }
        // G held + drag — move entity
        if (kb.IsKeyDown(Keys.T) && mouse.LeftButton == ButtonState.Pressed && _editorMovingEntity != null)
        {
            float nx = snapped.X - _editorMoveOffset.X;
            float ny = snapped.Y - _editorMoveOffset.Y;
            if (_editorMovingEntity is RectData rd) { rd.X = (int)nx; rd.Y = (int)ny; }
            else if (_editorMovingEntity is WallData wd) { wd.X = (int)nx; wd.Y = (int)ny; }
            else if (_editorMovingEntity is WallSpikeData wsd) { wsd.X = (int)nx; wsd.Y = (int)ny; }
            else if (_editorMovingEntity is ExitData exd) { exd.X = (int)nx; exd.Y = (int)ny; }
            else if (_editorMovingEntity is RopeData rpd) { float dy = ny - rpd.Top; rpd.X = nx; rpd.Top += dy; rpd.Bottom += dy; }
            else if (_editorMovingEntity is EnemySpawnData esd) { esd.X = nx; esd.Y = ny; }
            else if (_editorMovingEntity is EnvObjectData eod) { eod.X = nx; eod.Y = ny; }
            else if (_editorMovingEntity is NpcData npc) { npc.X = (int)nx; npc.Y = (int)ny; }
            else if (_editorMovingEntity is ItemData itd) { itd.X = nx; itd.Y = ny; }
        }
        // Release — drop entity and rebuild
        if ((mouse.LeftButton == ButtonState.Released || !kb.IsKeyDown(Keys.T)) && _editorMovingEntity != null)
        {
            _level.Build();
            SetEditorStatus("Placed");
            _editorMovingEntity = null;
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
        editorEnd:;
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
        // Check enemies
        for (int i = _level.Enemies.Length - 1; i >= 0; i--)
        {
            var e = _level.Enemies[i];
            int size = e.Type == "thornback" ? 32 : (e.Type == "hopper" ? 20 : (e.Type == "swarm" ? 20 : 16));
            int h = e.Type == "thornback" ? 28 : (e.Type == "hopper" ? 16 : (e.Type == "swarm" ? 20 : 10));
            if (new Rectangle((int)e.X, (int)e.Y, size, h).Contains(p))
            {
                var list = new List<EnemySpawnData>(_level.Enemies);
                list.RemoveAt(i);
                _level.Enemies = list.ToArray();
                return true;
            }
        }
        // Check env objects (trees etc)
        for (int i = _level.Objects.Length - 1; i >= 0; i--)
        {
            var o = _level.Objects[i];
            if (new Rectangle((int)o.X, (int)o.Y, o.W, o.H).Contains(p))
            {
                var list = new List<EnvObjectData>(_level.Objects);
                list.RemoveAt(i);
                _level.Objects = list.ToArray();
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

    /// <summary>Find the nearest surface (platform/solid floor/main floor) below a point and return Y so entity sits on it.</summary>
    private float SnapToSurface(float x, float y, int entityW, int entityH)
    {
        float bestY = _level.Floor.Y - entityH; // default: main floor
        
        // Check platforms
        foreach (var p in _level.Platforms)
        {
            float surfaceY = p.Y - entityH;
            if (x + entityW > p.X && x < p.X + p.W && surfaceY >= y - 20 && surfaceY < bestY)
                bestY = surfaceY;
        }
        // Check solid floors
        foreach (var sf in _level.SolidFloors)
        {
            float surfaceY = sf.Y - entityH;
            if (x + entityW > sf.X && x < sf.X + sf.W && surfaceY >= y - 20 && surfaceY < bestY)
                bestY = surfaceY;
        }
        // Check wall tops (walls can be stood on)
        foreach (var w in _level.Walls)
        {
            float surfaceY = w.Y - entityH;
            if (x + entityW > w.X && x < w.X + w.W && surfaceY >= y - 20 && surfaceY < bestY)
                bestY = surfaceY;
        }
        
        return bestY;
    }

    /// <summary>Find the left/right edges of the surface at the given foot position.</summary>
    private (float, float) FindSurfaceEdges(float x, float footY)
    {
        // Check platforms — find the one the entity is standing on
        foreach (var p in _level.Platforms)
        {
            if (MathF.Abs(footY - p.Y) < 4 && x + Crawler.Width > p.X && x < p.X + p.W)
                return (p.X, p.X + p.W);
        }
        // Check solid floors
        foreach (var sf in _level.SolidFloors)
        {
            if (MathF.Abs(footY - sf.Y) < 4 && x + Crawler.Width > sf.X && x < sf.X + sf.W)
                return (sf.X, sf.X + sf.W);
        }
        // Default: main floor spans the full level bounds
        return (_level.Bounds.Left, _level.Bounds.Right);
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
                    _camera = MakeCamera();
                    _editorCursor = new Vector2(_level.PlayerSpawn.X, _level.PlayerSpawn.Y);
                    _editorMenuOpen = false;
                    break;
                case 5: // Back to game
                    SaveLevel(); // auto-save level edits
                    _gameState = GameState.Playing;
                    _level.Build();
                    _camera = MakeCamera();
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
                        SyncInventoryToSave(); _saveData.Save();
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

    private void SyncInventoryToSave()
    {
        if (_saveData == null) return;
        _saveData.MeleeInventory = new List<string>(Array.ConvertAll(_meleeInventory, w => w.ToString()));
        _saveData.RangedInventory = new List<string>(Array.ConvertAll(_rangedInventory, w => w.ToString()));
        _saveData.MeleeIndex = _meleeIndex;
        _saveData.RangedIndex = _rangedIndex;
    }

    private void SaveLevel()
    {
        var dir = System.IO.Path.GetDirectoryName(_editorSaveFile);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Restore destroyed breakables before saving (runtime-only changes)
        if (_level.TileGridInstance != null)
        {
            foreach (var (col, row) in _destroyedBreakables)
                _level.TileGridInstance.SetTileAt(col, row, TileType.Breakable);
        }

        // Sync tile grid instance to serializable data
        if (_level.TileGridInstance != null)
            _level.TileGrid = _level.TileGridInstance.ToData();

        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var json = System.Text.Json.JsonSerializer.Serialize(_level, opts);
        System.IO.File.WriteAllText(_editorSaveFile, json);

        // Re-destroy breakables after saving
        if (_level.TileGridInstance != null)
        {
            foreach (var (col, row) in _destroyedBreakables)
                _level.TileGridInstance.SetTileAt(col, row, TileType.Empty);
        }

        SetEditorStatus($"Saved to {_editorSaveFile}");
    }

    private void DrawEditor()
    {
        var mouse = Mouse.GetState();

        // World-space rendering with camera (+ screen shake)
        var shakeOffset = Matrix.Identity;
        if (_shakeTimer > 0)
        {
            float sx = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
            float sy = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
            shakeOffset = Matrix.CreateTranslation(sx, sy, 0);
        }
        _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix * shakeOffset);

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
            var botRight = Vector2.Transform(new Vector2(ViewW, ViewH), camInv);
            int gs = _editorGridSize;
            int startX = ((int)topLeft.X / gs) * gs;
            int startY = ((int)topLeft.Y / gs) * gs;
            for (int gx = startX; gx < (int)botRight.X; gx += gs)
                _spriteBatch.Draw(_pixel, new Rectangle(gx, (int)topLeft.Y, 1, (int)(botRight.Y - topLeft.Y)), Color.White * 0.04f);
            for (int gy = startY; gy < (int)botRight.Y; gy += gs)
                _spriteBatch.Draw(_pixel, new Rectangle((int)topLeft.X, gy, (int)(botRight.X - topLeft.X), 1), Color.White * 0.04f);
        }

        // (tile grid drawn after walls — see below)

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

        // Draw tile grid (editor — after walls so tiles paint over them)
        if (_level.TileGridInstance != null)
        {
            var tg = _level.TileGridInstance;
            var camInv2 = Matrix.Invert(_camera.TransformMatrix);
            var tl = Vector2.Transform(Vector2.Zero, camInv2);
            var br = Vector2.Transform(new Vector2(ViewW, ViewH), camInv2);
            int startTX = Math.Max(0, ((int)tl.X - tg.OriginX) / tg.TileSize - 1);
            int startTY = Math.Max(0, ((int)tl.Y - tg.OriginY) / tg.TileSize - 1);
            int endTX = Math.Min(tg.Width, ((int)br.X - tg.OriginX) / tg.TileSize + 2);
            int endTY = Math.Min(tg.Height, ((int)br.Y - tg.OriginY) / tg.TileSize + 2);

            for (int ty = startTY; ty < endTY; ty++)
            {
                for (int tx = startTX; tx < endTX; tx++)
                {
                    var tile = tg.Tiles[tx, ty];
                    if (tile == TileType.Empty) continue;
                    int wx = tg.OriginX + tx * tg.TileSize;
                    int wy = tg.OriginY + ty * tg.TileSize;
                    var rect = new Rectangle(wx, wy, tg.TileSize, tg.TileSize);
                    var color = TileProperties.GetColor(tile);

                    if (tile == TileType.PlatformTop || tile == TileType.PlatformBottom)
                    {
                        int ts = tg.TileSize;
                        int halfH = ts / 2;
                        int py = tile == TileType.PlatformBottom ? wy + halfH : wy;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py, ts, halfH), color);
                        var dark = new Color((int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py, ts, 1), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py + halfH - 1, ts, 1), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py, 1, halfH), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, py, 1, halfH), dark);
                    }
                    else if (TileProperties.IsPlatform(tile))
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 4), color);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 2), Color.White * 0.3f);
                    }
                    else if (TileProperties.IsHazard(tile))
                    {
                        DrawSpikeTile(wx, wy, tg.TileSize, tile, color);
                    }
                    else if (TileProperties.IsSlope(tile))
                    {
                        DrawSlopeTile(wx, wy, tg.TileSize, tile, color);
                    }
                    else if (TileProperties.IsEffectTile(tile) || tile == TileType.Breakable)
                    {
                        int ts = tg.TileSize;
                        // Background fill
                        _spriteBatch.Draw(_pixel, rect, color * 0.4f);
                        var bright = new Color(
                            Math.Min(255, color.R + 80), Math.Min(255, color.G + 80), Math.Min(255, color.B + 80));
                        
                        // Draw symbol based on tile type
                        int cx = wx + ts / 2;
                        int cy = wy + ts / 2;
                        
                        if (tile == TileType.Breakable)
                        {
                            // "X" crack symbol
                            for (int i = -6; i <= 6; i++)
                            {
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + i, cy + i, 2, 2), bright);
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + i, cy - i, 2, 2), bright);
                            }
                        }
                        else if (tile == TileType.DamageTile)
                        {
                            // Flame shape: narrow at bottom, wide in middle, narrow tip
                            int[] flameW = { 2, 4, 6, 8, 10, 10, 8, 6, 6, 4, 2, 2 };
                            for (int i = 0; i < flameW.Length; i++)
                            {
                                int fy = cy + 8 - i * 2;
                                int fw = flameW[i];
                                _spriteBatch.Draw(_pixel, new Rectangle(cx - fw/2, fy, fw, 2), bright);
                            }
                        }
                        else if (tile == TileType.KnockbackTile)
                        {
                            // Arrow pointing right
                            _spriteBatch.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 12, 3), bright); // shaft
                            for (int i = 0; i < 6; i++) // arrowhead
                            {
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + 4, cy - i, 2, 1), bright);
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + 4, cy + i, 2, 1), bright);
                                cx++;
                            }
                        }
                        else if (tile == TileType.SpeedBoostTile)
                        {
                            // Double chevron >>
                            for (int i = -5; i <= 5; i++)
                            {
                                int ax = Math.Abs(i);
                                _spriteBatch.Draw(_pixel, new Rectangle(cx - 6 + ax, cy + i, 2, 1), bright);
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + ax, cy + i, 2, 1), bright);
                            }
                        }
                        else if (tile == TileType.FloatTile)
                        {
                            // Up arrow with wavy lines
                            _spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - 6, 3, 14), bright); // shaft
                            for (int i = 0; i < 5; i++) // arrowhead
                            {
                                _spriteBatch.Draw(_pixel, new Rectangle(cx - i, cy - 6 - i, 1, 1), bright);
                                _spriteBatch.Draw(_pixel, new Rectangle(cx + i, cy - 6 - i, 1, 1), bright);
                            }
                            // wavy lines on sides
                            _spriteBatch.Draw(_pixel, new Rectangle(cx - 7, cy - 2, 3, 2), bright);
                            _spriteBatch.Draw(_pixel, new Rectangle(cx + 5, cy + 2, 3, 2), bright);
                        }
                        else if (tile == TileType.DamageNoKBTile || tile == TileType.DamageFloorTile)
                        {
                            // Smaller flame (like DamageTile but dimmer/smaller)
                            int[] flameW = { 2, 3, 5, 6, 6, 5, 4, 3, 2 };
                            for (int i = 0; i < flameW.Length; i++)
                            {
                                int fy = cy + 6 - i * 2;
                                int fw = flameW[i];
                                _spriteBatch.Draw(_pixel, new Rectangle(cx - fw/2, fy, fw, 2), bright);
                            }
                        }
                        
                        // Border
                        var border = bright * 0.7f;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), border);
                    }
                    else
                    {
                        _spriteBatch.Draw(_pixel, rect, color);
                        if (tile == TileType.Grass)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 4), TileProperties.GetAccentColor(tile));

                        // Outline (non-slope only)
                        var dark = new Color(
                            (int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));
                        int ts = tg.TileSize;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), dark);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), dark);
                    }
                }
            }

            // Ghost preview at cursor for tile paint mode
            if (_editorTool == EditorTool.TilePaint)
            {
                var wm = Vector2.Transform(new Vector2(mouse.X, mouse.Y), Matrix.Invert(_camera.TransformMatrix));
                int gx = (int)MathF.Floor(wm.X / 32f) * 32;
                int gy = (int)MathF.Floor(wm.Y / 32f) * 32;
                _spriteBatch.Draw(_pixel, new Rectangle(gx, gy, 32, 32), TileProperties.GetColor(_selectedTileType) * 0.4f);
                DrawHollowRect(gx, gy, 32, 32, Color.White * 0.5f);
            }
        }

        // Re-draw ceilings over background tiles so they stay visible in editor
        foreach (var c in _level.Ceilings)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y, c.W, c.H), new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y + c.H - 2, c.W, 2), new Color(90, 90, 90));
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

        // Draw item pickups
        for (int i = 0; i < _itemPickups.Count; i++)
        {
            var item = _itemPickups[i];
            if (!item.Collected)
            {
                var itemColor = item.ItemType switch
                {
                    "stick" => new Color(139, 90, 43),
                    "sling" => Color.DarkKhaki,
                    "sword" => Color.Silver,
                    "axe" => Color.DarkGray,
                    "bow" => new Color(160, 120, 60),
                    "gun" => Color.SlateGray,
                    _ => Color.White
                };
                _spriteBatch.Draw(_pixel, item.Rect, itemColor);
            }
        }

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

        // Draw enemy spawns in editor
        foreach (var e in _level.Enemies)
        {
            Color ec = e.Type switch
            {
                "swarm" => Color.OrangeRed,
                "crawler" => new Color(120, 60, 20),
                "hopper" => new Color(80, 140, 60),
                "thornback" => new Color(60, 100, 30),
                _ => Color.White
            };
            int size = e.Type == "thornback" ? 32 : (e.Type == "hopper" ? 20 : (e.Type == "swarm" ? 20 : 16));
            _spriteBatch.Draw(_pixel, new Rectangle((int)e.X, (int)e.Y, size, size), ec * 0.6f);
            _spriteBatch.DrawString(_font, SafeText(e.Type), new Vector2(e.X, e.Y - 14), ec * 0.8f);
        }

        // Draw env objects (trees etc) in editor
        foreach (var obj in _level.Objects)
        {
            if (obj.Type == "tree")
            {
                int trunkW = obj.W / 3;
                int trunkH = obj.H / 2;
                int trunkX = (int)(obj.X + obj.W / 2f - trunkW / 2f);
                int trunkY = (int)(obj.Y + obj.H - trunkH);
                _spriteBatch.Draw(_pixel, new Rectangle(trunkX, trunkY, trunkW, trunkH), new Color(101, 67, 33) * 0.6f);
                int canopySize = obj.W;
                _spriteBatch.Draw(_pixel, new Rectangle((int)obj.X - 4, (int)obj.Y, canopySize + 8, canopySize), Color.ForestGreen * 0.5f);
                _spriteBatch.DrawString(_font, SafeText("tree"), new Vector2(obj.X, obj.Y - 14), Color.ForestGreen * 0.8f);
            }
            else
            {
                _spriteBatch.Draw(_pixel, new Rectangle((int)obj.X, (int)obj.Y, obj.W, obj.H), Color.Gray * 0.4f);
                _spriteBatch.DrawString(_font, SafeText(obj.Type), new Vector2(obj.X, obj.Y - 14), Color.Gray);
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
        string[] toolNames = { "0:Floor", "1:Plat", "2:Rope", "3:Wall", "4:Spike", "5:Exit", "6:Spawn", "7:WSpike", "8:Overworld", "9:Ceiling", "Tile" };
        float toolX = 10;
        for (int i = 0; i < toolNames.Length; i++)
        {
            bool active = (int)_editorTool == i;
            _spriteBatch.DrawString(_font, toolNames[i], new Vector2(toolX, 10), active ? Color.Yellow : Color.Gray * 0.6f);
            toolX += _font.MeasureString(toolNames[i]).X + 15;
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
        string controlsHint = _editorTool == EditorTool.TilePaint
            ? "[=] Play  [Esc] Menu  [Q] Tools  [LClick] Paint  [RClick] Erase  [[ ]] Tile Type"
            : "[=] Play  [Esc] Menu  [Q] Tools  [E] Entities  [Drag] Place  [RClick] Delete  [Tab] Target";
        _spriteBatch.DrawString(_font, controlsHint, new Vector2(10, 550), Color.Gray * 0.35f);

        // Tile type indicator when in tile paint mode
        if (_editorTool == EditorTool.TilePaint)
        {
            string tileInfo = $"Tile: {_selectedTileType}  [{_tilePaletteCursor + 1}/{TileProperties.PaletteTiles.Length}]  RClick=Erase";
            _spriteBatch.DrawString(_font, SafeText(tileInfo), new Vector2(ViewW - 360, 30), Color.Yellow);
            // Mini palette — wrap into rows, right side
            int colsPerRow = 16;
            int tileW = 20;
            int tileH = 20;
            int startX = ViewW - colsPerRow * tileW - 10;
            int startY = 50;
            for (int i = 0; i < TileProperties.PaletteTiles.Length; i++)
            {
                var tt = TileProperties.PaletteTiles[i];
                bool sel = i == _tilePaletteCursor;
                int col = i % colsPerRow;
                int row = i / colsPerRow;
                int px = startX + col * tileW;
                int py = startY + row * tileH;
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, 16, 16), TileProperties.GetColor(tt));
                if (sel)
                    DrawHollowRect(px - 1, py - 1, 18, 18, Color.White);
            }
        }

        // Tool palette overlay
        if (_toolPaletteOpen)
        {
            // Semi-transparent background
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.7f);

            string[] paletteNames = { "Solid Floor", "Platform", "Rope", "Wall", "Spike", "Exit", "Spawn", "Wall Spike", "Overworld Exit", "Ceiling", "Tile Paint" };
            int paletteCount = paletteNames.Length;
            float palW = 260f;
            float palLineH = 24f;
            float palH = paletteCount * palLineH + 60f;
            float palX = (ViewW - palW) / 2f;
            float palY = (ViewH - palH) / 2f;

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

        // Entity palette overlay
        if (_entityPaletteOpen)
        {
            var entityTypes = Enum.GetValues<EntityType>();
            float epalW = 180, epalH = entityTypes.Length * 28 + 20;
            float epalX = ViewW / 2f - epalW / 2f, epalY = ViewH / 2f - epalH / 2f;
            _spriteBatch.Draw(_pixel, new Rectangle((int)epalX, (int)epalY, (int)epalW, (int)epalH), Color.Black * 0.85f);
            _spriteBatch.DrawString(_font, SafeText("ENTITIES [E]"), new Vector2(epalX + 30, epalY + 4), Color.White);

            for (int i = 0; i < entityTypes.Length; i++)
            {
                float itemY = epalY + 24 + i * 28;
                bool selected = i == _entityPaletteCursor;
                if (selected)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)epalX + 4, (int)itemY, (int)epalW - 8, 26), Color.Yellow * 0.15f);

                string label = entityTypes[i].ToString();
                Color iconColor = entityTypes[i] switch
                {
                    EntityType.Swarm => Color.OrangeRed,
                    EntityType.Crawler => new Color(120, 60, 20),
                    EntityType.Thornback => new Color(60, 100, 30),
                    EntityType.Tree => Color.ForestGreen,
                    _ => Color.White
                };
                _spriteBatch.Draw(_pixel, new Rectangle((int)epalX + 10, (int)itemY + 8, 10, 10), iconColor);
                _spriteBatch.DrawString(_font, SafeText(label), new Vector2(epalX + 26, itemY + 4), selected ? Color.Yellow : Color.Gray);
            }
        }

        // Editor menu overlay
        if (_editorMenuOpen)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.8f);

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
        float cx = ViewW / 2f;
        float startY = ViewH / 2f - 100f;
        float lineHeight = 30f;

        if (_settingsActiveCategory == null)
        {
            // Top-level category menu
            { var hdr = "SETTINGS"; var hs = _fontLarge.MeasureString(hdr); _spriteBatch.DrawString(_fontLarge, hdr, new Vector2(cx - hs.X / 2, startY - 45), Color.White); }
            { var sub = "[Esc to close]"; var ss = _fontSmall.MeasureString(sub); _spriteBatch.DrawString(_fontSmall, SafeText(sub), new Vector2(cx - ss.X / 2, startY - 18), Color.Gray * 0.5f); }

            string[] categories = { "Audio", "Graphics", "Debug", "Quit Game" };
            for (int i = 0; i < categories.Length; i++)
            {
                bool selected = i == _settingsCategoryCursor;
                string prefix = selected ? "> " : "  ";
                Color color;
                if (i == 3) // Quit Game
                    color = selected ? Color.Red : Color.DarkGray;
                else
                    color = selected ? Color.Yellow : Color.Gray;
                var txt = $"{prefix}{categories[i]}";
                var ts = _font.MeasureString(txt);
                _spriteBatch.DrawString(_font, SafeText(txt), new Vector2(cx - ts.X / 2, startY + i * lineHeight), color);
            }

            { var hint = "[W/S] Navigate  [Enter/Space] Select"; var hs2 = _fontSmall.MeasureString(hint); _spriteBatch.DrawString(_fontSmall, SafeText(hint), new Vector2(cx - hs2.X / 2, startY + 4 * lineHeight + 20), Color.Gray * 0.6f); }
        }
        else
        {
            // Submenu
            string catName = _settingsActiveCategory.Value.ToString().ToUpper();
            { var hdr = catName; var hs = _fontLarge.MeasureString(hdr); _spriteBatch.DrawString(_fontLarge, hdr, new Vector2(cx - hs.X / 2, startY - 45), Color.White); }
            { var sub = "[Esc to go back]"; var ss = _fontSmall.MeasureString(sub); _spriteBatch.DrawString(_fontSmall, SafeText(sub), new Vector2(cx - ss.X / 2, startY - 18), Color.Gray * 0.5f); }

            if (_settingsActiveCategory == SettingsCategory.Graphics)
            {
                // Show window size with current value
                var (cw, ch, clabel) = WindowSizes[_windowSizeIndex];
                string prefix = "> ";
                string sizeStr = $"{prefix}Window Size: {clabel}";
                var color = Color.Yellow;
                var sizeMs = _font.MeasureString(sizeStr);
                _spriteBatch.DrawString(_font, SafeText(sizeStr), new Vector2(cx - sizeMs.X / 2, startY), color);
                { var hint = "(Enter to cycle)"; var hs2 = _font.MeasureString(hint); _spriteBatch.DrawString(_font, SafeText(hint), new Vector2(cx - hs2.X / 2, startY + lineHeight), Color.Gray * 0.6f); }
            }
            else
            {
                var items = _settingsActiveCategory == SettingsCategory.Audio ? _audioSettings : _debugSettings;
                int itemCount = items.Length;

                if (itemCount <= 4)
                {
                    // Single column — centered
                    for (int i = 0; i < itemCount; i++)
                    {
                        var s = items[i];
                        bool selected = i == _settingsItemCursor;
                        string prefix = selected ? "> " : "  ";
                        string value = s.IsAction ? "" : (s.Get() ? "  ON" : "  OFF");
                        var color = selected ? Color.Yellow : Color.Gray;
                        if (!s.IsAction && s.Get()) color = selected ? Color.Yellow : Color.LightGreen;
                        var txt = $"{prefix}{s.Label}{value}";
                        var ts = _font.MeasureString(txt);
                        _spriteBatch.DrawString(_font, SafeText(txt), new Vector2(cx - ts.X / 2, startY + i * lineHeight), color);
                    }
                }
                else
                {
                    // Two-column layout — centered around middle
                    int half = (itemCount + 1) / 2;
                    for (int i = 0; i < itemCount; i++)
                    {
                        var s = items[i];
                        bool selected = i == _settingsItemCursor;
                        string prefix = selected ? "> " : "  ";
                        string value = s.IsAction ? "" : (s.Get() ? "  ON" : "  OFF");
                        var color = selected ? Color.Yellow : Color.Gray;
                        if (!s.IsAction && s.Get()) color = selected ? Color.Yellow : Color.LightGreen;

                        int col = i < half ? 0 : 1;
                        int row = i < half ? i : i - half;
                        float x = col == 0 ? cx - 220f : cx + 50f;
                        float y = startY + row * lineHeight;
                        _spriteBatch.DrawString(_font, SafeText($"{prefix}{s.Label}{value}"), new Vector2(x, y), color);
                    }
                }
            }

            { var hint = "[Space/Enter] Toggle  [W/S] Navigate  [A/D] Column  [Esc] Back"; var hs2 = _fontSmall.MeasureString(hint); _spriteBatch.DrawString(_fontSmall, SafeText(hint), new Vector2(cx - hs2.X / 2, ViewH - 60), Color.Gray * 0.6f); }
        }
    }

    private void UpdateInventory(KeyboardState kb)
    {
        // Sections: 0=Ranged, 1=Melee
        // Navigate sections with A/D, items with W/S
        if (kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A))
        { _inventorySection = 0; _inventoryIndex = 0; }
        if (kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D))
        { _inventorySection = 1; _inventoryIndex = 0; }

        var inv = _inventorySection == 0 ? _rangedInventory : _meleeInventory;
        if (inv.Length > 0)
        {
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _inventoryIndex = (_inventoryIndex - 1 + inv.Length) % inv.Length;
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _inventoryIndex = (_inventoryIndex + 1) % inv.Length;

            // Space/Enter = equip (set as active)
            if ((kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)))
            {
                if (_inventorySection == 0)
                    _rangedIndex = _inventoryIndex;
                else
                    _meleeIndex = _inventoryIndex;
            }

            // X = discard selected weapon
            if (kb.IsKeyDown(Keys.X) && _prevKb.IsKeyUp(Keys.X))
            {
                var w = inv[_inventoryIndex];
                if (_inventorySection == 0)
                    UnequipRanged(w);
                else
                    UnequipMelee(w);
                // Drop item back into world at player position
                _itemPickups.Add(new ItemPickup
                {
                    X = _player.Position.X,
                    Y = _player.Position.Y + Player.Height - 12,
                    W = 24, H = 12,
                    ItemType = w.ToString().ToLower()
                });
                inv = _inventorySection == 0 ? _rangedInventory : _meleeInventory;
                if (_inventoryIndex >= inv.Length) _inventoryIndex = Math.Max(0, inv.Length - 1);
            }
        }

        // Tab or Esc closes
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
            _inventoryOpen = false;
    }

    private void DrawSpawnMenu()
    {
        // Small overlay
        int menuW = 160, menuH = 30 + SpawnMenuItems.Length * 22;
        int menuX = ViewW / 2 - menuW / 2, menuY = ViewH / 2 - menuH / 2;
        _spriteBatch.Draw(_pixel, new Rectangle(menuX - 2, menuY - 2, menuW + 4, menuH + 4), Color.White * 0.3f);
        _spriteBatch.Draw(_pixel, new Rectangle(menuX, menuY, menuW, menuH), Color.Black * 0.85f);

        _spriteBatch.DrawString(_font, "SPAWN ITEM", new Vector2(menuX + 10, menuY + 6), Color.White);
        for (int i = 0; i < SpawnMenuItems.Length; i++)
        {
            bool selected = i == _spawnMenuCursor;
            string prefix = selected ? "> " : "  ";
            Color color = selected ? Color.Yellow : Color.Gray;
            _spriteBatch.DrawString(_font, prefix + SpawnMenuItems[i], new Vector2(menuX + 10, menuY + 26 + i * 22), color);
        }
    }

    private void DrawInventory()
    {
        // Semi-transparent overlay
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.75f);

        { var hdr = "INVENTORY"; var hs = _fontLarge.MeasureString(hdr); _spriteBatch.DrawString(_fontLarge, hdr, new Vector2(ViewW / 2f - hs.X / 2, 35), Color.White); }
        { var hint = "[A/D] Section  [W/S] Navigate  [Enter] Equip  [X] Discard  [Tab/Esc] Close"; var hs2 = _font.MeasureString(hint); _spriteBatch.DrawString(_font, SafeText(hint),
            new Vector2(ViewW / 2f - hs2.X / 2, ViewH - 40), Color.Gray * 0.5f); }

        // Two columns
        float colX0 = 200, colX1 = 450;
        float startY = 100;

        // Ranged column
        bool rangedSelected = _inventorySection == 0;
        _spriteBatch.DrawString(_font, SafeText("RANGED [1]"), new Vector2(colX0, startY),
            rangedSelected ? Color.Yellow : Color.Gray);
        if (_rangedInventory.Length == 0)
        {
            _spriteBatch.DrawString(_font, SafeText("(empty)"), new Vector2(colX0, startY + 30), Color.Gray * 0.5f);
        }
        for (int i = 0; i < _rangedInventory.Length; i++)
        {
            bool isCurrent = i == _rangedIndex;
            bool isHover = rangedSelected && i == _inventoryIndex;
            string prefix = isCurrent ? "> " : "  ";
            Color c = isHover ? Color.Yellow : (isCurrent ? Color.White : Color.Gray * 0.7f);
            if (isHover)
                _spriteBatch.Draw(_pixel, new Rectangle((int)colX0 - 4, (int)(startY + 30 + i * 24), 160, 22), Color.Yellow * 0.1f);
            _spriteBatch.DrawString(_font, SafeText($"{prefix}{_rangedInventory[i]}"), new Vector2(colX0, startY + 30 + i * 24), c);
        }

        // Melee column
        bool meleeSelected = _inventorySection == 1;
        _spriteBatch.DrawString(_font, SafeText("MELEE [2]"), new Vector2(colX1, startY),
            meleeSelected ? Color.Yellow : Color.Gray);
        if (_meleeInventory.Length == 0)
        {
            _spriteBatch.DrawString(_font, SafeText("(empty)"), new Vector2(colX1, startY + 30), Color.Gray * 0.5f);
        }
        for (int i = 0; i < _meleeInventory.Length; i++)
        {
            bool isCurrent = i == _meleeIndex;
            bool isHover = meleeSelected && i == _inventoryIndex;
            string prefix = isCurrent ? "> " : "  ";
            Color c = isHover ? Color.Yellow : (isCurrent ? Color.White : Color.Gray * 0.7f);
            if (isHover)
                _spriteBatch.Draw(_pixel, new Rectangle((int)colX1 - 4, (int)(startY + 30 + i * 24), 160, 22), Color.Yellow * 0.1f);
            _spriteBatch.DrawString(_font, SafeText($"{prefix}{_meleeInventory[i]}"), new Vector2(colX1, startY + 30 + i * 24), c);
        }
    }

    private void UpdateMenu(KeyboardState kb)
    {
        bool up = (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W)) || (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up));
        bool down = (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S)) || (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down));
        bool confirm = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) || (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
        bool esc = kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape);

        if (_settingsActiveCategory == null)
        {
            // Top-level: 4 items (Audio, Graphics, Debug, Quit)
            if (up) _settingsCategoryCursor = (_settingsCategoryCursor - 1 + 4) % 4;
            if (down) _settingsCategoryCursor = (_settingsCategoryCursor + 1) % 4;

            if (confirm)
            {
                switch (_settingsCategoryCursor)
                {
                    case 0: _settingsActiveCategory = SettingsCategory.Audio; _settingsItemCursor = 0; break;
                    case 1: _settingsActiveCategory = SettingsCategory.Graphics; _settingsItemCursor = 0; break;
                    case 2: _settingsActiveCategory = SettingsCategory.Debug; _settingsItemCursor = 0; break;
                    case 3:
                        _menuOpen = false;
                        _settingsFromTitle = false;
                        _settingsActiveCategory = null;
                        _gameState = GameState.Title;
                        break;
                }
            }

            if (esc)
            {
                _menuOpen = false;
                if (_settingsFromTitle) _settingsFromTitle = false;
            }
        }
        else
        {
            // In a submenu
            if (_settingsActiveCategory == SettingsCategory.Graphics)
            {
                if (confirm)
                {
                    ApplyWindowSize(_windowSizeIndex + 1);
                }
                if (esc) _settingsActiveCategory = null;
                return;
            }

            var items = _settingsActiveCategory == SettingsCategory.Audio ? _audioSettings : _debugSettings;
            int itemCount = items.Length;

            if (itemCount <= 4)
            {
                // Single column navigation
                if (up) _settingsItemCursor = (_settingsItemCursor - 1 + itemCount) % itemCount;
                if (down) _settingsItemCursor = (_settingsItemCursor + 1) % itemCount;
            }
            else
            {
                // Two-column navigation (same as old layout)
                int half = (itemCount + 1) / 2;
                int rightCount = itemCount - half;

                if (up)
                {
                    int col = _settingsItemCursor < half ? 0 : 1;
                    int row = col == 0 ? _settingsItemCursor : _settingsItemCursor - half;
                    if (row == 0)
                    {
                        int colSize = col == 0 ? half : rightCount;
                        _settingsItemCursor = col == 0 ? half - 1 : half + rightCount - 1; // wrap to bottom
                    }
                    else
                    {
                        row--;
                        _settingsItemCursor = col == 0 ? row : row + half;
                    }
                }
                if (down)
                {
                    int col = _settingsItemCursor < half ? 0 : 1;
                    int row = col == 0 ? _settingsItemCursor : _settingsItemCursor - half;
                    int colSize = col == 0 ? half : rightCount;
                    if (row == colSize - 1)
                    {
                        _settingsItemCursor = col == 0 ? 0 : half; // wrap to top
                    }
                    else
                    {
                        row++;
                        _settingsItemCursor = col == 0 ? row : row + half;
                    }
                }

                // A/D column switching
                bool right = (kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D)) || (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right));
                bool left = (kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A)) || (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left));
                if (right && _settingsItemCursor < half && _settingsItemCursor + half < itemCount)
                    _settingsItemCursor += half;
                if (left && _settingsItemCursor >= half)
                    _settingsItemCursor -= half;
            }

            if (confirm) items[_settingsItemCursor].Toggle();
            if (esc) _settingsActiveCategory = null;
        }
    }

    private void UpdateOverworld(KeyboardState kb)
    {
        if (_worldMap == null) _worldMap = WorldMapData.LoadOrCreate();

        // Biome level menu open?
        if (_worldMapBiomeMenuId != null)
        {
            UpdateBiomeMenu(kb);
            return;
        }

        // Movement
        bool up = kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W);
        bool down = kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S);
        bool left = kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A);
        bool right = kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D);
        if (!up) up = kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up);
        if (!down) down = kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down);
        if (!left) left = kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left);
        if (!right) right = kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right);

        int nx = _worldMap.PlayerX, ny = _worldMap.PlayerY;
        if (up) ny--;
        if (down) ny++;
        if (left) nx--;
        if (right) nx++;

        // Bounds + walkability check
        if (nx >= 0 && nx < WorldMapData.MapW && ny >= 0 && ny < WorldMapData.MapH)
        {
            var tile = _worldMap.GetTile(nx, ny);
            if (tile != MapTileType.Ocean && tile != MapTileType.DeepOcean &&
                tile != MapTileType.ShallowOcean && tile != MapTileType.Reef &&
                tile != MapTileType.HighMountain && tile != MapTileType.Volcano &&
                tile != MapTileType.Lake && tile != MapTileType.FrozenLake &&
                tile != MapTileType.Glacier && tile != MapTileType.VoidRift)
            {
                _worldMap.PlayerX = nx;
                _worldMap.PlayerY = ny;
                _worldMap.Reveal(nx, ny);
            }
        }

        // Backtick (`) — instant teleport to debug room
        if (kb.IsKeyDown(Keys.OemTilde) && _prevKb.IsKeyUp(Keys.OemTilde))
        {
            LoadLevel("Content/levels/debug-room.json");
            _editorSaveFile = "Content/levels/debug-room.json";
            _camera = MakeCamera();
            Restart();
            _gameState = GameState.Playing;
            return;
        }

        // Enter/Space on biome entrance
        bool enter = kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter);
        bool space = kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space);
        if (enter || space)
        {
            var point = _worldMap.Points.FirstOrDefault(p =>
                Math.Abs(p.X - _worldMap.PlayerX) <= 1 && Math.Abs(p.Y - _worldMap.PlayerY) <= 1);
            if (point != null && !string.IsNullOrEmpty(point.BiomeId))
            {
                var biome = _worldMap.FindBiome(point.BiomeId);
                if (biome != null)
                {
                    if (biome.Cleared && enter)
                    {
                        // Sim mode for cleared biomes
                        _simRegion = SimRegion.LoadOrGenerate(biome.Id);
                        _simCursorX = SimRegion.GridW / 2;
                        _simCursorY = SimRegion.GridH / 2;
                        _simNodeId = biome.Id;
                        _gameState = GameState.SimMode;
                    }
                    else
                    {
                        // Open biome level menu
                        _worldMapBiomeMenuId = biome.Id;
                        _worldMapBiomeMenuCursor = 0;
                    }
                }
            }
        }

        // Toggle grid
        if (kb.IsKeyDown(Keys.G) && _prevKb.IsKeyUp(Keys.G))
            _worldMapGridVisible = !_worldMapGridVisible;

        if (kb.IsKeyDown(Keys.L) && _prevKb.IsKeyUp(Keys.L))
            _worldMapLegendVisible = !_worldMapLegendVisible;

        // Debug: reveal all fog
        if (kb.IsKeyDown(Keys.F) && _prevKb.IsKeyUp(Keys.F))
        {
            for (int i = 0; i < _worldMap.Revealed.Length; i++)
                _worldMap.Revealed[i] = true;
        }

        // Zoom: Q = zoom out, E = zoom in
        if (kb.IsKeyDown(Keys.Q) && _prevKb.IsKeyUp(Keys.Q))
            _worldMapZoom = MathHelper.Clamp(_worldMapZoom - 0.25f, 0.25f, 2f);
        if (kb.IsKeyDown(Keys.E) && _prevKb.IsKeyUp(Keys.E))
            _worldMapZoom = MathHelper.Clamp(_worldMapZoom + 0.25f, 0.25f, 2f);

        // Escape
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _worldMap.Save();
            _gameState = GameState.Title;
        }

        // M key
        if (kb.IsKeyDown(Keys.M) && _prevKb.IsKeyUp(Keys.M))
        {
            _worldMap.Save();
            if (_level != null)
                _gameState = GameState.Playing;
            else
                _gameState = GameState.Title;
        }
    }

    private void UpdateBiomeMenu(KeyboardState kb)
    {
        var biome = _worldMap.FindBiome(_worldMapBiomeMenuId);
        if (biome == null) { _worldMapBiomeMenuId = null; return; }

        var discovered = biome.Levels.Where(l => l.Discovered).ToList();
        if (discovered.Count == 0) { _worldMapBiomeMenuId = null; return; }

        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
            _worldMapBiomeMenuCursor = (_worldMapBiomeMenuCursor - 1 + discovered.Count) % discovered.Count;
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
            _worldMapBiomeMenuCursor = (_worldMapBiomeMenuCursor + 1) % discovered.Count;
        if (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up))
            _worldMapBiomeMenuCursor = (_worldMapBiomeMenuCursor - 1 + discovered.Count) % discovered.Count;
        if (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down))
            _worldMapBiomeMenuCursor = (_worldMapBiomeMenuCursor + 1) % discovered.Count;

        bool confirm = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                       (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
        if (confirm)
        {
            var level = discovered[_worldMapBiomeMenuCursor];
            string path = $"Content/levels/{level.LevelFile}.json";
            if (System.IO.File.Exists(path))
            {
                LoadLevel(path);
                _editorSaveFile = path;
                _camera = MakeCamera();
                _gameState = GameState.Playing;
                Restart();
                _prevInExit = new bool[_level.ExitRects.Length];
                for (int k = 0; k < _prevInExit.Length; k++)
                    _prevInExit[k] = true;
                _worldMapBiomeMenuId = null;
            }
        }

        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
            _worldMapBiomeMenuId = null;
    }

    private void DrawOverworld()
    {
        if (_worldMap == null) return;
        GraphicsDevice.Clear(new Color(8, 8, 16));
        _spriteBatch.Begin();

        int ts = (int)(WorldMapData.TileSize * _worldMapZoom);
        if (ts < 4) ts = 4;

        // Camera centered on player
        float camX = _worldMap.PlayerX * ts + ts / 2f - ViewW / 2f;
        float camY = _worldMap.PlayerY * ts + ts / 2f - ViewH / 2f;

        // Draw visible tiles
        int startX = Math.Max(0, (int)(camX / ts) - 1);
        int startY = Math.Max(0, (int)(camY / ts) - 1);
        int endX = Math.Min(WorldMapData.MapW, (int)((camX + ViewW) / ts) + 2);
        int endY = Math.Min(WorldMapData.MapH, (int)((camY + ViewH) / ts) + 2);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                int sx = (int)(x * ts - camX);
                int sy = (int)(y * ts - camY);
                var rect = new Rectangle(sx, sy, ts, ts);

                if (!_worldMap.IsRevealed(x, y))
                {
                    _spriteBatch.Draw(_pixel, rect, new Color(5, 5, 10));
                    continue;
                }

                var tile = _worldMap.GetTile(x, y);
                Color fill = GetWorldTileColor(tile);
                _spriteBatch.Draw(_pixel, rect, fill);

                if (_worldMapGridVisible)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(sx, sy, ts, 1), Color.White * 0.1f);
                    _spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 1, ts), Color.White * 0.1f);
                }
            }
        }

        // Draw biome entrance markers
        foreach (var point in _worldMap.Points)
        {
            if (!_worldMap.IsRevealed(point.X, point.Y)) continue;
            int px = (int)(point.X * ts - camX);
            int py = (int)(point.Y * ts - camY);

            float pulse = 0.7f + 0.3f * MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 3f);
            var biome = _worldMap.FindBiome(point.BiomeId);
            Color markerColor = biome != null && biome.Cleared ? Color.LimeGreen * pulse : Color.Gold * pulse;
            int markerSize = 10;
            _spriteBatch.Draw(_pixel, new Rectangle(px + ts / 2 - markerSize / 2, py + ts / 2 - markerSize / 2, markerSize, markerSize), markerColor);

            var labelSize = _fontSmall.MeasureString(point.Label);
            _spriteBatch.DrawString(_fontSmall, point.Label, new Vector2(px + ts / 2f - labelSize.X / 2, py - 14), Color.White * 0.8f);
        }

        // Draw player
        {
            int px = (int)(_worldMap.PlayerX * ts - camX);
            int py = (int)(_worldMap.PlayerY * ts - camY);
            int pSize = 12;
            int pOff = (ts - pSize) / 2;
            _spriteBatch.Draw(_pixel, new Rectangle(px + pOff - 1, py + pOff - 1, pSize + 2, pSize + 2), Color.Black);
            _spriteBatch.Draw(_pixel, new Rectangle(px + pOff, py + pOff, pSize, pSize), Color.White);
        }

        // Header
        string header = "WORLD MAP";
        var hs = _fontLarge.MeasureString(header);
        _spriteBatch.DrawString(_fontLarge, header, new Vector2(ViewW / 2f - hs.X / 2, 8), Color.White * 0.6f);

        // Position
        string posInfo = $"({_worldMap.PlayerX}, {_worldMap.PlayerY}) Zoom: {_worldMapZoom:0.##}x";
        _spriteBatch.DrawString(_fontSmall, posInfo, new Vector2(10, ViewH - 45), Color.Gray * 0.4f);

        // Proximity hint
        var nearPoint = _worldMap.Points.FirstOrDefault(p =>
            Math.Abs(p.X - _worldMap.PlayerX) <= 1 && Math.Abs(p.Y - _worldMap.PlayerY) <= 1);
        if (nearPoint != null)
        {
            var biome = _worldMap.FindBiome(nearPoint.BiomeId);
            string status = biome != null && biome.Cleared ? "(Cleared)" : "";
            string prompt = $"[Enter] {nearPoint.Label} {status}";
            var promptSize = _font.MeasureString(prompt);
            _spriteBatch.DrawString(_font, prompt, new Vector2(ViewW / 2f - promptSize.X / 2, ViewH - 75), Color.Yellow);
        }

        // Controls
        string hint = "[WASD] Move  [Q/E] Zoom  [Enter] Enter  [G] Grid  [F] Reveal  [L] Legend  [M] Close  [Esc] Title";
        var hintSize = _fontSmall.MeasureString(hint);
        _spriteBatch.DrawString(_fontSmall, hint, new Vector2(ViewW / 2f - hintSize.X / 2, ViewH - 25), Color.Gray * 0.4f);

        // Tile legend (toggle with L)
        if (_worldMapLegendVisible)
        {
            var legendTiles = new (MapTileType type, string name)[]
            {
                (MapTileType.DeepOcean, "Deep Ocean"),
                (MapTileType.Ocean, "Ocean"),
                (MapTileType.ShallowOcean, "Shallow"),
                (MapTileType.Reef, "Reef"),
                (MapTileType.Beach, "Beach"),
                (MapTileType.RockyShore, "Rocky Shore"),
                (MapTileType.Plains, "Plains"),
                (MapTileType.Grassland, "Grassland"),
                (MapTileType.Savanna, "Savanna"),
                (MapTileType.Floodplain, "Floodplain"),
                (MapTileType.Forest, "Forest"),
                (MapTileType.DenseForest, "Dense Forest"),
                (MapTileType.Jungle, "Jungle"),
                (MapTileType.Swamp, "Swamp"),
                (MapTileType.Marsh, "Marsh"),
                (MapTileType.Hills, "Hills"),
                (MapTileType.Foothills, "Foothills"),
                (MapTileType.Mountain, "Mountain"),
                (MapTileType.HighMountain, "High Mountain"),
                (MapTileType.Snow, "Snow"),
                (MapTileType.Glacier, "Glacier"),
                (MapTileType.Tundra, "Tundra"),
                (MapTileType.Taiga, "Taiga"),
                (MapTileType.SnowForest, "Snow Forest"),
                (MapTileType.FrozenLake, "Frozen Lake"),
                (MapTileType.Desert, "Desert"),
                (MapTileType.Dunes, "Dunes"),
                (MapTileType.Wasteland, "Wasteland"),
                (MapTileType.River, "River"),
                (MapTileType.Lake, "Lake"),
                (MapTileType.Cave, "Cave"),
                (MapTileType.Volcano, "Volcano"),
                (MapTileType.Ruins, "Ruins"),
                (MapTileType.Oasis, "Oasis"),
                (MapTileType.CrystalForest, "Crystal Forest"),
                (MapTileType.Ashlands, "Ashlands"),
                (MapTileType.VoidRift, "Void Rift"),
                (MapTileType.Mushroom, "Mushroom"),
                (MapTileType.Petrified, "Petrified"),
                (MapTileType.BiomeEntrance, "Biome Entrance"),
            };
            int legendX = ViewW - 160;
            int legendY = 30;
            int rowH = 14;
            // Background panel
            _spriteBatch.Draw(_pixel, new Rectangle(legendX - 6, legendY - 4, 164, legendTiles.Length * rowH + 8), Color.Black * 0.75f);
            for (int i = 0; i < legendTiles.Length; i++)
            {
                int ly = legendY + i * rowH;
                Color c = GetWorldTileColor(legendTiles[i].type);
                _spriteBatch.Draw(_pixel, new Rectangle(legendX, ly + 1, 10, 10), c);
                _spriteBatch.DrawString(_fontSmall, legendTiles[i].name, new Vector2(legendX + 14, ly), Color.White * 0.8f);
            }
        }

        // Biome menu overlay
        if (_worldMapBiomeMenuId != null)
            DrawBiomeMenu();

        _spriteBatch.End();
    }

    private void DrawBiomeMenu()
    {
        var biome = _worldMap.FindBiome(_worldMapBiomeMenuId);
        if (biome == null) return;

        var discovered = biome.Levels.Where(l => l.Discovered).ToList();

        float boxW = 350, boxH = 60 + discovered.Count * 30;
        float bx = ViewW / 2f - boxW / 2f;
        float by = ViewH / 2f - boxH / 2f;
        _spriteBatch.Draw(_pixel, new Rectangle((int)bx, (int)by, (int)boxW, (int)boxH), new Color(10, 10, 20) * 0.95f);
        _spriteBatch.Draw(_pixel, new Rectangle((int)bx, (int)by, (int)boxW, 2), Color.White * 0.5f);
        _spriteBatch.Draw(_pixel, new Rectangle((int)bx, (int)(by + boxH - 2), (int)boxW, 2), Color.White * 0.5f);

        var nameSize = _font.MeasureString(biome.Name);
        _spriteBatch.DrawString(_font, biome.Name, new Vector2(ViewW / 2f - nameSize.X / 2, by + 10), Color.White);

        for (int i = 0; i < discovered.Count; i++)
        {
            var level = discovered[i];
            bool selected = i == _worldMapBiomeMenuCursor;
            string prefix = selected ? "> " : "  ";
            string cleared = level.Cleared ? " [Cleared]" : "";
            string text = $"{prefix}{level.Name}{cleared}";
            Color color = selected ? Color.Yellow : (level.Cleared ? Color.LimeGreen * 0.7f : Color.Gray);
            var textSize = _font.MeasureString(text);
            _spriteBatch.DrawString(_font, text, new Vector2(ViewW / 2f - textSize.X / 2, by + 40 + i * 30), color);
        }
    }

    private static Color GetWorldTileColor(MapTileType tile)
    {
        return tile switch
        {
            // Water
            MapTileType.DeepOcean => new Color(8, 12, 40),
            MapTileType.Ocean => new Color(16, 28, 62),
            MapTileType.ShallowOcean => new Color(25, 50, 90),
            MapTileType.Reef => new Color(30, 75, 95),
            MapTileType.Water => new Color(30, 55, 110),
            MapTileType.Lake => new Color(35, 65, 120),
            MapTileType.River => new Color(32, 58, 115),
            MapTileType.FrozenLake => new Color(160, 180, 200),
            // Coast
            MapTileType.Beach => new Color(195, 180, 135),
            MapTileType.RockyShore => new Color(130, 120, 100),
            // Lowland
            MapTileType.Plains => new Color(65, 105, 50),
            MapTileType.Grassland => new Color(80, 120, 55),
            MapTileType.Savanna => new Color(140, 135, 65),
            MapTileType.Floodplain => new Color(55, 90, 45),
            // Forest
            MapTileType.Forest => new Color(30, 65, 25),
            MapTileType.DenseForest => new Color(15, 42, 14),
            MapTileType.Jungle => new Color(20, 55, 15),
            // Wet
            MapTileType.Swamp => new Color(50, 65, 35),
            MapTileType.Marsh => new Color(60, 75, 50),
            // Highland
            MapTileType.Hills => new Color(95, 100, 65),
            MapTileType.Foothills => new Color(110, 105, 78),
            MapTileType.Mountain => new Color(120, 110, 95),
            MapTileType.HighMountain => new Color(145, 140, 130),
            MapTileType.Snow => new Color(225, 230, 240),
            MapTileType.Glacier => new Color(180, 200, 220),
            // Cold
            MapTileType.Tundra => new Color(150, 160, 145),
            MapTileType.Taiga => new Color(45, 60, 40),
            MapTileType.SnowForest => new Color(100, 120, 105),
            // Hot
            MapTileType.Desert => new Color(180, 160, 95),
            MapTileType.Dunes => new Color(200, 180, 110),
            MapTileType.Wasteland => new Color(135, 115, 80),
            // Special
            MapTileType.Cave => new Color(55, 45, 40),
            MapTileType.Volcano => new Color(75, 28, 18),
            MapTileType.Ruins => new Color(90, 80, 70),
            MapTileType.Oasis => new Color(50, 100, 55),
            // Fantasy
            MapTileType.CrystalForest => new Color(120, 160, 200),
            MapTileType.Ashlands => new Color(60, 40, 35),
            MapTileType.VoidRift => new Color(80, 20, 120),
            MapTileType.Mushroom => new Color(140, 60, 140),
            MapTileType.Petrified => new Color(110, 100, 85),
            MapTileType.Path => new Color(140, 125, 90),
            MapTileType.BiomeEntrance => new Color(90, 75, 55),
            _ => new Color(20, 20, 20),
        };
    }


    // ── Sim Mode ──────────────────────────────────────────

    private void UpdateSimMode(KeyboardState kb)
    {
        // Cursor movement
        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W)) _simCursorY = Math.Max(0, _simCursorY - 1);
        if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S)) _simCursorY = Math.Min(SimRegion.GridH - 1, _simCursorY + 1);
        if (kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A)) _simCursorX = Math.Max(0, _simCursorX - 1);
        if (kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D)) _simCursorX = Math.Min(SimRegion.GridW - 1, _simCursorX + 1);

        // Also arrow keys
        if (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up)) _simCursorY = Math.Max(0, _simCursorY - 1);
        if (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down)) _simCursorY = Math.Min(SimRegion.GridH - 1, _simCursorY + 1);
        if (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left)) _simCursorX = Math.Max(0, _simCursorX - 1);
        if (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right)) _simCursorX = Math.Min(SimRegion.GridW - 1, _simCursorX + 1);

        // Escape → back to overworld
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
        {
            _simRegion.Save();
            _gameState = GameState.Overworld;
        }

        // M key → back to overworld
        if (kb.IsKeyDown(Keys.M) && _prevKb.IsKeyUp(Keys.M))
        {
            _simRegion.Save();
            _gameState = GameState.Overworld;
        }
    }

    private void DrawSimMode()
    {
        GraphicsDevice.Clear(new Color(10, 14, 10));
        _spriteBatch.Begin();

        if (_simRegion == null) { _spriteBatch.End(); return; }

        // Center the grid on screen
        int ts = SimRegion.TileSize;
        float gridPixW = SimRegion.GridW * ts;
        float gridPixH = SimRegion.GridH * ts;
        float ox = (ViewW - gridPixW) / 2f;
        float oy = (ViewH - gridPixH) / 2f + 15;

        // Draw tiles
        for (int y = 0; y < SimRegion.GridH; y++)
        {
            for (int x = 0; x < SimRegion.GridW; x++)
            {
                var tile = _simRegion.GetTile(x, y);
                var rect = new Rectangle((int)(ox + x * ts), (int)(oy + y * ts), ts, ts);
                Color fill = GetSimTileColor(tile);
                _spriteBatch.Draw(_pixel, rect, fill);

                // Grid lines
                _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, ts, 1), Color.Black * 0.3f);
                _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, ts), Color.Black * 0.3f);
            }
        }

        // Draw cursor
        {
            var cRect = new Rectangle((int)(ox + _simCursorX * ts), (int)(oy + _simCursorY * ts), ts, ts);
            int b = 2;
            _spriteBatch.Draw(_pixel, new Rectangle(cRect.X - b, cRect.Y - b, cRect.Width + b * 2, b), Color.White);
            _spriteBatch.Draw(_pixel, new Rectangle(cRect.X - b, cRect.Bottom, cRect.Width + b * 2, b), Color.White);
            _spriteBatch.Draw(_pixel, new Rectangle(cRect.X - b, cRect.Y, b, cRect.Height), Color.White);
            _spriteBatch.Draw(_pixel, new Rectangle(cRect.Right, cRect.Y, b, cRect.Height), Color.White);
        }

        // Header
        string nodeName = _simNodeId ?? "Unknown";
        var node = _overworld?.FindNode(_simNodeId);
        if (node != null) nodeName = node.Name ?? node.Id;
        string header = $"SETTLEMENT: {nodeName.ToUpper()}";
        var hs = _fontLarge.MeasureString(header);
        _spriteBatch.DrawString(_fontLarge, header, new Vector2(ViewW / 2f - hs.X / 2, 8), Color.White);

        // Tile info at cursor
        var curTile = _simRegion.GetTile(_simCursorX, _simCursorY);
        string tileLabel = curTile.ToString();
        string info = $"({_simCursorX},{_simCursorY}) {tileLabel}";
        var infoSize = _font.MeasureString(info);
        _spriteBatch.DrawString(_font, info, new Vector2(ViewW / 2f - infoSize.X / 2, ViewH - 55), Color.Yellow);

        // Population
        string pop = $"Population: {_simRegion.Population}";
        var popSize = _font.MeasureString(pop);
        _spriteBatch.DrawString(_font, pop, new Vector2(ViewW - popSize.X - 20, 40), Color.LightGreen);

        // Controls
        string hint = "[WASD] Move  [Esc/M] Overworld";
        var hintSize = _fontSmall.MeasureString(hint);
        _spriteBatch.DrawString(_fontSmall, hint, new Vector2(ViewW / 2f - hintSize.X / 2, ViewH - 30), Color.Gray * 0.5f);

        _spriteBatch.End();
    }

    private static Color GetSimTileColor(SimTileType tile)
    {
        return tile switch
        {
            SimTileType.Grass => new Color(34, 80, 34),
            SimTileType.Forest => new Color(15, 55, 15),
            SimTileType.Rock => new Color(90, 85, 75),
            SimTileType.Water => new Color(30, 50, 100),
            SimTileType.Ruins => new Color(100, 85, 60),
            SimTileType.Road => new Color(120, 110, 90),
            SimTileType.Hub => new Color(200, 180, 60),
            SimTileType.Shelter => new Color(140, 100, 60),
            SimTileType.Farm => new Color(80, 140, 40),
            SimTileType.Workshop => new Color(130, 80, 50),
            SimTileType.Wall => new Color(110, 110, 120),
            SimTileType.MonsterLair => new Color(120, 20, 30),
            _ => new Color(20, 20, 20),
        };
    }

    private void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        float angle = MathF.Atan2(dy, dx);
        _spriteBatch.Draw(_pixel, new Rectangle(x1, y1, (int)length, 2), null, color, angle, Vector2.Zero, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
    }

    private void DrawHollowRect(int x, int y, int w, int h, Color c)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, 1), c);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + h - 1, w, 1), c);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, h), c);
        _spriteBatch.Draw(_pixel, new Rectangle(x + w - 1, y, 1, h), c);
    }

    /// <summary>Draw a slope tile as a filled shape (scanline).</summary>
    private void DrawSlopeTile(int wx, int wy, int ts, TileType tile, Color color)
    {
        for (int row = 0; row < ts; row++)
        {
            int lineY = wy + row;
            int lineX, lineW;

            switch (tile)
            {
                case TileType.SlopeUpRight:
                    // ╱ shape: solid below diagonal (bottom-left to top-right)
                    lineX = wx + (ts - 1 - row);
                    lineW = row + 1;
                    break;
                case TileType.SlopeUpLeft:
                    // ╲ shape: solid below diagonal (bottom-right to top-left)
                    lineX = wx;
                    lineW = row + 1;
                    break;
                case TileType.SlopeCeilRight:
                    // Ceiling: solid top-right triangle (diagonal from top-left to bottom-right)
                    lineX = wx + row;
                    lineW = ts - row;
                    break;
                case TileType.SlopeCeilLeft:
                    // Ceiling: solid top-left triangle (diagonal from top-right to bottom-left)
                    lineX = wx;
                    lineW = ts - row;
                    break;
                case TileType.GentleCeilRight:
                    lineX = wx + Math.Min(row * 2, ts);
                    lineW = Math.Max(0, ts - row * 2);
                    break;
                case TileType.GentleCeilLeft:
                    lineX = wx;
                    lineW = Math.Max(0, ts - row * 2);
                    break;
                case TileType.ShavedCeilRight:
                {
                    // Full block with bottom-right shaved off
                    int sr = Math.Max(0, row - ts / 2);
                    lineX = wx;
                    lineW = Math.Max(0, ts - sr * 2);
                    break;
                }
                case TileType.ShavedCeilLeft:
                {
                    // Full block with bottom-left shaved off
                    int sr = Math.Max(0, row - ts / 2);
                    lineX = wx + sr * 2;
                    lineW = Math.Max(0, ts - sr * 2);
                    break;
                }
                case TileType.GentleUpRight:
                    // Rises right: surface from (0,ts) to (ts,ts/2)
                    // At row r, fill from the surface X to the right edge
                    // Surface X at row r: x where the line y=r intersects
                    // Line: y = ts - (x/ts)*(ts/2) → x = (ts - y) * 2
                    {
                        if (row < ts / 2) continue; // above the highest point
                        int surfX = (int)((ts - row) * 2f);
                        if (surfX >= ts) continue;
                        lineX = wx + surfX;
                        lineW = ts - surfX;
                    }
                    break;
                case TileType.GentleUpLeft:
                    // Rises left: surface from (0,ts/2) to (ts,ts)
                    {
                        if (row < ts / 2) continue;
                        int fillW = (int)((row - ts / 2) * 2f) + 1;
                        if (fillW > ts) fillW = ts;
                        lineX = wx;
                        lineW = fillW;
                    }
                    break;
                case TileType.ShavedRight:
                    // Full block with gentle slope shaved off top-right
                    // Surface: from (0,0) flat to (ts, ts/2) — the shave is a triangle cut from top-right
                    // At row r < ts/2: fill from left edge to the shave line
                    // Shave line at row r: x = r * 2 (from top-right corner)
                    {
                        lineX = wx;
                        if (row < ts / 2)
                        {
                            lineW = (int)(row * 2f) + 1;
                            if (lineW > ts) lineW = ts;
                        }
                        else
                        {
                            lineW = ts;
                        }
                    }
                    break;
                case TileType.ShavedLeft:
                    // Full block with gentle slope shaved off top-left
                    {
                        if (row < ts / 2)
                        {
                            int cut = ts - (int)(row * 2f) - 1;
                            if (cut < 0) cut = 0;
                            lineX = wx + cut;
                            lineW = ts - cut;
                        }
                        else
                        {
                            lineX = wx;
                            lineW = ts;
                        }
                    }
                    break;
                case TileType.Gentle4UpRightA:
                case TileType.Gentle4UpRightB:
                case TileType.Gentle4UpRightC:
                case TileType.Gentle4UpRightD:
                    {
                        int qi = tile - TileType.Gentle4UpRightA; // 0=A, 1=B, 2=C, 3=D
                        // UpRight rises going right. A is shallowest.
                        // A: surface left=32, right=24. B: left=24, right=16. C: left=16, right=8. D: left=8, right=0.
                        int qBase = ts - qi * (ts / 4);       // surface Y at left edge (higher value = lower)
                        int qTop = qBase - ts / 4;            // surface Y at right edge
                        if (row < qTop) continue;
                        if (row >= qBase)
                        {
                            lineX = wx;
                            lineW = ts;
                        }
                        else
                        {
                            // surface X where row intersects: row = qBase - x/(ts/(ts/4)) = qBase - x/4
                            // x = (qBase - row) * 4
                            int surfX = (qBase - row) * 4;
                            if (surfX > ts) surfX = ts;
                            if (surfX < 0) surfX = 0;
                            lineX = wx + surfX;
                            lineW = ts - surfX;
                            if (lineW <= 0) continue;
                        }
                    }
                    break;
                case TileType.Gentle4UpLeftA:
                case TileType.Gentle4UpLeftB:
                case TileType.Gentle4UpLeftC:
                case TileType.Gentle4UpLeftD:
                    {
                        int qi = tile - TileType.Gentle4UpLeftA;
                        // UpLeft rises going left. A is shallowest.
                        // A: surface left=24, right=32. B: left=16, right=24. etc.
                        int qBase = ts - qi * (ts / 4);       // surface Y at right edge
                        int qTop = qBase - ts / 4;            // surface Y at left edge
                        if (row < qTop) continue;
                        if (row >= qBase)
                        {
                            lineX = wx;
                            lineW = ts;
                        }
                        else
                        {
                            // Fill from left: x = (row - qTop) * 4
                            int fillW = (row - qTop) * 4;
                            if (fillW > ts) fillW = ts;
                            if (fillW <= 0) continue;
                            lineX = wx;
                            lineW = fillW;
                        }
                    }
                    break;
                case TileType.Gentle4CeilRightA:
                case TileType.Gentle4CeilRightB:
                case TileType.Gentle4CeilRightC:
                case TileType.Gentle4CeilRightD:
                    {
                        int qi = tile - TileType.Gentle4CeilRightA;
                        // CeilRight: A is shallowest (near ceiling). Solid above surface.
                        // A: surface left=0, right=8. B: left=8, right=16. etc.
                        int surfLeft = qi * (ts / 4);
                        int surfRight = surfLeft + ts / 4;
                        if (row > surfRight) continue;
                        if (row <= surfLeft)
                        {
                            lineX = wx;
                            lineW = ts;
                        }
                        else
                        {
                            int sx = (row - surfLeft) * 4;
                            if (sx > ts) sx = ts;
                            lineX = wx + sx;
                            lineW = ts - sx;
                            if (lineW <= 0) continue;
                        }
                    }
                    break;
                case TileType.Gentle4CeilLeftA:
                case TileType.Gentle4CeilLeftB:
                case TileType.Gentle4CeilLeftC:
                case TileType.Gentle4CeilLeftD:
                    {
                        int qi = tile - TileType.Gentle4CeilLeftA;
                        // CeilLeft: descends going left. A is shallowest.
                        // Solid hangs from top-right. Surface at right = qi*ts/4, left = (qi+1)*ts/4
                        int surfRight = qi * (ts / 4);
                        int surfLeft = surfRight + ts / 4;
                        if (row > surfLeft) continue;
                        if (row <= surfRight)
                        {
                            lineX = wx;
                            lineW = ts;
                        }
                        else
                        {
                            // Fill from right side
                            int sx = (row - surfRight) * 4;
                            if (sx > ts) sx = ts;
                            lineX = wx;
                            lineW = ts - sx;
                            if (lineW <= 0) continue;
                        }
                    }
                    break;
                default:
                    return;
            }
            _spriteBatch.Draw(_pixel, new Rectangle(lineX, lineY, lineW, 1), color);
        }
    }

    /// <summary>Draw a spike tile (triangular spikes pointing in a direction).</summary>
    private void DrawSpikeTile(int wx, int wy, int ts, TileType tile, Color color)
    {
        bool isHalf = tile >= TileType.HalfSpikesUp && tile <= TileType.HalfSpikesRight;
        bool up = tile == TileType.Spikes || tile == TileType.HalfSpikesUp;
        bool down = tile == TileType.SpikesDown || tile == TileType.HalfSpikesDown;
        bool left = tile == TileType.SpikesLeft || tile == TileType.HalfSpikesLeft;
        bool right = tile == TileType.SpikesRight || tile == TileType.HalfSpikesRight;
        
        int n = 4; // number of spikes
        
        if (up || down)
        {
            int h = isHalf ? ts / 2 : ts; // half spikes = half height
            int sw = ts / n;
            // Base is flush with the wall: UP base at bottom, DOWN base at top
            int oy = 0;
            if (up) oy = ts - h; // UP: base at bottom of tile, tips point up
            // DOWN: oy=0, base at top of tile, tips point down
            
            for (int s = 0; s < n; s++)
            {
                int tipX = wx + s * sw + sw / 2;
                for (int row = 0; row < h; row++)
                {
                    float tipRatio;
                    if (up)
                        tipRatio = 1f - (float)row / (h - 1); // row 0=tip, row h-1=base
                    else
                        tipRatio = (float)row / (h - 1); // row 0=base, row h-1=tip
                    
                    float widthRatio = 1f - tipRatio;
                    int halfW = Math.Max(0, (int)(sw / 2f * widthRatio));
                    if (halfW > 0)
                        _spriteBatch.Draw(_pixel, new Rectangle(tipX - halfW, wy + oy + row, halfW * 2, 1), color);
                    else if (widthRatio > 0f)
                        _spriteBatch.Draw(_pixel, new Rectangle(tipX, wy + oy + row, 1, 1), color);
                }
            }
        }
        else // left or right
        {
            int w = isHalf ? ts / 2 : ts; // half spikes = half width
            int sh = ts / n;
            // Base is flush with the wall: RIGHT base at left, LEFT base at right
            int ox = 0;
            if (left) ox = ts - w; // LEFT: base at right edge, tips point left
            // RIGHT: ox=0, base at left edge, tips point right
            
            for (int s = 0; s < n; s++)
            {
                int tipY = wy + s * sh + sh / 2;
                for (int col = 0; col < w; col++)
                {
                    float tipRatio;
                    if (right)
                        tipRatio = (float)col / (w - 1); // col 0=base, col w-1=tip
                    else // left
                        tipRatio = 1f - (float)col / (w - 1); // col 0=tip, col w-1=base
                    
                    float heightRatio = 1f - tipRatio;
                    int halfH = Math.Max(0, (int)(sh / 2f * heightRatio));
                    if (halfH > 0)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ox + col, tipY - halfH, 1, halfH * 2), color);
                    else if (heightRatio > 0f)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ox + col, tipY, 1, 1), color);
                }
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        bool isDebugLevel = _editorSaveFile.Contains("debug", StringComparison.OrdinalIgnoreCase);
        GraphicsDevice.Clear(isDebugLevel ? new Color(25, 10, 35) : new Color(20, 20, 20));

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

        if (_gameState == GameState.SimMode)
        {
            DrawSimMode();
            base.Draw(gameTime);
            return;
        }

        if (_gameState == GameState.Title)
        {
            _spriteBatch.Begin();

            // Title
            string title = "Genesys";
            var titleSize = _fontLarge.MeasureString(title);
            float cx = ViewW / 2f;
            float cy = ViewH / 2f;
            _spriteBatch.DrawString(_fontLarge, title, new Vector2(cx - titleSize.X / 2, cy - 130), Color.White);

            // Subtitle
            // No subtitle for now

            // Menu options
            float startY = cy - 30;
            float lineH = 35;
            for (int i = 0; i < _titleOptions.Length; i++)
            {
                bool selected = i == _titleCursor;
                string prefix = selected ? "> " : "  ";
                var color = selected ? Color.Yellow : Color.Gray;
                var text = $"{prefix}{_titleOptions[i]}";
                var size = _font.MeasureString(text);
                _spriteBatch.DrawString(_font, text, new Vector2(cx - size.X / 2, startY + i * lineH), color);
            }

            var hintText = "[W/S] Navigate  [Space/Enter] Select";
            var hintSize = _fontSmall.MeasureString(hintText);
            _spriteBatch.DrawString(_fontSmall, hintText, new Vector2(cx - hintSize.X / 2, ViewH - 120), Color.Gray * 0.4f);

            // Settings overlay on title screen
            if (_menuOpen && _settingsFromTitle)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), new Color(20, 20, 20));
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

        // Draw background tiles BEFORE floor/platforms so they appear behind everything
        if (_level.TileGridInstance != null)
        {
            var tg = _level.TileGridInstance;
            var camInvBg = Matrix.Invert(_camera.TransformMatrix);
            var tlBg = Vector2.Transform(Vector2.Zero, camInvBg);
            var brBg = Vector2.Transform(new Vector2(ViewW, ViewH), camInvBg);
            int stxBg = Math.Max(0, ((int)tlBg.X - tg.OriginX) / tg.TileSize - 1);
            int styBg = Math.Max(0, ((int)tlBg.Y - tg.OriginY) / tg.TileSize - 1);
            int etxBg = Math.Min(tg.Width, ((int)brBg.X - tg.OriginX) / tg.TileSize + 2);
            int etyBg = Math.Min(tg.Height, ((int)brBg.Y - tg.OriginY) / tg.TileSize + 2);

            for (int ty = styBg; ty < etyBg; ty++)
            {
                for (int tx = stxBg; tx < etxBg; tx++)
                {
                    var tile = tg.Tiles[tx, ty];
                    if (tile == TileType.Empty || !TileProperties.IsBackground(tile)) continue;
                    int wx = tg.OriginX + tx * tg.TileSize;
                    int wy = tg.OriginY + ty * tg.TileSize;
                    var color = TileProperties.GetColor(tile);
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, tg.TileSize), color);
                    var accent = TileProperties.GetAccentColor(tile);
                    if (accent != Color.Transparent)
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + 2, wy + 2, tg.TileSize - 4, 3), accent);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + 4, wy + tg.TileSize - 6, tg.TileSize - 8, 3), accent);
                    }
                }
            }
        }

        _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, floorH), isDebugLevel ? new Color(40, 20, 55) : new Color(40, 40, 40));
        _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, 2), isDebugLevel ? Color.White * 0.6f : new Color(80, 80, 80));

        // Draw platforms
        foreach (var plat in _level.PlatformRects)
        {
            // Skip platforms fully covered by solid foreground tiles
            if (_level.TileGridInstance != null)
            {
                var tg = _level.TileGridInstance;
                var (ttx, tty) = tg.WorldToTile(plat.X, plat.Y);
                if (ttx >= 0 && tty >= 0 && TileProperties.IsSolid(tg.GetTileAt(ttx, tty)))
                    continue;
            }
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
            // Skip solid floors covered by solid foreground tiles
            if (_level.TileGridInstance != null)
            {
                var tg = _level.TileGridInstance;
                var (ttx, tty) = tg.WorldToTile(sf.X, sf.Y);
                if (ttx >= 0 && tty >= 0 && TileProperties.IsSolid(tg.GetTileAt(ttx, tty)))
                    continue;
            }
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

        // Draw tile grid (gameplay) — after all legacy geometry so tile colors aren't covered
        if (_level.TileGridInstance != null)
        {
            var tg = _level.TileGridInstance;
            var camInvG = Matrix.Invert(_camera.TransformMatrix);
            var tlG = Vector2.Transform(Vector2.Zero, camInvG);
            var brG = Vector2.Transform(new Vector2(ViewW, ViewH), camInvG);
            int stx = Math.Max(0, ((int)tlG.X - tg.OriginX) / tg.TileSize - 1);
            int sty = Math.Max(0, ((int)tlG.Y - tg.OriginY) / tg.TileSize - 1);
            int etx = Math.Min(tg.Width, ((int)brG.X - tg.OriginX) / tg.TileSize + 2);
            int ety = Math.Min(tg.Height, ((int)brG.Y - tg.OriginY) / tg.TileSize + 2);

            for (int ty = sty; ty < ety; ty++)
            {
                for (int tx = stx; tx < etx; tx++)
                {
                    var tile = tg.Tiles[tx, ty];
                    if (tile == TileType.Empty) continue;
                    if (TileProperties.IsBackground(tile)) continue;
                    int wx = tg.OriginX + tx * tg.TileSize;
                    int wy = tg.OriginY + ty * tg.TileSize;
                    var color = TileProperties.GetColor(tile);

                    if (tile == TileType.PlatformTop || tile == TileType.PlatformBottom)
                    {
                        int ts2 = tg.TileSize;
                        int halfH = ts2 / 2;
                        int py = tile == TileType.PlatformBottom ? wy + halfH : wy;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py, ts2, halfH), color);
                        var dark2 = new Color((int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py, ts2, 1), dark2);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, py + halfH - 1, ts2, 1), dark2);
                    }
                    else if (TileProperties.IsPlatform(tile))
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 4), color);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 2), new Color(90, 90, 90));
                    }
                    else if (TileProperties.IsHazard(tile))
                    {
                        DrawSpikeTile(wx, wy, tg.TileSize, tile, color);
                    }
                    else if (TileProperties.IsSlope(tile))
                    {
                        DrawSlopeTile(wx, wy, tg.TileSize, tile, isDebugLevel ? new Color(40, 20, 55) : color);
                        if (isDebugLevel)
                        {
                            var outline = Color.White * 0.6f;
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 1), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + tg.TileSize - 1, tg.TileSize, 1), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, tg.TileSize), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + tg.TileSize - 1, wy, 1, tg.TileSize), outline);
                        }
                    }
                    else
                    {
                        var tileColor = isDebugLevel ? new Color(40, 20, 55) : color;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, tg.TileSize), tileColor);
                        if (!isDebugLevel && tile == TileType.Grass)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 4), TileProperties.GetAccentColor(tile));
                        if (!isDebugLevel)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 1), Color.White * 0.1f);
                        if (isDebugLevel)
                        {
                            var outline = Color.White * 0.6f;
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 1), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + tg.TileSize - 1, tg.TileSize, 1), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, tg.TileSize), outline);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + tg.TileSize - 1, wy, 1, tg.TileSize), outline);
                        }
                    }

                    var dark = new Color(
                        (int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));
                    int ts = tg.TileSize;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), dark);
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), dark);
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), dark);
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), dark);
                }
            }
        }

        // Draw ropes (gameplay)
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

        // Draw item pickups (gameplay)
        for (int i = 0; i < _itemPickups.Count; i++)
        {
            var item = _itemPickups[i];
            if (!item.Collected)
            {
                var itemColor = item.ItemType switch
                {
                    "stick" => new Color(139, 90, 43),
                    "sling" => Color.DarkKhaki,
                    "sword" => Color.Silver,
                    "axe" => Color.DarkGray,
                    "bow" => new Color(160, 120, 60),
                    "gun" => Color.SlateGray,
                    "heart" => Color.Red,
                    _ => Color.White
                };
                // Draw with glow so it's visible
                _spriteBatch.Draw(_pixel, new Rectangle(item.Rect.X - 2, item.Rect.Y - 2, item.Rect.Width + 4, item.Rect.Height + 4), Color.White * 0.25f);
                _spriteBatch.Draw(_pixel, item.Rect, itemColor);
                // Heart items: no pickup prompt, auto-collect on touch
                if (item.ItemType == "heart") continue;
                // Draw "[W]" prompt above when player is near (X and Y)
                var pCenter = _player.Position.X + Player.Width / 2f;
                var pCenterY = _player.Position.Y + Player.Height / 2f;
                var iCenter = item.X + item.W / 2f;
                var iCenterY = item.Y + item.H / 2f;
                if (MathF.Abs(pCenter - iCenter) < 60f && MathF.Abs(pCenterY - iCenterY) < 60f)
                {
                    string prompt = SafeText("[W] Pick up");
                    var promptSize = _font.MeasureString(prompt);
                    _spriteBatch.DrawString(_font, prompt, new Vector2(iCenter - promptSize.X / 2f, item.Y - 20), Color.White * 0.8f);
                }
            }
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

        // Draw background layer of insect swarms (behind trees)
        if (_enemiesEnabled)
            foreach (var swarm in _swarms) swarm.DrawBackground(_spriteBatch, _pixel);

        // Draw environment objects (trees etc)
        foreach (var obj in _level.Objects)
        {
            if (obj.Type == "tree")
            {
                int trunkW = obj.W / 3;
                int trunkH = obj.H / 2;
                int trunkX = (int)(obj.X + obj.W / 2f - trunkW / 2f);
                int trunkY = (int)(obj.Y + obj.H - trunkH);
                _spriteBatch.Draw(_pixel, new Rectangle(trunkX, trunkY, trunkW, trunkH), new Color(101, 67, 33));

                int canopySize = obj.W;
                int canopyX = (int)obj.X;
                int canopyY = (int)obj.Y;
                _spriteBatch.Draw(_pixel, new Rectangle(canopyX - 8, canopyY + 4, canopySize + 16, canopySize - 8), Color.DarkGreen * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle(canopyX - 4, canopyY, canopySize + 8, canopySize), Color.ForestGreen * 0.8f);
                _spriteBatch.Draw(_pixel, new Rectangle(canopyX, canopyY + 4, canopySize, canopySize - 8), Color.Green * 0.7f);
            }
        }

        // Draw enemies (swarms, crawlers, thornbacks)
        if (_enemiesEnabled)
        {
            foreach (var swarm in _swarms) swarm.Draw(_spriteBatch, _pixel);
            foreach (var c in _crawlers) c.Draw(_spriteBatch, _pixel);
            foreach (var h in _hoppers) h.Draw(_spriteBatch, _pixel);
            foreach (var t in _thornbacks) t.Draw(_spriteBatch, _pixel);
            foreach (var bird in _birds) bird.Draw(_spriteBatch, _pixel);
        }

        // Draw player
        if (!_isDead)
        {
            bool visible = _spawnInvincibility <= 0f || MathF.Sin(_spawnInvincibility * 20f) > 0;
            if (visible)
                _player.Draw(_spriteBatch, _pixel);
                // Effect overlays
                if (_player.SpeedBoostTimer > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height), Color.Lime * 0.2f);
                if (_player.FloatTimer > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height), Color.MediumPurple * 0.25f);
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

        _spriteBatch.End();

        // --- UI rendering (no camera transform, screen-space) ---
        _spriteBatch.Begin();

        // Death overlay
        if (_isDead)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.6f);
        }

        // --- Settings menu overlay ---
        if (_menuOpen)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), new Color(20, 20, 20));
            DrawSettingsMenu();
        }
        else if (_inventoryOpen)
        {
            DrawInventory();
        }
        else if (_spawnMenuOpen)
        {
            DrawSpawnMenu();
        }
        else if (!_isDead)
        {
            // Minimal HUD
            _spriteBatch.DrawString(_font, "[Esc] Menu", new Vector2(10, 10), Color.Gray * 0.5f);

            // Health bar (top-left)
            int hpBarW = 200, hpBarH = 12;
            int hpBarX = 10, hpBarY = 30;
            float hpPct = _player.Hp / (float)_player.MaxHp;
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, hpBarY, hpBarW, hpBarH), Color.DarkRed * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, hpBarY, (int)(hpBarW * hpPct), hpBarH),
                hpPct > 0.5f ? Color.LimeGreen : (hpPct > 0.25f ? Color.Yellow : Color.Red));
            DrawHollowRect(hpBarX, hpBarY, hpBarW, hpBarH, Color.White * 0.3f);
            _spriteBatch.DrawString(_font, SafeText($"HP {_player.Hp}/{_player.MaxHp}"), new Vector2(hpBarX + hpBarW + 8, hpBarY - 2), Color.White * 0.7f);

            // Weapon HUD
            {
                string rangedName = CurrentRanged != WeaponType.None ? CurrentRanged.ToString() : "---";
                string meleeName = CurrentMelee != WeaponType.None ? CurrentMelee.ToString() : "Fists";
                _spriteBatch.DrawString(_font, SafeText($"[1] {rangedName}"), new Vector2(10, ViewH - 30), Color.White * 0.7f);
                _spriteBatch.DrawString(_font, SafeText($"[2] {meleeName}"), new Vector2(130, ViewH - 30), Color.White * 0.7f);
            }
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
                DrawWrappedText(_font, npc.Dialogue[_dialogueLine], new Vector2(70, 490), Color.White, 660f);
                if (_dialogueLine < npc.Dialogue.Length - 1)
                    _spriteBatch.DrawString(_font, "[W/Space]", new Vector2(670, 548), Color.Gray * 0.6f);
                else
                    _spriteBatch.DrawString(_font, "[End]", new Vector2(695, 548), Color.Gray * 0.6f);
            }
        }

        _spriteBatch.End();

        // Room transition overlay
        if (_transitionActive && _transitionAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * _transitionAlpha);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }
}
