using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.IO;
using FontStashSharp;

namespace Genesis;

public enum WeaponType { None, Knife, Stick, Whip, Dagger, Sword, GreatSword, Axe, Club, GreatClub, Hammer, Sling, Bow, Gun, WoodShield }

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

public enum GameState { Prologue, TitleCard, Title, Playing, Editing, Overworld, SimMode }

public class Game1 : Game
{
    private GameState _gameState = GameState.Title;
    private int _titleCursor;
    private bool _settingsFromTitle;
    private string[] _titleOptions => SaveData.Exists()
        ? new[] { "Continue", "New Game", "Settings", "Quit" }
        : new[] { "New Game", "Settings", "Quit" };

    // Title screen "The Descent" animation
    private float _titleTime;           // total elapsed time on title screen
    private float _titleScrollY;        // vertical scroll position (increases = descending)
    private Random _titleRng = new Random(42); // deterministic seed for consistent visuals
    private struct TitleStar { public float X, Y, Brightness, Speed; }
    private TitleStar[] _titleStars;
    private struct TitleCloud { public float X, Y, W, H; public float Alpha; }
    private TitleCloud[] _titleClouds;
    private struct TitleTree { public float X, BaseY, Height, Width; public Color Trunk, Canopy; }
    private TitleTree[] _titleTrees;

    // === PROLOGUE / TITLE CARD STATE ===
    private int _prologuePhase;        // 0-3: Ship, Override, Descent, Eye
    private float _prologueTimer;      // time within current phase
    private float _prologueFadeAlpha;  // for fades between phases
    private bool _prologueSkipHeld;    // ESC hold-to-skip
    private float _prologueSkipTimer;  // 1.0s hold required
    private bool _prologueSkipped;      // true if player skipped prologue (no EVE)
    
    // Wake-up cinematic sequence
    private float _wakeUpTimer;
    private int _wakeUpPhase;          // 0=blackout, 1=eyes opening, 2=look around, 3=control given
    private bool _wakeUpComplete;
    
    // EVE spark particles (during boot-up)
    private struct EveSpark { public float X, Y, VX, VY, Life, MaxLife; public bool IsBlue; }
    private List<EveSpark> _eveSparkParticles = new();
    private float _titleCardTimer;     // 3-second "GENESYS" display
    private float _titleCardFade;      // fade in/out alpha
    private float _tierSwitchFlash;    // flash timer on tier change

    // === SCREEN FADE TRANSITION SYSTEM ===
    private float _fadeAlpha;          // 0=clear, 1=black
    private float _fadeSpeed;          // per-second rate
    private bool _fadingOut;           // true=going to black, false=going to clear
    private GameState? _fadeTargetState; // state to switch to at peak black
    private Action _fadeCallback;      // optional action at peak black

    // === ROOM TRANSITION PAN SYSTEM ===
    private bool _roomTransitioning;
    private float _roomTransTimer;
    private float _roomTransDuration;
    private Vector2 _roomTransCamStart;   // camera position at start of pan
    private Vector2 _roomTransCamEnd;     // camera target position
    private Vector2 _roomTransPlayerStart;
    private Vector2 _roomTransPlayerEnd;
    private string _roomTransDir;         // "left","right","up","down"
    private bool _roomTransFirstVisit;
    private bool _roomTransFlicker;       // tech suppression flicker on this room

    // Prologue phase durations (seconds)
    private static readonly float[] ProloguePhaseDurations = { 6f, 5f, 5f, 4f };

    private OverworldData _overworld;
    private WorldGraph _worldGraph;

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

    // Day-night cycle
    private float _worldTime = 8f; // 0-24 hours, start at 8am
    private const float WorldTimeScale = 1f / 60f; // 1 real second = 1 game minute (full cycle = 24 real minutes)
    private int _worldMapBiomeMenuCursor;
    private int _overworldCursor;
    private string _currentNodeId;
    private const string OverworldPath = "Content/overworld.json";

    private GraphicsDeviceManager _graphics;
    private SaveData _saveData;
    private SpriteBatch _spriteBatch;
    private Camera _camera;
    private Texture2D _pixel;
    private Texture2D[] _parallaxLayers;
    private Texture2D _adamSheet; // player sprite sheet

    // CRT effect
    private RenderTarget2D _crtTarget;
    
    // Lantern & lighting system
    private bool _lanternActive;
    private Texture2D _lightGradient;
    private float _lanternFlicker = 1f;
    private RenderTarget2D _darknessTarget;
    private static readonly BlendState _multiplyBlend = new BlendState
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.Zero,
    };
    private bool _levelIsUnderground;

    // Noise-based water shader system
    private bool _useShaderWater = true;
    private Texture2D _noiseTexA;
    private Texture2D _noiseTexB;
    private byte[] _noiseDataA; // cached grayscale pixels (256x256) for CPU sampling
    private byte[] _noiseDataB;
    private RenderTarget2D _waterSceneTarget; // scene below water for refraction
    private Effect _crtShader;
    private bool _crtEnabled;
    private Texture2D _scanlineTex; // fallback scanline overlay
    private FontSystem _fontSystem;
    private SpriteFontBase _font;
    private SpriteFontBase _fontSmall;
    private SpriteFontBase _fontLarge;
    private Song _bgm;
    private Song _menuBgm;
    private Song _currentSong;
    private Player _player;

    // Hit feedback
    private float _hitStopTimer;
    private float _shakeTimer;
    private float _shakeIntensity;

    // Hit feedback toggles
    private bool _hitStopEnabled = true;
    private bool _screenShakeEnabled = true;
    private bool _enemySquashEnabled = true;
    private bool _knockbackEnabled = true;
    private bool _deathParticlesEnabled = true;
    private bool _dustParticlesEnabled = true;

    // Particles
    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public Color Color;
        public int Size;
        public bool DamagesPlayer; // bombardier spray particles
        public int Damage;         // damage per hit (0 = no damage)
    }
    private List<Particle> _particles = new();

    // Ionized zone arc strikes
    private struct ArcStrike { public Vector2 Start, End; public float Timer; public float MaxTimer; public float[] Offsets; }
    private List<ArcStrike> _arcStrikes = new();
    private float _arcSpawnTimer;

    private struct Splatter
    {
        public Vector2 Position;
        public float Life;
        public Color Color;
    }
    private List<Splatter> _splatters = new();
    private bool _playerWasGrounded;
    private float _playerPrevVelY;
    private bool _playerWasDashing;
    private bool _playerWasGrappleFiring;
    private Random _shakeRng = new();

    private List<Bullet> _bullets;
    private List<InsectSwarm> _swarms = new();
    private List<Crawler> _crawlers = new();
    private List<Hopper> _hoppers = new();
    private List<Thornback> _thornbacks = new();
    private List<Bird> _birds = new();
    private List<Wingbeater> _wingbeaters = new();

    // Unified creature list — ALL creatures go here. Typed lists above are derived views.
    private List<Creature> _creatures = new();

    private Random _rng;

    private bool _isDead;
    private bool _isPaused;
    private float _deathFadeTimer; // fade to black on death
    private float _deathRespawnDelay = 2.5f;
    private bool _respawning;

    // Shelter system
    private bool _nearShelter;
    private ShelterData _currentShelter;
    private float _shelterRestTimer; // hold W timer
    private bool _isResting; // rest animation active
    private float _restFadeAlpha;
    private string _shelterPromptText;

    // Death log (Terraria-style)
    private List<string> _deathLog = new();
    private List<float> _deathLogTimers = new();
    private bool _deathLogEnabled = true;
    private string _lastDamageSource = "Unknown";
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
    private const string DefaultLevel = "Content/levels/crashsite.json";

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
    private bool _enableMusic = true;

    // --- Inventory state ---
    private bool _inventoryOpen;
    private int _equipCursorX; // 0=left hand slots, 1=center (armor), 2=right hand slots
    private int _equipCursorY; // 0=slot1/helmet, 1=slot2/chest, 2=legs
    private bool _equipPickerOpen; // weapon picker sub-list
    private int _equipPickerIndex; // index in weapon picker list
    private int _invCategory; // 0=Equipment, 1=Suit, 2=Tools, 3=Log
    private int _invRightScroll; // scroll index for right panel in non-Equipment categories
    private bool _invFocusSidebar; // true = focus on left sidebar category tabs
    private static readonly string[] _invCategoryNames = { "Equipment", "Suit", "Tools", "Log" };
    // Spawn weapon menu (P key)
    private bool _spawnMenuOpen;
    private int _spawnMenuCursor;
    private static readonly string[] SpawnMenuItems = { "Knife", "Grapple", "Stick", "Whip", "Dagger", "Sword", "GreatSword", "Axe", "Club", "GreatClub", "Hammer", "Gun", "Bow", "Sling", "WoodShield", "Battery", "Heart" };

    // --- Dialogue state ---
    private bool _dialogueOpen;
    private int _dialogueNpcIndex = -1;
    private int _dialogueLine;

    // --- EVE orb ---
    private float _totalTime;

    // Suit/battery systems — values live on Player, constants here
    private float _suitSparkTimer; // visual sparks when suit is low
    private float _hudGlitchTimer; // HUD distortion when suit is low
    private Random _hudGlitchRng = new();
    private const float TechDashBatteryCost = 0.5f;
    private const float GrappleBatteryCost = 0.5f;
    private const float ShieldBatteryCost = 1.0f;
    private const float EveScanL3BatteryCost = 5.0f;
    private const float L3ScanDuration = 4.0f;
    private bool _cipherScanUnlocked;            // story gate — post-Dragon EVE revival
    private bool _isL3Scanning;
    private float _l3ScanTimer;
    private string _l3ScanTarget;
    private Creature _l3ScanTargetCreature;
    private float _l3GlitchIntensity;            // 0-1, drives visual overlay
    private const float BatteryCellRestoreAmount = 20f;

    // EVE status
    private enum EveStatus { Ok, Scanning, Overheat, Offline }
    private EveStatus _eveStatus = EveStatus.Ok;
    private bool _eveOrbActive;
    #pragma warning disable CS0414
    private bool _eveDialogueExhausted; // used later for quest tracking
    #pragma warning restore CS0414
    private string _eveMessage = "";

    // --- EVE Dialogue Log ---
    private struct EveLogEntry { public string Text; public float Timestamp; }
    private List<EveLogEntry> _eveDialogueLog = new();
    private bool _eveLogVisible; // toggled with L key
    private int _eveLogScroll; // scroll offset (0 = bottom/newest)

    // --- EVE Map Projection ---
    private bool _hasMapModule;          // player found the cartographic module
    private bool _eveProjectingMap;      // EVE is on ground projecting mini-map
    private Vector2 _eveMapGroundPos;    // where EVE landed to project
    private float _eveMapTimer;          // animation timer
    private bool _fullscreenMapOpen;     // W pressed near projection → fullscreen hex map
    private Vector2 _fullscreenMapScroll;// scroll offset in fullscreen map
    private string _currentRoomId = "";  // which WorldGraph room the player is in
    private float _eveMessageTimer;
    private void EveAlert(string msg, float duration = 3f, bool ignoresSilence = false)
    {
        if (!ignoresSilence && _eveSilenceTimer > 0) return; // EVE is silent
        _eveMessage = msg;
        _eveMessageTimer = duration;
        _eveDialogueLog.Add(new EveLogEntry { Text = msg, Timestamp = _totalTime });
        if (_eveDialogueLog.Count > 200) _eveDialogueLog.RemoveAt(0);
    }
    /// <summary>Say a line only once per session. Returns false if already said.</summary>
    private bool EveAlertOnce(string msg, float duration = 3f, bool ignoresSilence = false)
    {
        if (_eveSaidLines.Contains(msg)) return false;
        _eveSaidLines.Add(msg);
        EveAlert(msg, duration, ignoresSilence);
        return true;
    }
    private HashSet<string> _eveSaidLines = new();

    /// <summary>Trigger an evolution wave. Unlocks gated species and queues EVE alerts.</summary>
    private void TriggerEvolutionWave(string flag)
    {
        if (_evolutionFlags.Contains(flag)) return;
        _evolutionFlags.Add(flag);
        var newSpecies = CreatureDatabase.GetSpeciesForFlag(flag);
        foreach (var sp in newSpecies)
        {
            if (sp.EvolutionEventText != null)
                EveAlert(sp.EvolutionEventText, 5f, ignoresSilence: true);
        }
    }

    // EVE world position + movement system
    private Vector2 _evePos;           // actual world position
    private Vector2 _eveVel;           // velocity
    private bool _evePosInitialized;
    private SecondOrderDynamics2D _eveSpring; // smooth orbit/scan target
    private enum EveMovementMode { Orbit, FlyTo, Scan, MapProject }
    private EveMovementMode _eveMode = EveMovementMode.Orbit;
    private Vector2 _eveFlyTarget;     // target for FlyTo
    private float _eveFlySpeed = 120f;
    private Action? _eveFlyCallback;   // called when FlyTo arrives
    
    // EVE physics constants
    private const float EveMaxSpeed = 350f;
    private const float EveAccel = 600f;
    private const float EveDrag = 3.5f;
    private const float EveOrbitAccel = 800f;
    private const float EveScanAccel = 500f;

    // EVE visual states
    private enum EveVisualState { Normal, Scared, LowBattery, Damaged, Scanning }
    private EveVisualState _eveVisualState = EveVisualState.Normal;
    private float _eveTraumaTimer;  // shaky orbit after big hits nearby
    private int _evePrevHp;         // track HP changes for trauma detection

    // EVE water droplets
    private struct EveDroplet
    {
        public Vector2 Offset;     // relative to EVE pos
        public Vector2 Velocity;
        public float Life;
        public float Size;
        public bool Dripping;      // true = falling off
    }
    private List<EveDroplet> _eveDroplets = new();
    private bool _eveWasInWater;
    private float _eveDropletCooldown;

    // --- Passive Scan System ---
    private class ScannableObject
    {
        public string Id;
        public Vector2 Position;
        public string ScanL1Text;         // auto-scan (EVE proximity)
        public string ScanL2Text;         // player-triggered (Q key)
        public string ScanL3Text;         // deep scan (hold Q, cipher tier)
        public float ScanCooldown;        // seconds until EVE can re-scan this
        public bool Scanned;              // has been auto-scanned at least once
        public float GlowTimer;           // visual pulse timer when being scanned
    }
    private List<ScannableObject> _scannables = new();
    private HashSet<string> _scanLog = new();  // persistent log of scanned IDs
    private float _passiveScanCooldown;         // global cooldown between auto-scans

    // --- Bestiary ---
    private Bestiary _bestiary = new();
    private float _bestiaryNotifyTimer;
    private string _bestiaryNotifySpecies;
    private float _eveBestiaryTimer;
    private float _eveScanPulseTimer;
    private const float EveObservationInterval = 15f;
    private HashSet<Guid> _recentlyObserved = new();
    private float _recentlyObservedClearTimer;
    private int _bestiarySelectedIndex;
    private int _bestiaryRightScroll;  // pixel scroll offset for right panel text
    private bool _bestiaryFocusRight;  // true = D pressed, scrolling right panel
    private const float PassiveScanRange = 192f; // ~6 tiles
    private const float PassiveScanInterval = 8f; // seconds between auto-scans
    private bool _scanL2Available;               // unlocked after EVE patches scanner

    // --- Kill Tracking + EVE Silence ---
    private Dictionary<string, int> _areaKillCounts = new(); // kills per area
    private int _totalKills;

    // --- Evolution System ---
    private HashSet<string> _evolutionFlags = new(); // tracks which evolution waves have triggered
    private int _passiveCreatureKills;    // specifically passive/fleeing creatures
    private float _eveSilenceTimer;       // EVE won't speak while > 0
    private const float EveSilencePerPassiveKill = 15f; // seconds of silence per passive kill
    
    /// <summary>Command EVE to fly to a world position, then call onArrive.</summary>
    private void EveFlyTo(Vector2 target, float speed = 120f, Action? onArrive = null)
    {
        _eveMode = EveMovementMode.FlyTo;
        _eveFlyTarget = target;
        _eveFlySpeed = speed;
        _eveFlyCallback = onArrive;
    }
    
    /// <summary>Get EVE's current orbit target position (clockwise).</summary>
    private Vector2 EveOrbitPos(Vector2 playerCenter, float time)
    {
        float angle = time * 2f;
        float radius = 30f;
        float xOff = 0f, yOff = 0f;

        switch (_eveVisualState)
        {
            case EveVisualState.Scared:
                // Tighter orbit, hugs close, moves faster, slight tremble
                radius = 16f;
                angle = time * 3.5f;
                xOff = MathF.Sin(time * 17f) * 2f;
                yOff = MathF.Cos(time * 13f) * 2f;
                break;
            case EveVisualState.Damaged:
                // Erratic, jittery — bigger offsets, uneven orbit
                radius = 28f;
                angle = time * 2.2f;
                xOff = MathF.Sin(time * 23f) * 4f + MathF.Sin(time * 7f) * 2f;
                yOff = MathF.Cos(time * 19f) * 3f;
                break;
            case EveVisualState.LowBattery:
                // Slow, droopy — sinks lower, sluggish orbit
                radius = 26f;
                angle = time * 1.2f;
                yOff = 8f + MathF.Sin(time * 0.7f) * 3f; // sags downward
                break;
            case EveVisualState.Scanning:
                // Handled by FlyTo mode, but if orbiting during scan prep
                radius = 30f;
                break;
            default:
                break;
        }

        return playerCenter + new Vector2(MathF.Cos(angle) * radius + xOff, -MathF.Sin(angle) * radius + yOff);
    }
    
    /// <summary>Lissajous health scan — EVE traces a full figure-8 beside Adam (clockwise).</summary>
    private Vector2 EveScanPos(Vector2 playerCenter, float time)
    {
        float t = time * 0.8f;
        float xAmp = 18f;
        float yAmp = 36f;
        float xOffset = 24f;
        return new Vector2(
            playerCenter.X + xOffset + MathF.Sin(t * 2f + MathF.PI * 0.25f) * xAmp,
            playerCenter.Y - MathF.Sin(t) * yAmp); // negative to match orbit rotation
    }
    
    /// <summary>Update EVE position each frame using velocity-based physics.</summary>
    private void UpdateEvePosition(float dt, Vector2 playerCenter)
    {
        if (!_evePosInitialized)
        {
            _evePos = EveOrbitPos(playerCenter, _totalTime);
            _eveVel = Vector2.Zero;
            _eveSpring = new SecondOrderDynamics2D(3f, 0.5f, -0.5f, _evePos);
            _evePosInitialized = true;
        }
        else if (_eveSpring == null)
        {
            _eveSpring = new SecondOrderDynamics2D(3f, 0.5f, -0.5f, _evePos);
        }
        
        // Calculate desired target based on mode
        Vector2 target;
        float accel;
        float drag;
        
        switch (_eveMode)
        {
            case EveMovementMode.Orbit:
                target = _eveSpring.Update(dt, EveOrbitPos(playerCenter, _totalTime));
                accel = EveOrbitAccel;
                drag = EveDrag;
                break;
            case EveMovementMode.Scan:
                target = _eveSpring.Update(dt, EveScanPos(playerCenter, _totalTime));
                accel = EveScanAccel;
                drag = EveDrag * 0.8f; // slightly less drag for smoother scan
                break;
            case EveMovementMode.FlyTo:
            {
                var diff = _eveFlyTarget - _evePos;
                float dist = diff.Length();
                if (dist < 6f && _eveVel.Length() < 20f)
                {
                    // Arrived
                    _evePos = _eveFlyTarget;
                    _eveVel *= 0.5f;
                    var cb = _eveFlyCallback;
                    _eveFlyCallback = null;
                    _eveMode = EveMovementMode.Orbit;
                    cb?.Invoke();
                    return;
                }
                // Accelerate toward target, decelerate near it
                float decelDist = _eveFlySpeed * 0.8f; // start slowing down
                float speedMult = dist < decelDist ? (dist / decelDist) * 0.8f + 0.2f : 1f;
                target = _eveFlyTarget;
                accel = EveAccel * speedMult * (_eveFlySpeed / 120f);
                drag = EveDrag * 0.5f; // less drag during fly-to for momentum
                break;
            }
            default:
                return;
            case EveMovementMode.MapProject:
                // EVE holds position on ground, gentle bob
                _evePos = _eveMapGroundPos + new Vector2(0, MathF.Sin((float)_totalTime * 2f) * 1.5f);
                _eveVel = Vector2.Zero;
                _eveMapTimer += dt;
                return; // skip normal steering
        }
        
        // Steering: accelerate toward target — faster when far, ease near
        var toTarget = target - _evePos;
        float tDist = toTarget.Length();
        if (tDist > 0.5f)
        {
            // Distance-based multiplier: ramps up with distance, eases near target
            float distMult = MathF.Min(3f, 0.3f + tDist / 30f); // 0.3x close, up to 3x far
            var desired = (toTarget / tDist) * accel * distMult;
            _eveVel += desired * dt;
        }
        
        // Distance-based drag: more drag when close (braking), less when far (momentum)
        float distFromPlayer = (playerCenter - _evePos).Length();
        float dynamicDrag = drag * (0.6f + 0.8f / (1f + distFromPlayer / 40f));
        _eveVel *= MathF.Max(0f, 1f - dynamicDrag * dt);
        
        // Clamp speed
        float speed = _eveVel.Length();
        if (speed > EveMaxSpeed)
            _eveVel = (_eveVel / speed) * EveMaxSpeed;
        
        // Integrate
        _evePos += _eveVel * dt;
    }

    // --- Weather system ---
    private bool _weatherRain;
    private bool _weatherStorm;
    private bool _weatherWind;
    private float _windDir = 1f; // 1 = right, -1 = left
    private float _windStrength = 40f;
    private float _lightningTimer;
    private float _lightningFlash;
    private struct RainDrop { public float X, Y, Speed, Length; }
    private List<RainDrop> _rainDrops = new();
    private struct Cloud { public float X, Y, W, H, Speed, Opacity; }
    private List<Cloud> _clouds = new();

    // EVE Scan System
    private Dictionary<string, int> _scanProgress = new(); // legacy — kept for save compat
    private float _scanTimer;
    private const float L2ScanDuration = 2.5f;
    private const float L2ScanBatteryCost = 3f;
    private const float L2ScanRange = 200f;
    private bool _isScanning;
    private string _scanTarget;           // species ID of scan target
    private Vector2 _scanTargetPos;
    private Creature _scanTargetCreature; // actual creature ref for tracking + interrupt
    private float _scanPulseTimer;
    private float _scanRevealTimer;
    private string _scanRevealText;

    // Ecosystem interaction timers
    private float _birdHuntTimer;
    private float _swarmDamageTimer;

    // --- Weapon system ---


    // Left Hand / Right Hand weapon system
    private List<WeaponType> _weaponInventory = new();
    private WeaponType _rightHand1 = WeaponType.None; // R1 - active right
    private WeaponType _rightHand2 = WeaponType.None; // R2 - backup right
    private WeaponType _leftHand1 = WeaponType.None;  // L1 - active left
    private WeaponType _leftHand2 = WeaponType.None;  // L2 - backup left
    private bool _rightActiveSlot1 = true;  // true = R1 active, false = R2 active
    private bool _leftActiveSlot1 = true;   // true = L1 active, false = L2 active

    private WeaponType ActiveRight => _rightActiveSlot1 ? _rightHand1 : _rightHand2;
    private WeaponType ActiveLeft => _leftActiveSlot1 ? _leftHand1 : _leftHand2;

    // --- Sidearm ammo system ---
    private const int SidearmClipSize = 12;
    private const float SidearmReloadTime = 1.2f; // seconds
    private const float SidearmFireCooldown = 0.15f; // seconds between shots
    private int _sidearmRounds = 12; // current rounds in clip
    private int _sidearmClips = -1; // -1 = infinite
    private float _sidearmReloadTimer;
    private float _sidearmFireTimer;
    private bool _sidearmReloading;
    // Muzzle flash
    private float _muzzleFlashTimer;
    private Vector2 _muzzleFlashPos;
    private Vector2 _muzzleFlashDir;



    private List<ItemPickup> _itemPickups = new();
    private HashSet<(int col, int row, TileType origType)> _destroyedBreakables = new();
    private HashSet<string> _activatedSwitches = new();
    private float _nextLatchDelay; // stagger delay between consecutive latches
    private const int MaxLatched = 4; // max crawlers latched at once

    // --- Editor state ---
    private enum EditorTool { SolidFloor = 0, Platform = 1, Rope = 2, Wall = 3, Spike = 4, Exit = 5, Spawn = 6, WallSpike = 7, OverworldExit = 8, Ceiling = 9, TilePaint = 10, Enemy = 11, Item = 12, EnvRegion = 13 }
    // Wall climbSide values: 0=both, 1=right face, -1=left face, 99=no climb (solid only)
    private EditorTool _editorTool = EditorTool.Platform;
    private bool _toolPaletteOpen;
    private Vector2 _editorCursor; // world position
    private bool _editorGridSnap = true;
    private bool _editorShowGrid = true;
    private int _editorGridSize = 32;
    private bool _editorDragging;
    private Vector2 _editorDragStart;
    private bool _editorMenuOpen;
    private int _editorMenuCursor;
    private MouseState _prevMouse;

    // Environmental region gameplay state
    private float _envHpDrainAccum; // fractional HP damage accumulator
    private string _currentEnvType = "neutral"; // current region type the player is in
    private string _editorSaveFile = "Content/levels/crashsite.json";
    private string _editorStatusMsg = "";
    private float _editorStatusTimer;

    // Exit target search overlay
    private bool _exitSearchOpen;
    private string _exitSearchQuery = "";
    private string[] _exitSearchResults = Array.Empty<string>();
    private int _exitSearchCursor;
    private int _exitSearchExitIndex = -1; // which exit we're editing
    private enum ExitSearchMode { TargetLevel, TargetExitId, ExitId }
    private ExitSearchMode _exitSearchMode;
    // Entity drag-move state
    private object _editorMovingEntity; // reference to the entity being moved (EnemySpawnData, EnvObjectData, NpcData, ItemData)
    private Vector2 _editorMoveOffset; // offset from entity origin to grab point

    // Drag-fill state
    private bool _dragFilling;
    private int _dragFillStartCol, _dragFillStartRow;
    private int _dragFillEndCol, _dragFillEndRow;
    private bool _dragErasing; // Ctrl+right-drag mass erase
    private int _dragEraseStartCol, _dragEraseStartRow;
    private int _dragEraseEndCol, _dragEraseEndRow;

    // Undo system (tile changes)
    private readonly List<List<(int col, int row, TileType oldTile, TileType newTile)>> _undoStack = new();
    private List<(int col, int row, TileType oldTile, TileType newTile)> _currentUndoBatch;
    private const int MaxUndoSteps = 50;

    // Entity delete undo stack (stores JSON snapshots of level arrays before deletion)
    private readonly List<(string type, int index, object data)> _entityUndoStack = new();

    // Enemy/item editor placement — accordion structure
    private static readonly (string category, string[] variants)[] EnemyCategories = {
        ("Crawler", new[] { "forager", "skitter", "leaper", "bombardier" }),
        ("Evolved", new[] { "stalker", "spitter", "mimic", "resonant" }),
        ("Wingbeater", new[] { "wingbeater" }),
        ("Bird", new[] { "bird" }),
        ("Dummy", new[] { "dummy", "crit-dummy" }),
        ("Swarm", new[] { "swarm" }),
    };
    private int _enemyCategoryCursor;
    private int _enemyVariantCursor;
    private bool _enemyVariantExpanded; // Right arrow expands variants
    private string SelectedEnemyType => EnemyCategories[_enemyCategoryCursor].variants[_enemyVariantCursor];
    private int _editorEnemyCursor; // legacy, unused now
    private static readonly string[] ItemTypes = { "knife", "grapple", "stick", "dagger", "sword", "axe", "club", "hammer", "greatsword", "greatclub", "whip", "sling", "bow", "gun", "heart", "battery" };
    private int _editorItemCursor;

    // Item placement palette (P key in editor)
    private static readonly string[] ItemPaletteTypes = { "knife", "grapple", "stick", "dagger", "sword", "axe", "club", "hammer", "greatsword", "greatclub", "whip", "sling", "bow", "gun", "heart", "battery", "shelter" };
    private bool _itemPaletteOpen;
    private int _itemPaletteCursor;
    private bool _entityPaletteOpen;
    private int _entityPaletteCursor;
    private enum EntityType { Swarm, Crawler, Thornback, Hopper, Tree, Bird, Dummy, CritDummy, Wingbeater }

    // Tile paint state
    private int _tilePaletteCursor;
    private TileType _selectedTileType = TileType.Dirt;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false; // hidden in gameplay, shown in editor
        Window.Title = "Genesys";
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        _audioSettings = new SettingEntry[]
        {
            new() { Label = "Music", Get = () => _enableMusic, Toggle = () => { _enableMusic = !_enableMusic; if (_enableMusic) { PlaySong(_gameState == GameState.Title ? _menuBgm : _bgm); } else { MediaPlayer.Stop(); _currentSong = null; } } },
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
            new() { Label = "EVE Orb", Get = () => _eveOrbActive, Toggle = () => _eveOrbActive = !_eveOrbActive },
            new() { Label = "Hit Stop", Get = () => _hitStopEnabled, Toggle = () => _hitStopEnabled = !_hitStopEnabled },
            new() { Label = "Screen Shake", Get = () => _screenShakeEnabled, Toggle = () => _screenShakeEnabled = !_screenShakeEnabled },
            new() { Label = "Enemy Squash", Get = () => _enemySquashEnabled, Toggle = () => _enemySquashEnabled = !_enemySquashEnabled },
            new() { Label = "Knockback", Get = () => _knockbackEnabled, Toggle = () => _knockbackEnabled = !_knockbackEnabled },
            new() { Label = "Death Particles", Get = () => _deathParticlesEnabled, Toggle = () => _deathParticlesEnabled = !_deathParticlesEnabled },
            new() { Label = "Dust Particles", Get = () => _dustParticlesEnabled, Toggle = () => _dustParticlesEnabled = !_dustParticlesEnabled },
            new() { Label = "Death Log", Get = () => _deathLogEnabled, Toggle = () => _deathLogEnabled = !_deathLogEnabled },
            new() { Label = "Shader Water", Get = () => _useShaderWater, Toggle = () => _useShaderWater = !_useShaderWater },
        };

        _graphicsSettings = new SettingEntry[]
        {
            new() { Label = "Window Size", Get = () => true, Toggle = () => ApplyWindowSize(_windowSizeIndex + 1) },
            new() { Label = "CRT Filter", Get = () => _crtEnabled, Toggle = () => { _crtEnabled = !_crtEnabled; var s = _saveData ?? SaveData.Load(); if (s != null) { s.CrtEnabled = _crtEnabled; s.Save(); } } },
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
        if (tempSave != null)
            _crtEnabled = tempSave.CrtEnabled;

        if (System.IO.File.Exists(OverworldPath))
            _overworld = OverworldData.Load(OverworldPath);
        else
            _overworld = new OverworldData();
        _currentNodeId = _overworld.StartNode;
        
        // World graph — interconnected room structure for creature sim, weather, pathing
        _worldGraph = WorldGraph.Load();

        base.Initialize();

        Window.TextInput += OnTextInput;
    }

    private void LoadLevel(string path)
    {
        _level = LevelData.Load(path);
        _levelIsUnderground = _level.IsUnderground;
        Player.WorldLeft = _level.Bounds.Left;
        Player.WorldRight = _level.Bounds.Right;
        Player.HasLeftNeighbor = _level.Neighbors?.HasNeighbor("left") == true;
        Player.HasRightNeighbor = _level.Neighbors?.HasNeighbor("right") == true;
        Player.HasUpNeighbor = _level.Neighbors?.HasNeighbor("up") == true;
        Player.HasDownNeighbor = _level.Neighbors?.HasNeighbor("down") == true;

        // Clear enemies (SpawnEnemiesFromLevel re-populates if needed)
        _swarms.Clear();
        _creatures.Clear();
        _crawlers.Clear();
        _hoppers.Clear();
        _thornbacks.Clear();
        _birds.Clear(); _wingbeaters.Clear();

        // Load item pickups
        _itemPickups.Clear();
        _destroyedBreakables.Clear();
        _activatedSwitches.Clear();
        _weatherRain = false; _weatherStorm = false; _weatherWind = false;
        _rainDrops.Clear(); _clouds.Clear(); _lightningFlash = 0;
        foreach (var item in _level.Items)
        {
            bool alreadyCollected = _saveData?.CollectedItems?.Contains(item.Id) == true;
            _itemPickups.Add(new ItemPickup { Id = item.Id, X = item.X, Y = item.Y, W = item.W, H = item.H, ItemType = item.Type, Collected = alreadyCollected });
        }

        // Enemies are spawned in SpawnEnemiesFromLevel(), called by Restart()

        // Track current room in WorldGraph
        string levelName = System.IO.Path.GetFileNameWithoutExtension(path);
        var matchRoom = _worldGraph?.Rooms.FirstOrDefault(r => r.LevelFile == levelName);
        if (matchRoom != null)
        {
            _currentRoomId = matchRoom.Id;
            matchRoom.Visited = true;
            matchRoom.Discovered = true;
            // Discover adjacent rooms
            foreach (var exit in matchRoom.Exits)
            {
                var adj = _worldGraph.GetRoom(exit.TargetRoomId);
                if (adj != null) adj.Discovered = true;
            }
        }

        // Load scannable objects for this level
        _scannables.Clear();
        LoadScannables(levelName);
    }

    private void LoadScannables(string levelName)
    {
        // Scannable objects per level — defined in code for now, JSON later
        // L1 = short tag (passive, auto-scan). L2/L3 = full dialogue (player-initiated).
        switch (levelName)
        {
            case "test-arena":
                _scannables.Add(new ScannableObject { Id = "crash-debris-1", Position = new Vector2(300, 420),
                    ScanL1Text = "[Hull fragment]",
                    ScanL2Text = "Thermal scoring consistent with atmospheric entry. Alloy stress fractures suggest... Adam, this impact should have been terminal." });
                _scannables.Add(new ScannableObject { Id = "alien-plant-1", Position = new Vector2(500, 400),
                    ScanL1Text = "[Bioluminescent flora]",
                    ScanL2Text = "Not photosynthesis — chemosynthesis. Root network extends underground, interconnected. Nutrient sharing across specimens. This is one organism." });
                _scannables.Add(new ScannableObject { Id = "thermal-vent-1", Position = new Vector2(800, 430),
                    ScanL1Text = "[Thermal signature]",
                    ScanL2Text = "Subsurface heat source. Not volcanic — too regular, too distributed. Something is circulating energy beneath us." });
                _scannables.Add(new ScannableObject { Id = "adapted-marking-1", Position = new Vector2(1200, 380),
                    ScanL1Text = "[Surface markings]",
                    ScanL2Text = "Repeating pattern. Not natural erosion. Tool marks — deliberate. Someone made this." });
                _scannables.Add(new ScannableObject { Id = "chitin-fragment-1", Position = new Vector2(1600, 410),
                    ScanL1Text = "[Organic compound]",
                    ScanL2Text = "Chitin derivative — exoskeletal. Growth rings indicate recent shedding. This was alive. Recently." });
                break;
            case "debug-room":
                _scannables.Add(new ScannableObject { Id = "test-scan-1", Position = new Vector2(400, 400),
                    ScanL1Text = "[Test object]",
                    ScanL2Text = "EVE scan system test. If you're reading this, it works." });
                break;
        }
    }

    private void RespawnItemsFromLevel()
    {
        _itemPickups.Clear();
        foreach (var item in _level.Items)
        {
            bool alreadyCollected = _saveData?.CollectedItems?.Contains(item.Id) == true;
            _itemPickups.Add(new ItemPickup { Id = item.Id, X = item.X, Y = item.Y, W = item.W, H = item.H, ItemType = item.Type, Collected = alreadyCollected });
        }
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
        _crtTarget = new RenderTarget2D(GraphicsDevice, ViewW, ViewH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        _waterSceneTarget = new RenderTarget2D(GraphicsDevice, ViewW, ViewH);
        // Persist window size
        var saveForPersist = _saveData ?? SaveData.Load();
        if (saveForPersist != null) { saveForPersist.WindowSizeIndex = _windowSizeIndex; saveForPersist.Save(); }
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
        
        // Restore persistent state from save after creating new Player
        if (_saveData != null)
        {
            _player.CurrentTier = (Player.MoveTier)Math.Clamp(_saveData.MoveTier, 0, 2);
            _player.ApplyTierConstants();
            _player.HasGrapple = _saveData.CollectedItems?.Any(id => id.StartsWith("grapple")) == true;
            _player.SuitIntegrity = _saveData.SuitIntegrity;
            _player.Battery = _saveData.Battery;
            _player.Hp = Math.Clamp(_saveData.Hp, 1, _player.MaxHp);
            LoadInventoryFromSave();
        }
        
        SpawnEnemiesFromLevel();
    }

    private void SpawnEnemiesFromLevel()
    {
        _swarms.Clear();
        _creatures.Clear();
        _crawlers.Clear();
        _hoppers.Clear();
        _thornbacks.Clear();
        _birds.Clear(); _wingbeaters.Clear();
        if (_rng == null) _rng = new Random();

        var tg = _level.TileGridInstance;
        int ts = _level.TileGrid?.TileSize ?? 32;
        var plats = _level.AllPlatforms;
        var sFloors = _level.SolidFloorRects;
        var walls = _level.WallRects;
        float mainFloor = _level.Floor.Y;
        float bLeft = _level.Bounds.Left;
        float bRight = _level.Bounds.Right;

        foreach (var e in _level.Enemies)
        {
            // Evolution gate: skip species not yet unlocked
            if (CreatureDatabase.Profiles.TryGetValue(e.Type, out var profile)
                && profile.EvolutionGateFlag != null
                && !_evolutionFlags.Contains(profile.EvolutionGateFlag))
                continue;

            switch (e.Type)
            {
                case "swarm":
                    _swarms.Add(new InsectSwarm(new Vector2(e.X, e.Y), e.Count > 0 ? e.Count : 10, _rng));
                    break;
                case "crawler":
                case "forager":
                case "skitter":
                case "leaper":
                case "bombardier":
                case "stalker":
                case "spitter":
                case "mimic":
                case "resonant":
                    float snapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Crawler.Width, Crawler.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var pushOut = EnemyPhysics.PushOutOfSolid(e.X, snapY, Crawler.Width, Crawler.Height, tg, ts);
                    var c = new Crawler(new Vector2(pushOut.X, pushOut.Y), bLeft, bRight, 0, 0, _rng);
                    c.Frozen = e.Frozen;
                    c.Variant = e.Type switch
                    {
                        "leaper" => CrawlerVariant.Leaper,
                        "skitter" => CrawlerVariant.Skitter,
                        "bombardier" => CrawlerVariant.Bombardier,
                        "stalker" => CrawlerVariant.Stalker,
                        "spitter" => CrawlerVariant.Spitter,
                        "mimic" => CrawlerVariant.Mimic,
                        "resonant" => CrawlerVariant.Resonant,
                        _ => CrawlerVariant.Forager,
                    };
                    c.ApplyVariantRole();
                    c.UpdateSurfaceEdges(tg, ts, plats, sFloors, bLeft, bRight);
                    _crawlers.Add(c); _creatures.Add(c);
                    break;
                case "thornback":
                    float tSnapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Thornback.Width, Thornback.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var tb = new Thornback(new Vector2(e.X, tSnapY));
                    _thornbacks.Add(tb); _creatures.Add(tb);
                    break;
                case "hopper":
                    float hSnapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Hopper.Width, Hopper.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var hop = new Hopper(new Vector2(e.X, hSnapY), hSnapY + Hopper.Height);
                    _hoppers.Add(hop); _creatures.Add(hop);
                    break;
                case "bird":
                    float bSnapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Bird.Width, Bird.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var bird = new Bird(new Vector2(e.X, bSnapY), 0, 0, _rng);
                    bird.UpdateSurfaceEdges(tg, ts, plats, sFloors, bLeft, bRight);
                    _birds.Add(bird); _creatures.Add(bird);
                    break;
                case "wingbeater":
                    var wb = new Wingbeater(new Vector2(e.X, e.Y));
                    wb.Passive = e.Passive;
                    _wingbeaters.Add(wb); _creatures.Add(wb);
                    break;
                case "dummy":
                    float dSnapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Crawler.Width, Crawler.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var dummy = new Crawler(new Vector2(e.X, dSnapY), e.X - 10, e.X + 10, 0, 0);
                    dummy.IsDummy = true;
                    dummy.Hp = 9999;
                    dummy.DummyScale = e.Scale;
                    dummy.DummyScaleX = e.ScaleX;
                    dummy.DummyScaleY = e.ScaleY;
                    dummy.Position.Y -= (dummy.EffectiveHeight - Crawler.Height);
                    dummy.SetSpawnPos(dummy.Position);
                    dummy.UpdateSurfaceEdges(tg, ts, plats, sFloors, bLeft, bRight);
                    _crawlers.Add(dummy); _creatures.Add(dummy);
                    break;
                case "crit-dummy":
                    float cdSnapY = EnemyPhysics.SnapToSurface(e.X, e.Y, Crawler.Width, Crawler.Height, tg, ts, plats, sFloors, walls, mainFloor);
                    var critDummy = new Crawler(new Vector2(e.X, cdSnapY), e.X - 10, e.X + 10, 0, 0);
                    critDummy.IsDummy = true;
                    critDummy.AlwaysCrit = true;
                    critDummy.Hp = 9999;
                    critDummy.DummyScale = e.Scale;
                    critDummy.Position.Y -= (critDummy.EffectiveHeight - Crawler.Height);
                    critDummy.SetSpawnPos(critDummy.Position);
                    critDummy.UpdateSurfaceEdges(tg, ts, plats, sFloors, bLeft, bRight);
                    _crawlers.Add(critDummy); _creatures.Add(critDummy);
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
        
        // Generate radial gradient for light system
        {
            int sz = 128;
            _lightGradient = new Texture2D(GraphicsDevice, sz, sz);
            var ld = new Color[sz * sz];
            float half = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = MathHelper.Clamp(1f - dist, 0f, 1f);
                    // Linear falloff, premultiplied alpha for SpriteBatch compatibility
                    int alpha = (int)(a * 255);
                    int rgb = (int)(a * 255); // premultiplied: color channels = white * alpha
                    ld[y * sz + x] = new Color(rgb, rgb, rgb, alpha);
                }
            _lightGradient.SetData(ld);
        }

        // Generate noise textures for water shader
        _noiseTexA = GenerateNoiseTexture(256, 256, 42, 32f);
        _noiseTexB = GenerateNoiseTexture(256, 256, 137, 48f);
        // Cache noise pixel data for fast CPU-side sampling
        var colorsA = new Color[256 * 256];
        _noiseTexA.GetData(colorsA);
        _noiseDataA = new byte[256 * 256];
        for (int i = 0; i < colorsA.Length; i++) _noiseDataA[i] = colorsA[i].R;
        var colorsB = new Color[256 * 256];
        _noiseTexB.GetData(colorsB);
        _noiseDataB = new byte[256 * 256];
        for (int i = 0; i < colorsB.Length; i++) _noiseDataB[i] = colorsB[i].R;
        _waterSceneTarget = new RenderTarget2D(GraphicsDevice, ViewW, ViewH);

        // Load parallax background layers (DISABLED — backgrounds too small)
        // var layers = new List<Texture2D>();
        // string bgDir = "Content/backgrounds/Nature Landscapes Free Pixel Art/nature_5";
        // for (int i = 1; i <= 5; i++)
        // {
        //     string path = Path.Combine(bgDir, $"{i}.png");
        //     if (File.Exists(path)) { using var fs = File.OpenRead(path); layers.Add(Texture2D.FromStream(GraphicsDevice, fs)); }
        // }
        _parallaxLayers = Array.Empty<Texture2D>();

        // Load player sprite sheet (prefer richter, fall back to adam)
        string[] sheetPaths = { "Content/sprites/richter_sheet.png", "Content/sprites/adam_sheet.png" };
        foreach (var sp in sheetPaths)
        {
            if (File.Exists(sp))
            {
                using var fs = File.OpenRead(sp);
                _adamSheet = Texture2D.FromStream(GraphicsDevice, fs);
                break;
            }
        }

        // CRT setup: render target + scanline overlay texture
        _crtTarget = new RenderTarget2D(GraphicsDevice, ViewW, ViewH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        // Try loading compiled CRT shader
        if (File.Exists("Content/shaders/CRT.xnb"))
        {
            try { _crtShader = Content.Load<Effect>("shaders/CRT"); } catch { _crtShader = null; }
        }
        // Build scanline overlay texture (fallback when no shader)
        _scanlineTex = new Texture2D(GraphicsDevice, 1, 4);
        _scanlineTex.SetData(new[] { Color.Transparent, new Color(0, 0, 0, 60), new Color(0, 0, 0, 80), Color.Transparent });
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(File.ReadAllBytes("Content/Fonts/main.ttf"));
        _font = _fontSystem.GetFont(12);       // main text (was 16, too large for Press Start 2P)
        _fontSmall = _fontSystem.GetFont(9);   // small UI hints
        _fontLarge = _fontSystem.GetFont(22);  // titles, boss names
        try
        {
            var musicPath = Path.GetFullPath("Content/audio/music/overworld.ogg");
            _bgm = Song.FromUri("overworld", new Uri(musicPath));
        }
        catch (Exception ex) { Console.WriteLine($"[MUSIC] overworld FAILED: {ex}"); }
        try
        {
            // MonoGame DesktopGL only supports OGG (NVorbis). Convert MP3→OGG if needed.
            var menuOgg = Path.GetFullPath("Content/audio/music/menu.ogg");
            var menuMp3 = Path.GetFullPath("Content/audio/music/Glitch - Prehistory (main menu).mp3");
            var menuPath = File.Exists(menuOgg) ? menuOgg : (File.Exists(menuMp3) ? menuMp3 : null);
            if (menuPath != null)
                _menuBgm = Song.FromUri("menu", new Uri(menuPath));
        }
        catch (Exception ex) { Console.WriteLine($"[MUSIC] menu FAILED: {ex.Message}. Convert to OGG: ffmpeg -i 'Glitch - Prehistory (main menu).mp3' menu.ogg"); }
        // Start with menu music
        PlaySong(_menuBgm);
        GenerateTitleScene();
    }

    private void PlaySong(Song song)
    {
        if (song == null || !_enableMusic) return;
        if (_currentSong == song && MediaPlayer.State == MediaState.Playing) return;
        try
        {
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = 0.5f;
            MediaPlayer.Play(song);
            _currentSong = song;
        }
        catch { }
    }

    private Vector2 PlayerCenter =>
        _player.Position + new Vector2(Player.Width / 2f, 
            _player.IsCrouching ? Player.Height - Player.CrouchHeight / 2f : Player.Height / 2f);

    private GameState _prevGameState;
    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Advance world time (only during gameplay, not editor)
        if (_gameState == GameState.Playing)
        {
            _worldTime += dt * WorldTimeScale;
            if (_worldTime >= 24f) _worldTime -= 24f;
            
            // Lantern battery drain
            if (_lanternActive)
            {
                _player.Battery -= 0.5f * dt;
                if (_player.Battery <= 0f)
                {
                    _player.Battery = 0f;
                    _lanternActive = false;
                    EveAlert("Lantern offline. Battery depleted.");
                }
            }
        }

        // Music switching on state change
        if (_gameState != _prevGameState)
        {
            if (_gameState == GameState.Title)
                PlaySong(_menuBgm);
            else if (_gameState == GameState.Playing || _gameState == GameState.Editing)
                PlaySong(_bgm);
            _prevGameState = _gameState;
        }

        // Fade music volume with screen fade (smooth transition)
        if (_enableMusic && _fadeAlpha > 0f)
            MediaPlayer.Volume = Math.Max(0f, 0.5f * (1f - _fadeAlpha));
        else if (_enableMusic && MediaPlayer.Volume < 0.5f)
            MediaPlayer.Volume = Math.Min(0.5f, MediaPlayer.Volume + dt * 0.5f); // fade back in over ~1s

        // === FADE TRANSITION UPDATE ===
        if (_fadingOut)
        {
            _fadeAlpha = Math.Min(1f, _fadeAlpha + _fadeSpeed * dt);
            if (_fadeAlpha >= 1f)
            {
                _fadingOut = false;
                if (_fadeTargetState.HasValue)
                {
                    _gameState = _fadeTargetState.Value;
                    _fadeTargetState = null;
                }
                _fadeCallback?.Invoke();
                _fadeCallback = null;
            }
        }
        else if (_fadeAlpha > 0f)
        {
            _fadeAlpha = Math.Max(0f, _fadeAlpha - _fadeSpeed * dt);
        }

        // === PROLOGUE UPDATE ===
        if (_gameState == GameState.Prologue)
        {
            UpdatePrologue(kb, dt);
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

        // === TITLE CARD UPDATE ===
        if (_gameState == GameState.TitleCard)
        {
            UpdateTitleCard(dt);
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

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
            UpdateTitleAnimation(dt);
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
                            StartFadeTo(GameState.Playing, 1.0f, () =>
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
                                _rng = new Random();
                                SpawnEnemiesFromLevel();
                                _player = new Player(new Microsoft.Xna.Framework.Vector2(_saveData.SpawnX, _saveData.SpawnY));
                                _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                                _bullets = new List<Bullet>();
                                _prevInExit = new bool[_level.ExitRects.Length];
                                for (int k = 0; k < _prevInExit.Length; k++)
                                    _prevInExit[k] = true;
                                if (_saveData.Flags.ContainsKey("eveOrbActive"))
                                    _eveOrbActive = _saveData.Flags["eveOrbActive"];
                                LoadInventoryFromSave();
                                _bestiary = _saveData.Bestiary ?? new Bestiary();
                                _evolutionFlags = _saveData.EvolutionFlags ?? new HashSet<string>();
                                _cipherScanUnlocked = _saveData.CipherScanUnlocked;
                                _wakeUpComplete = true;
                                _player.IsLyingDown = false;
                                _player.HasGrapple = _saveData?.CollectedItems?.Any(id => id.StartsWith("grapple")) == true;
                                _hasMapModule = _saveData?.CollectedItems?.Any(id => id.StartsWith("map-module")) == true;
                                _camera.Zoom = 1f;
                                _camera.TargetZoom = 1f;
                                _player.CurrentTier = (Player.MoveTier)Math.Clamp(_saveData.MoveTier, 0, 2);
                                _player.ApplyTierConstants();
                                _player.SuitIntegrity = _saveData.SuitIntegrity;
                                _player.Battery = _saveData.Battery;
                                _player.Hp = _saveData.Hp;
                                _worldTime = _saveData.WorldTime;
                            });
                        }
                        break;
                    case "New Game":
                        SaveData.Delete();
                        _saveData = new SaveData();
                        _eveOrbActive = false;
                        _evePosInitialized = false;
                        _eveSpring = null;
                        _wakeUpComplete = false;
                        _wakeUpPhase = 0;
                        _wakeUpTimer = 0f;
                        _eveMode = EveMovementMode.Orbit;
                        _eveMessageTimer = 0f;
                        _eveMessage = "";
                        _eveSilenceTimer = 0f;
                        _eveDialogueLog.Clear();
                        _eveProjectingMap = false;
                        _fullscreenMapOpen = false;
                        _eveLogVisible = false;
                        _scannables.Clear();
                        _scanLog.Clear();
                        _bestiary = new Bestiary();
                        _evolutionFlags = new HashSet<string>();
                        _cipherScanUnlocked = false;
                        _totalKills = 0;
                        _passiveCreatureKills = 0;
                        _areaKillCounts.Clear();
                        _player.SuitIntegrity = 31f;
                        _player.Battery = 80f;
                        _hasMapModule = false;
                        _prologueSkipped = false;
                        _eveDialogueExhausted = false;
                        ClearInventory();
                        StartFadeTo(GameState.Prologue, 1.0f, () =>
                        {
                            _prologuePhase = 0;
                            _prologueTimer = 0f;
                            _prologueFadeAlpha = 1f;
                            _prologueSkipTimer = 0f;
                        });
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
            IsMouseVisible = true;
            _prevKb = kb;
            _prevMouse = Mouse.GetState();
            return;
        }

        // Toggle menu with Escape (only open — closing is handled inside UpdateMenu)
        if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape) && !_menuOpen && !_inventoryOpen)
        {
            // Close any map state first
            _fullscreenMapOpen = false;
            if (_eveProjectingMap) { _eveProjectingMap = false; _eveMode = EveMovementMode.Orbit; }
            _menuOpen = true;
            _settingsCategoryCursor = 0;
            _settingsActiveCategory = null;
            _settingsItemCursor = 0;
            _prevKb = kb; // consume the Escape press so UpdateMenu doesn't immediately close
        }

        // L key — toggle EVE dialogue log
        if (kb.IsKeyDown(Keys.L) && _prevKb.IsKeyUp(Keys.L) && !_menuOpen)
        {
            _eveLogVisible = !_eveLogVisible;
            _eveLogScroll = 0; // reset scroll on toggle
        }
        // Scroll EVE log with W/S (or Up/Down) when open
        if (_eveLogVisible && _eveDialogueLog.Count > 0)
        {
            if ((kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W)) || (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up)))
                _eveLogScroll = Math.Min(_eveLogScroll + 1, Math.Max(0, _eveDialogueLog.Count - 12));
            if ((kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S)) || (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down)))
                _eveLogScroll = Math.Max(0, _eveLogScroll - 1);
        }
        // M key — EVE map projection toggle
        if (kb.IsKeyDown(Keys.M) && _prevKb.IsKeyUp(Keys.M) && !_menuOpen && !_dialogueOpen && _eveOrbActive)
        {
            if (_fullscreenMapOpen)
            {
                // Close fullscreen map → return to projection
                _fullscreenMapOpen = false;
            }
            else if (_eveProjectingMap)
            {
                // Cancel projection → EVE returns to orbit
                _eveProjectingMap = false;
                _eveMode = EveMovementMode.Orbit;
            }
            else if (_hasMapModule)
            {
                // Start projection: EVE flies to ground near player
                _eveProjectingMap = true;
                _eveMapTimer = 0f;
                _fullscreenMapOpen = false;
                float groundY = SnapToSurface(_player.Position.X + Player.Width / 2f, _player.Position.Y, 4, 0);
                _eveMapGroundPos = new Vector2(_player.Position.X + Player.Width / 2f + _player.FacingDir * 40f, groundY - 8f);
                EveFlyTo(_eveMapGroundPos, 200f, () => { _eveMode = EveMovementMode.MapProject; });
            }
            else
            {
                EveAlert("No cartographic module installed.", 2f);
            }
        }

        // W key near map projection → fullscreen
        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W) && _eveProjectingMap && _eveMode == EveMovementMode.MapProject && !_fullscreenMapOpen)
        {
            var dist = Vector2.Distance(new Vector2(_player.Position.X + Player.Width / 2f, _player.Position.Y), _eveMapGroundPos);
            if (dist < 80f)
                _fullscreenMapOpen = true;
        }

        // Fullscreen map blocks gameplay input, handles scroll
        if (_fullscreenMapOpen)
        {
            // Escape or M closes the map
            if ((kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape)) ||
                (kb.IsKeyDown(Keys.M) && _prevKb.IsKeyUp(Keys.M)))
            {
                _fullscreenMapOpen = false;
                _eveProjectingMap = false;
                _eveMode = EveMovementMode.Orbit;
                _prevKb = kb;
                return;
            }
            // Backtick from map → debug room
            if (kb.IsKeyDown(Keys.OemTilde) && _prevKb.IsKeyUp(Keys.OemTilde))
            {
                _fullscreenMapOpen = false;
                _eveProjectingMap = false;
                _eveMode = EveMovementMode.Orbit;
                LoadLevel("Content/levels/debug-room.json");
                _editorSaveFile = "Content/levels/debug-room.json";
                _camera = MakeCamera();
                Restart();
                _gameState = GameState.Playing;
                _prevKb = kb;
                return;
            }
            float scrollSpeed = 3f;
            if (kb.IsKeyDown(Keys.W)) _fullscreenMapScroll.Y += scrollSpeed;
            if (kb.IsKeyDown(Keys.S)) _fullscreenMapScroll.Y -= scrollSpeed;
            if (kb.IsKeyDown(Keys.A)) _fullscreenMapScroll.X += scrollSpeed;
            if (kb.IsKeyDown(Keys.D)) _fullscreenMapScroll.X -= scrollSpeed;
            _prevKb = kb;
            return;
        }

        if (_menuOpen)
        {
            UpdateMenu(kb);
            _prevKb = kb;
            return; // game is paused while menu is open
        }

        // Lantern toggle with N
        if (kb.IsKeyDown(Keys.N) && _prevKb.IsKeyUp(Keys.N) && !_menuOpen && !_dialogueOpen && !_inventoryOpen)
        {
            if (!_lanternActive && _player.Battery > 0)
            {
                _lanternActive = true;
                EveAlertOnce("Lantern online. Battery drain minimal.");
            }
            else
                _lanternActive = false;
        }

        // Toggle inventory with Tab or I
        bool invToggle = (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab)) ||
                         (kb.IsKeyDown(Keys.I) && _prevKb.IsKeyUp(Keys.I));
        if (invToggle && !_dialogueOpen)
        {
            _inventoryOpen = !_inventoryOpen;
            if (_inventoryOpen) { _equipCursorX = 0; _equipCursorY = 0; _equipPickerOpen = false; _equipPickerIndex = 0; _invCategory = 0; _invRightScroll = 0; _invFocusSidebar = true; }
        }

        if (_inventoryOpen)
        {
            if (!invToggle) // skip UpdateInventory on the frame we just opened (prevents same-frame close)
                UpdateInventory(kb);
            _prevKb = kb;
            base.Update(gameTime);
            return;
        }

        if (_isDead)
        {
            _deathFadeTimer += dt;
            if (_deathFadeTimer >= _deathRespawnDelay && !_respawning)
            {
                _respawning = true;
                // Respawn at last shelter or level spawn
                var save = _saveData ?? SaveData.Load();
                if (save != null)
                {
                    save.DeathCount++;
                    save.Save();
                    if (!string.IsNullOrEmpty(save.ShelterLevel))
                    {
                        // Respawn at shelter
                        _player.Position = new Microsoft.Xna.Framework.Vector2(save.ShelterX, save.ShelterY);
                        _player.Hp = _player.MaxHp;
                    }
                    else
                    {
                        _player.Position = new Microsoft.Xna.Framework.Vector2(save.SpawnX, save.SpawnY);
                        _player.Hp = _player.MaxHp;
                    }
                }
                else
                {
                    _player.Hp = _player.MaxHp;
                }
                _isDead = false;
                _deathFadeTimer = 0;
                _respawning = false;
                _spawnInvincibility = 2f;
                // Shift weather on death (consequence)
                // Force weather shift on death (consequence)
                _weatherRain = !_weatherRain;
                if (!_weatherRain) _weatherStorm = false;
                _weatherWind = _rng.NextDouble() > 0.5;
                // TODO: shuffle enemy patrol patterns
                if (_eveOrbActive && save?.DeathCount == 1)
                    EveAlert("Your vitals flatlined for... 11 seconds. The local microbiome appears to have... intervened.", 5f);
                else if (_eveOrbActive)
                    EveAlert("Biosignatures stabilizing. Try to be more careful.", 3f);
            }
            _prevKb = kb;
            _prevMouse = Mouse.GetState();
            base.Update(gameTime);
            return;
        }

        _totalTime += dt;
        if (_eveMessageTimer > 0) _eveMessageTimer -= dt;
        if (_bestiaryNotifyTimer > 0) _bestiaryNotifyTimer -= dt;
        if (_eveSilenceTimer > 0) _eveSilenceTimer -= dt;
        if (_passiveScanCooldown > 0) _passiveScanCooldown -= dt;
        
        // --- EVE Passive Scan ---
        if (_eveOrbActive && _passiveScanCooldown <= 0 && _eveMode == EveMovementMode.Orbit && _wakeUpComplete)
        {
            var playerCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            ScannableObject nearest = null;
            float nearestDist = PassiveScanRange;
            foreach (var sc in _scannables)
            {
                if (sc.Scanned || sc.ScanCooldown > 0 || string.IsNullOrEmpty(sc.ScanL1Text)) continue;
                float dist = Vector2.Distance(playerCenter, sc.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = sc;
                }
            }
            if (nearest != null)
            {
                nearest.Scanned = true;
                nearest.GlowTimer = 1.5f;
                nearest.ScanCooldown = 30f; // don't re-scan for 30s
                _passiveScanCooldown = PassiveScanInterval;
                _scanLog.Add(nearest.Id);
                EveAlert(nearest.ScanL1Text, 4f);
            }
        }

        // --- EVE Bestiary Passive Observation ---
        _eveBestiaryTimer -= dt;
        if (_eveScanPulseTimer > 0) _eveScanPulseTimer -= dt;
        _recentlyObservedClearTimer -= dt;
        if (_recentlyObservedClearTimer <= 0) { _recentlyObserved.Clear(); _recentlyObservedClearTimer = 60f; }
        if (_eveOrbActive && _eveBestiaryTimer <= 0 && _wakeUpComplete)
        {
            _eveBestiaryTimer = EveObservationInterval;
            var scanCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            string alertMsg = null;
            foreach (var creature in _creatures)
            {
                if (!creature.Alive || _recentlyObserved.Contains(creature.Id)) continue;
                float dist = Vector2.Distance(scanCenter, creature.Position);
                if (dist > PassiveScanRange) continue;

                _recentlyObserved.Add(creature.Id);

                // Determine behavior
                float speed = creature.Velocity.Length();
                string behavior;
                if (speed > 50 && Vector2.Dot(creature.Velocity, scanCenter - creature.Position) > 0)
                    behavior = "aggressive";
                else if (speed > 30)
                    behavior = "fleeing";
                else if (speed < 10 && creature.Needs.Hunger > 0.7f)
                    behavior = "foraging";
                else if (speed < 5)
                    behavior = "idle";
                else
                    behavior = "moving";

                string timeOfDay = GetTimeOfDay();
                string weather = _currentEnvType switch
                {
                    "cold" => "cold",
                    "hot" => "hot",
                    "toxic" => "toxic",
                    _ => "clear"
                };
                string biome = _level?.Name ?? "unknown";
                string classification = creature.Role switch
                {
                    EcologicalRole.Herbivore => "herbivore",
                    EcologicalRole.Predator => "predator",
                    EcologicalRole.Scavenger => "scavenger",
                    EcologicalRole.Decomposer => "decomposer",
                    EcologicalRole.Prey => "prey species",
                    EcologicalRole.Defensive => "defensive",
                    EcologicalRole.Flighty => "skittish",
                    _ => "unknown"
                };

                bool isNew = !_bestiary.Entries.ContainsKey(creature.SpeciesName);
                bool added = _bestiary.LogSighting(creature.SpeciesName, classification, timeOfDay, weather, behavior, biome);

                if (added && alertMsg == null) // one alert per interval
                {
                    var entry = _bestiary.Entries[creature.SpeciesName];
                    // Check for progressive L1 reveal text from CreatureDatabase
                    string revealText = CreatureDatabase.GetL1RevealText(creature.SpeciesName, entry.SightCount);
                    if (revealText != null)
                    {
                        // Short spoken quip with personality — full text in bestiary
                        string quip = CreatureDatabase.GetSpokenQuip(creature.SpeciesName, entry.SightCount);
                        alertMsg = quip ?? "Noted.";
                        // Trigger bestiary notification
                        _bestiaryNotifyTimer = 3f;
                        _bestiaryNotifySpecies = creature.SpeciesName;
                    }
                    else if (isNew && _bestiary.Entries.Count == 1)
                        alertMsg = "Unknown species. Logging.";
                    else if (isNew)
                        alertMsg = $"New contact — {classification}. Documenting.";
                    else if (entry.Observations.Count >= 7 && entry.Observations.Count <= 8)
                        alertMsg = $"{creature.SpeciesName} — basic profile complete.";
                    else
                        alertMsg = $"Sighted {creature.SpeciesName} again. {behavior}.";
                }
            }
            if (alertMsg != null) { EveAlert(alertMsg, 3f); _eveScanPulseTimer = 1.5f; }
        }

        // Tick scannable cooldowns
        foreach (var sc in _scannables)
        {
            if (sc.ScanCooldown > 0) sc.ScanCooldown -= dt;
            if (sc.GlowTimer > 0) sc.GlowTimer -= dt;
        }

        // Update EVE position every frame
        if (_eveOrbActive)
        {
            var pc = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            UpdateEvePosition(dt, pc);

            // EVE visual state determination
            if (_eveTraumaTimer > 0) _eveTraumaTimer -= dt;
            // Detect HP drop → trauma
            if (_player.Hp < _evePrevHp && _evePrevHp > 0)
                _eveTraumaTimer = Math.Max(_eveTraumaTimer, 1.5f + (_evePrevHp - _player.Hp) * 0.5f);
            _evePrevHp = _player.Hp;
            if (_isScanning || _isL3Scanning)
                _eveVisualState = EveVisualState.Scanning;
            else if (_player.Hp <= 3)
                _eveVisualState = EveVisualState.Scared;
            else if (_player.Battery < 10)
                _eveVisualState = EveVisualState.LowBattery;
            else if (_eveTraumaTimer > 0)
                _eveVisualState = EveVisualState.Damaged;
            else
                _eveVisualState = EveVisualState.Normal;

            // EVE water droplets
            // Check if EVE is in water (use tile at her position)
            var evGrid = _level?.TileGridInstance;
            bool eveInWater = false;
            if (evGrid != null)
            {
                int eveTileX = (int)((_evePos.X - evGrid.OriginX) / evGrid.TileSize);
                int eveTileY = (int)((_evePos.Y - evGrid.OriginY) / evGrid.TileSize);
                if (eveTileX >= 0 && eveTileX < evGrid.Width && eveTileY >= 0 && eveTileY < evGrid.Height)
                {
                    var tile = evGrid.Tiles[eveTileX, eveTileY];
                    eveInWater = TileProperties.IsLiquid(tile);
                }
            }

            if (eveInWater && !_eveWasInWater)
            {
                // Just entered water — add droplets all over
                for (int di = 0; di < 8; di++)
                    _eveDroplets.Add(new EveDroplet {
                        Offset = new Vector2(_rng.Next(-5, 6), _rng.Next(-5, 6)),
                        Velocity = Vector2.Zero,
                        Life = 2f + (float)_rng.NextDouble() * 2f,
                        Size = 1f + (float)_rng.NextDouble(),
                        Dripping = false
                    });
            }
            else if (eveInWater)
            {
                // Sustained water — add droplets periodically
                _eveDropletCooldown -= dt;
                if (_eveDropletCooldown <= 0)
                {
                    _eveDropletCooldown = 0.3f;
                    _eveDroplets.Add(new EveDroplet {
                        Offset = new Vector2(_rng.Next(-4, 5), _rng.Next(-4, 5)),
                        Velocity = Vector2.Zero,
                        Life = 1.5f + (float)_rng.NextDouble() * 1.5f,
                        Size = 1f + (float)_rng.NextDouble() * 0.5f,
                        Dripping = false
                    });
                }
            }
            _eveWasInWater = eveInWater;

            // Update droplets — they cling, then drip off with gravity + EVE momentum
            for (int di = _eveDroplets.Count - 1; di >= 0; di--)
            {
                var d = _eveDroplets[di];
                d.Life -= dt;
                if (d.Life <= 0) { _eveDroplets.RemoveAt(di); continue; }

                if (!d.Dripping)
                {
                    // Cling to EVE — start dripping when life gets low or EVE moves fast
                    float eveSpeed = _eveVel.Length();
                    bool shouldDrip = d.Life < 1.2f || eveSpeed > 120f || (float)_rng.NextDouble() < 0.01f;
                    if (shouldDrip)
                    {
                        d.Dripping = true;
                        // Initial velocity = EVE's velocity * small factor + downward
                        d.Velocity = _eveVel * 0.15f + new Vector2(0, 20f);
                        // Convert from relative offset to world offset that will be gravity-affected
                    }
                }

                if (d.Dripping)
                {
                    d.Velocity += new Vector2(0, 180f * dt); // gravity
                    d.Offset += d.Velocity * dt;
                    d.Size = Math.Max(0.5f, d.Size - dt * 0.3f); // shrink as they fall
                }

                _eveDroplets[di] = d;
            }
            
            // Auto-cancel map projection if player walks too far
            if (_eveProjectingMap && _eveMode == EveMovementMode.MapProject)
            {
                float distToProj = Vector2.Distance(pc, _eveMapGroundPos);
                if (distToProj > 200f)
                {
                    _eveProjectingMap = false;
                    _fullscreenMapOpen = false;
                    _eveMode = EveMovementMode.Orbit;
                }
            }
        }

        // Update retractable spike timer
        if (_level.TileGridInstance != null)
            _level.TileGridInstance.RetractTimer += dt;

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

        // Ability gating now handled by Player.ApplyTierConstants()
        _player.EnableDash = _enableDash;
        _player.EnableDropThrough = _enableDropThrough;
        _player.EnableSpinMelee = _enableSpinMelee;

        // Weapon system: set weapon availability and melee range
        var rightWs = WeaponStats.Get(ActiveRight);
        var leftWs = WeaponStats.Get(ActiveLeft);
        _player.RightIsRanged = ActiveRight != WeaponType.None && rightWs.IsRanged;
        _player.RightIsMelee = ActiveRight != WeaponType.None && !rightWs.IsRanged && !rightWs.IsShield;
        _player.RightIsShield = ActiveRight != WeaponType.None && rightWs.IsShield;
        _player.LeftIsRanged = ActiveLeft != WeaponType.None && leftWs.IsRanged;
        _player.LeftIsMelee = (ActiveLeft == WeaponType.None) || (!leftWs.IsRanged && !leftWs.IsShield); // fists count as melee
        _player.LeftIsShield = ActiveLeft != WeaponType.None && leftWs.IsShield;
        _player.HasMeleeWeapon = _player.RightIsMelee || _player.LeftIsMelee;
        _player.HasRangedWeapon = _player.RightIsRanged || _player.LeftIsRanged;
        _player.RightWeapon = ActiveRight;
        _player.LeftWeapon = ActiveLeft;
        // CurrentWeapon is now set per-attack in Player.cs; default to CurrentMelee for range/rate calc
        // Don't override CurrentWeapon here — it's set per-attack in Player.cs
        // to match the hand that actually attacked (RightWeapon or LeftWeapon)
        if (_player.MeleeTimer <= 0) _player.CurrentWeapon = CurrentMelee; // only set default when not attacking
        _player.MeleeRangeOverride = CurrentMelee switch
        {
            WeaponType.None => 28,
            WeaponType.Stick => 30,
            WeaponType.Dagger => 24,
            WeaponType.Whip => 50,
            WeaponType.Sword => 60,
            WeaponType.Axe => 40,
            WeaponType.Club => 35,
            WeaponType.Hammer => 45,
            WeaponType.GreatSword => 70,
            WeaponType.GreatClub => 50,
            _ => Player.MeleeRange
        };

        var currentWs = WeaponStats.Get(CurrentMelee);
        _player.CurrentMeleeRate = currentWs.AttackSpeed;
        _player.CurrentMeleeActiveTime = currentWs.ActiveTime;
        _player.CurrentComboWindow = currentWs.ComboWindow;
        _player.CurrentComboCooldown = currentWs.ComboCooldown;

        // Shield blocking input
        {
            bool hasShieldRight = WeaponStats.Get(ActiveRight).IsShield;
            bool hasShieldLeft = WeaponStats.Get(ActiveLeft).IsShield;
            var kbShield = Keyboard.GetState();
            // J = right hand, K = left hand
            bool holdingShieldButton = (hasShieldRight && (kbShield.IsKeyDown(Keys.J) || Mouse.GetState().LeftButton == ButtonState.Pressed))
                                    || (hasShieldLeft && (kbShield.IsKeyDown(Keys.K) || Mouse.GetState().RightButton == ButtonState.Pressed));
            _player.IsBlocking = holdingShieldButton;
            if (holdingShieldButton)
            {
                _player.BlockFromRight = _player.FacingDir == 1;
                _player.BlockAngle = _player.FacingDir == 1 ? 0f : MathF.PI;
            }

            // Shield knocked away check
            if (_player.ShieldKnockedAway)
            {
                _player.ShieldKnockedAway = false;
                // Unequip shield from whichever hand has it
                if (_rightActiveSlot1 && WeaponStats.Get(_rightHand1).IsShield) _rightHand1 = WeaponType.None;
                else if (!_rightActiveSlot1 && WeaponStats.Get(_rightHand2).IsShield) _rightHand2 = WeaponType.None;
                else if (_leftActiveSlot1 && WeaponStats.Get(_leftHand1).IsShield) _leftHand1 = WeaponType.None;
                else if (!_leftActiveSlot1 && WeaponStats.Get(_leftHand2).IsShield) _leftHand2 = WeaponType.None;
                _player.IsBlocking = false;
            }
        }

        // Weapon cycling (only during gameplay, not editor)
        if (!_menuOpen && !_dialogueOpen && _gameState == GameState.Playing)
        {
            // === Wake-up cinematic sequence ===
            if (!_wakeUpComplete)
            {
                _wakeUpTimer += dt;
                switch (_wakeUpPhase)
                {
                    case 0: // Black screen (0-2s) — zoom stays at 2.5
                        _fadeAlpha = Math.Max(0.7f, 1f - _wakeUpTimer * 0.15f);
                        _camera.Zoom = 2.5f;
                        _camera.TargetZoom = 2.5f;
                        _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                        if (_wakeUpTimer >= 2f) { _wakeUpPhase = 1; _wakeUpTimer = 0; }
                        break;
                    case 1: // Eyes opening (0-3s) — zoom 2.5 → 1.9
                    {
                        float t1 = _wakeUpTimer / 3f; // 0→1
                        float z1 = MathHelper.Lerp(2.5f, 1.9f, t1 * t1); // ease-in
                        _fadeAlpha = Math.Max(0f, 0.7f - _wakeUpTimer * 0.25f);
                        _camera.Zoom = z1;
                        _camera.TargetZoom = z1;
                        _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                        if (_wakeUpTimer >= 3f) { _wakeUpPhase = 2; _wakeUpTimer = 0; }
                        break;
                    }
                    case 2: // EVE arrives + scans (0-8s) — zoom 1.9 → 1.2
                    {
                        float t2 = _wakeUpTimer / 8f;
                        float z2 = MathHelper.Lerp(1.9f, 1.2f, t2);
                        _camera.Zoom = z2;
                        _camera.TargetZoom = z2;
                        _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                        if (_wakeUpTimer >= 1.5f && !_eveOrbActive)
                        {
                            // EVE boots up — spawn at right edge of visible area, fly to scan position
                            _eveOrbActive = true;
                            var pc = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
                            // Visible half-width at current zoom
                            float visHalfW = (ViewW / _camera.Zoom) * 0.5f;
                            _evePos = new Vector2(pc.X + visHalfW + 8f, pc.Y); // just off right edge, same height as Adam
                            _evePosInitialized = true;
                            _eveMode = EveMovementMode.FlyTo;
                            _eveFlyTarget = EveScanPos(pc, _totalTime);
                            _eveFlySpeed = 80f; // steady approach
                            _eveFlyCallback = () => {
                                _eveMode = EveMovementMode.Scan;
                                EveAlertOnce("Sys... systems rebooting. Adam? Can you hear me?", 4f);
                            };
                        }
                        // Adam starts standing up at 4s (after some scanning)
                        if (_wakeUpTimer >= 4f && _player.IsLyingDown && _player.StandUpProgress < 0.01f)
                        {
                            _player.BeginStandUp();
                        }
                        if (_player.IsLyingDown)
                            _player.UpdateStandUp(dt);
                        if (_wakeUpTimer >= 8f)
                        {
                            _wakeUpPhase = 3; _wakeUpTimer = 0;
                            // EVE patches Adam's suit servos
                            _player.CureInjury();
                            EveAlertOnce("Micro-fractures in the suit servos. Compensating.");
                        }
                        break;
                    }
                    case 3: // Control given (0-2s) — zoom 1.2 → 1.0
                    {
                        float t3 = Math.Min(1f, _wakeUpTimer / 2f);
                        float z3 = MathHelper.Lerp(1.2f, 1f, t3);
                        _camera.Zoom = z3;
                        _camera.TargetZoom = z3;
                        _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                        if (_wakeUpTimer >= 2f && !_wakeUpComplete)
                        {
                            _wakeUpComplete = true;
                            // Transition EVE from scan to orbit smoothly
                            var pc3 = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
                            EveFlyTo(EveOrbitPos(pc3, _totalTime), 80f, () => {
                                _eveMode = EveMovementMode.Orbit;
                            });
                            EveAlertOnce("Try to move. Carefully.", 3f);
                            // Save EVE state so Continue remembers her
                            _saveData.Flags["eveOrbActive"] = true;
                            _scanL2Available = true; // EVE's scanner is online
                            SyncInventoryToSave(); _saveData.Save();
                        }
                        // Delayed crash survival hint (5s after control given)
                        if (_wakeUpTimer >= 5f && _wakeUpTimer < 5f + dt * 2f)
                        {
                            EveAlertOnce("Adam... I've lost all telemetry from the descent. Flight recorder is gone.", 5f);
                        }
                        if (_wakeUpTimer >= 11f && _wakeUpTimer < 11f + dt * 2f)
                        {
                            EveAlertOnce("Based on impact analysis... survival probability was 0.003%. Something doesn't add up.", 6f);
                        }
                        break;
                    }
                }
                // Update EVE sparks during wake-up
                if (_eveOrbActive)
                {
                    // Spawn sparks from EVE's position
                    float sparkRate = (_wakeUpPhase == 2) ? 8f : 4f; // more sparks early
                    if (_rng.NextDouble() < sparkRate * dt)
                    {
                        float ex = _evePos.X;
                        float ey = _evePos.Y;
                        float vx = (_rng.NextSingle() - 0.5f) * 80f;
                        float vy = -_rng.NextSingle() * 60f + 20f; // mostly upward/sideways
                        float life = 0.3f + _rng.NextSingle() * 0.5f;
                        _eveSparkParticles.Add(new EveSpark { X = ex, Y = ey, VX = vx, VY = vy, Life = life, MaxLife = life, IsBlue = _rng.NextDouble() > 0.4 });
                    }
                }
                // Update existing sparks
                for (int si = _eveSparkParticles.Count - 1; si >= 0; si--)
                {
                    var sp = _eveSparkParticles[si];
                    sp.X += sp.VX * dt;
                    sp.Y += sp.VY * dt;
                    sp.VY += 120f * dt; // gravity on sparks
                    sp.Life -= dt;
                    if (sp.Life <= 0) { _eveSparkParticles.RemoveAt(si); continue; }
                    _eveSparkParticles[si] = sp;
                }
                _totalTime += dt; // keep _totalTime advancing for orbit
                // Override keyboard to block player input during wake-up
                kb = new KeyboardState();
            }

            // Block player input during room transition pan
            if (_roomTransitioning)
            {
                kb = new KeyboardState();
            }

            // Pause toggle
            if (kb.IsKeyDown(Keys.B) && _prevKb.IsKeyUp(Keys.B))
            {
                _isPaused = !_isPaused;
                _player.Paused = _isPaused;
            }
            if (_isPaused) { _prevKb = kb; return; }

            // Y: Cycle movement tier
            if (kb.IsKeyDown(Keys.Y) && _prevKb.IsKeyUp(Keys.Y))
            {
                _player.CurrentTier = (Player.MoveTier)(((int)_player.CurrentTier + 1) % 3);
                _player.ApplyTierConstants();
                _tierSwitchFlash = 1.0f;
                if (_saveData != null) { SyncInventoryToSave(); _saveData.Save(); }
            }

            if (kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1))
            {
                _rightActiveSlot1 = !_rightActiveSlot1;
            }
            if (kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2))
            {
                _leftActiveSlot1 = !_leftActiveSlot1;
            }

            // --- Manual reload (R key) ---
            if (kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R)
                && CurrentRanged == WeaponType.Gun && !_sidearmReloading
                && _sidearmRounds < SidearmClipSize
                && (_sidearmClips == -1 || _sidearmClips > 0))
            {
                _sidearmReloading = true;
                _sidearmReloadTimer = SidearmReloadTime;
            }

            // --- Spawn weapon menu (P key) ---
            if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P))
            {
                _spawnMenuOpen = !_spawnMenuOpen;
                if (_spawnMenuOpen) _spawnMenuCursor = 0;
            }
            if (_spawnMenuOpen)
            {
                if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                    _spawnMenuCursor = (_spawnMenuCursor - 1 + SpawnMenuItems.Length) % SpawnMenuItems.Length;
                if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                    _spawnMenuCursor = (_spawnMenuCursor + 1) % SpawnMenuItems.Length;
                if (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter))
                {
                    SpawnItemAtPlayer(SpawnMenuItems[_spawnMenuCursor].ToLower());
                    _spawnMenuOpen = false;
                }
                if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                    _spawnMenuOpen = false;
            }
        }

        // --- EVE L2 Active Scan System (Q key) ---
        if (_gameState == GameState.Playing && !_isDead && !_menuOpen && !_dialogueOpen && !_spawnMenuOpen)
        {
            if (kb.IsKeyDown(Keys.Q))
            {
                var pc = PlayerCenter;
                if (!_isScanning && _prevKb.IsKeyUp(Keys.Q))
                {
                    if (!_scanL2Available)
                    {
                        EveAlert("Scanner not online yet.", 2f);
                    }
                    else if (_player.Battery < L2ScanBatteryCost)
                    {
                        EveAlert("Not enough battery to scan.", 2f);
                    }
                    else
                    {
                        // Find nearest alive creature within range
                        float bestDist = L2ScanRange;
                        Creature bestCreature = null;
                        foreach (var cr in _creatures)
                        {
                            if (!cr.Alive) continue;
                            if (cr is Crawler crl && crl.IsDummy) continue;
                            var ep = cr.Position + new Vector2(cr.Rect.Width / 2f, cr.Rect.Height / 2f);
                            float d2 = Vector2.Distance(pc, ep);
                            if (d2 < bestDist) { bestDist = d2; bestCreature = cr; }
                        }
                        if (bestCreature != null)
                        {
                            string speciesId = bestCreature.SpeciesName;
                            // Check if already L2 scanned
                            if (_bestiary.Entries.TryGetValue(speciesId, out var existing) && existing.L2Scanned)
                            {
                                // L2 done — try L3 if cipher unlocked
                                if (_cipherScanUnlocked && !existing.L3Scanned)
                                {
                                    if (_player.Battery < EveScanL3BatteryCost)
                                    {
                                        EveAlert("Not enough battery for deep scan.", 2f);
                                    }
                                    else
                                    {
                                        // Start L3 cipher scan
                                        _player.Battery = Math.Max(0, _player.Battery - EveScanL3BatteryCost);
                                        _isL3Scanning = true;
                                        _l3ScanTimer = L3ScanDuration;
                                        _l3ScanTarget = speciesId;
                                        _l3ScanTargetCreature = bestCreature;
                                        _l3GlitchIntensity = 0f;
                                        _scanTargetPos = bestCreature.Position + new Vector2(bestCreature.Rect.Width / 2f, bestCreature.Rect.Height / 2f);
                                        _eveMode = EveMovementMode.FlyTo;
                                        _eveFlyTarget = _scanTargetPos + new Vector2(0, -20);
                                        EveAlert("Deep scan... opening cipher link.", 2f);
                                    }
                                }
                                else if (_cipherScanUnlocked && existing.L3Scanned)
                                {
                                    EveAlert("Nothing more to learn here.", 2f);
                                }
                                else
                                {
                                    EveAlert("Already have full scan data on this one.", 2f);
                                }
                            }
                            else
                            {
                                // Start L2 scan — battery cost upfront
                                _player.Battery = Math.Max(0, _player.Battery - L2ScanBatteryCost);
                                _isScanning = true;
                                _scanTimer = L2ScanDuration;
                                _scanPulseTimer = 0f;
                                _scanTarget = speciesId;
                                _scanTargetCreature = bestCreature;
                                _scanTargetPos = bestCreature.Position + new Vector2(bestCreature.Rect.Width / 2f, bestCreature.Rect.Height / 2f);
                                // EVE flies to the creature
                                _eveMode = EveMovementMode.FlyTo;
                                _eveFlyTarget = _scanTargetPos + new Vector2(0, -20); // hover above
                                EveAlert("Scanning...", 1.5f);
                            }
                        }
                        else
                        {
                            // Check scannables (static objects) for L2 scan
                            ScannableObject bestScannable = null;
                            float bestScDist = L2ScanRange;
                            foreach (var sc in _scannables)
                            {
                                if (string.IsNullOrEmpty(sc.ScanL2Text)) continue;
                                if (_scanLog.Contains(sc.Id + "-L2")) continue;
                                float d = Vector2.Distance(pc, sc.Position);
                                if (d < bestScDist) { bestScDist = d; bestScannable = sc; }
                            }
                            if (bestScannable != null)
                            {
                                _player.Battery = Math.Max(0, _player.Battery - L2ScanBatteryCost);
                                bestScannable.GlowTimer = 2f;
                                EveAlert(bestScannable.ScanL2Text, 6f);
                                _scanLog.Add(bestScannable.Id + "-L2");
                            }
                            else
                            {
                                EveAlert("Nothing to scan nearby.", 2f);
                            }
                        }
                    }
                }
                else if (_isScanning)
                {
                    // Track target creature position
                    if (_scanTargetCreature == null || !_scanTargetCreature.Alive)
                    {
                        // Target died — scan interrupted
                        _isScanning = false;
                        _eveMode = EveMovementMode.Orbit;
                        EveAlert("Target lost.", 2f);
                    }
                    else
                    {
                        _scanTargetPos = _scanTargetCreature.Position + new Vector2(_scanTargetCreature.Rect.Width / 2f, _scanTargetCreature.Rect.Height / 2f);
                        _eveFlyTarget = _scanTargetPos + new Vector2(0, -20);
                        _scanTimer -= dt;
                        _scanPulseTimer += dt;

                        // EVE vulnerability — check if any creature hits near EVE
                        float eveHitRadius = 12f;
                        foreach (var cr in _creatures)
                        {
                            if (!cr.Alive || cr == _scanTargetCreature) continue;
                            var crCenter = cr.Position + new Vector2(cr.Rect.Width / 2f, cr.Rect.Height / 2f);
                            if (Vector2.Distance(_evePos, crCenter) < eveHitRadius + cr.Rect.Width / 2f)
                            {
                                // Scan interrupted!
                                _isScanning = false;
                                _eveMode = EveMovementMode.Orbit;
                                EveAlert("Scan interrupted! Too dangerous.", 3f);
                                _eveScanPulseTimer = 0f;
                                break;
                            }
                        }

                        // Also check if target creature attacks (aggressive creatures near EVE)
                        if (_isScanning && Vector2.Distance(_evePos, _scanTargetPos) < 16f)
                        {
                            // Close enough — scan particles
                            if (_rng.NextDouble() < 0.5)
                            {
                                float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                                float r = 8f + (float)_rng.NextDouble() * 12f;
                                _particles.Add(new Particle
                                {
                                    Position = _scanTargetPos + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r),
                                    Velocity = new Vector2(MathF.Cos(angle) * -15f, MathF.Sin(angle) * -15f),
                                    Life = 0.6f, MaxLife = 0.6f,
                                    Color = new Color(100, 255, 200), Size = 2
                                });
                            }
                            // Scan beam particles (EVE to target)
                            if (_rng.NextDouble() < 0.3)
                            {
                                float t = (float)_rng.NextDouble();
                                var beamPos = Vector2.Lerp(_evePos, _scanTargetPos, t);
                                _particles.Add(new Particle
                                {
                                    Position = beamPos + new Vector2(_rng.Next(-3, 3), _rng.Next(-3, 3)),
                                    Velocity = Vector2.Zero,
                                    Life = 0.3f, MaxLife = 0.3f,
                                    Color = Color.Cyan * 0.7f, Size = 1
                                });
                            }
                        }

                        // Scan complete
                        if (_isScanning && _scanTimer <= 0)
                        {
                            _isScanning = false;
                            _eveMode = EveMovementMode.Orbit;

                            // Set L2 scanned on bestiary entry
                            if (!_bestiary.Entries.ContainsKey(_scanTarget))
                                _bestiary.LogSighting(_scanTarget, "Unknown", GetTimeOfDay().ToString(), _weatherRain ? "rain" : "clear", "scanned", "unknown");
                            _bestiary.Entries[_scanTarget].L2Scanned = true;

                            // EVE speaks L2 result
                            string displayName = _scanTarget;
                            if (CreatureDatabase.Profiles.TryGetValue(_scanTarget, out var profile))
                            {
                                displayName = profile.DisplayName;
                                // Short spoken L2 summary
                                string l2Quip = profile.Temperament != null
                                    ? $"{displayName}: {profile.Temperament.Split('—')[0].Trim()}"
                                    : $"Full scan: {displayName}";
                                if (l2Quip.Length > 50) l2Quip = l2Quip[..47] + "...";
                                EveAlert(l2Quip, 4f);
                            }
                            else
                            {
                                EveAlert($"Scan complete: {displayName}", 3f);
                            }

                            _scanRevealText = $"[ L2 Scan Complete — {displayName} ]";
                            _scanRevealTimer = 3f;
                            _eveScanPulseTimer = 1.5f;
                            _bestiaryNotifyTimer = 3f;
                            _bestiaryNotifySpecies = _scanTarget;
                        }
                    }
                }
            }
            else if (_isScanning)
            {
                // Released Q — scan interrupted
                _isScanning = false;
                _eveMode = EveMovementMode.Orbit;
                EveAlert("Scan aborted.", 2f);
            }
            else if (_isL3Scanning)
            {
                // Released Q — L3 scan interrupted
                _isL3Scanning = false;
                _l3GlitchIntensity = 0f;
                _eveMode = EveMovementMode.Orbit;
                EveAlert("Cipher link severed.", 2f);
            }
        }

        // L3 scan update (runs independently of Q held — once started, it continues while Q held)
        if (_isL3Scanning)
        {
            if (_l3ScanTargetCreature == null || !_l3ScanTargetCreature.Alive)
            {
                _isL3Scanning = false;
                _l3GlitchIntensity = 0f;
                _eveMode = EveMovementMode.Orbit;
                EveAlert("Connection lost. Subject expired.", 3f);
            }
            else
            {
                _scanTargetPos = _l3ScanTargetCreature.Position + new Vector2(_l3ScanTargetCreature.Rect.Width / 2f, _l3ScanTargetCreature.Rect.Height / 2f);
                _eveFlyTarget = _scanTargetPos + new Vector2(0, -20);
                _l3ScanTimer -= dt;
                _scanPulseTimer += dt;

                // Glitch intensity ramps up over the scan duration
                float progress = 1f - _l3ScanTimer / L3ScanDuration;
                _l3GlitchIntensity = MathHelper.Clamp(progress * 1.2f, 0f, 1f);

                // EVE vulnerability — same as L2
                float eveHitRadius = 12f;
                foreach (var cr in _creatures)
                {
                    if (!cr.Alive || cr == _l3ScanTargetCreature) continue;
                    var crCenter = cr.Position + new Vector2(cr.Rect.Width / 2f, cr.Rect.Height / 2f);
                    if (Vector2.Distance(_evePos, crCenter) < eveHitRadius + cr.Rect.Width / 2f)
                    {
                        _isL3Scanning = false;
                        _l3GlitchIntensity = 0f;
                        _eveMode = EveMovementMode.Orbit;
                        EveAlert("Cipher link disrupted!", 3f);
                        break;
                    }
                }

                // Scan complete
                if (_isL3Scanning && _l3ScanTimer <= 0)
                {
                    _isL3Scanning = false;
                    _eveMode = EveMovementMode.Orbit;

                    // Set L3 scanned
                    if (_bestiary.Entries.TryGetValue(_l3ScanTarget, out var l3entry))
                        l3entry.L3Scanned = true;

                    // Get biome-specific feeling if available
                    string feeling = null;
                    if (CreatureDatabase.Profiles.TryGetValue(_l3ScanTarget, out var l3profile))
                    {
                        // Check biome variant
                        string currentBiome = _level?.Name ?? "unknown";
                        if (l3profile.BiomeFeelingVariants != null && l3profile.BiomeFeelingVariants.TryGetValue(currentBiome, out var biomeFeeling))
                            feeling = biomeFeeling;
                        else
                            feeling = l3profile.CreatureFeeling;

                        EveAlert($"I felt it. {feeling}", 5f);
                    }
                    else
                    {
                        EveAlert("Deep scan complete.", 3f);
                    }

                    _scanRevealText = $"[ Cipher Scan Complete — {_l3ScanTarget} ]";
                    _scanRevealTimer = 3f;
                    _bestiaryNotifyTimer = 3f;
                    _bestiaryNotifySpecies = _l3ScanTarget;

                    // Fade glitch out over next second
                    _l3GlitchIntensity = 0.8f; // will be faded by drawing code
                }
            }
        }
        // Fade L3 glitch when not scanning
        if (!_isL3Scanning && _l3GlitchIntensity > 0)
            _l3GlitchIntensity = Math.Max(0, _l3GlitchIntensity - dt * 2f);

        // Update scan reveal timer
        if (_scanRevealTimer > 0) _scanRevealTimer -= dt;

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
        _playerPrevVelY = _player.Velocity.Y;
        // Mouse world position for aiming
        var mouseState = Mouse.GetState();
        var mouseScreen = new Vector2(mouseState.X, mouseState.Y);
        var mouseWorld = Vector2.Transform(mouseScreen, Matrix.Invert(_camera.TransformMatrix));
        _player.Update(dt, kb, _level.Floor.Y, _level.AllPlatforms, ropesToPass, ropeTopsToPass, ropeBottomsToPass, wallsToPass, wallSidesToPass, _level.WallRects, _level.CeilingRects, _level.SolidFloorRects, _level.TileGridInstance, mouseWorld, mouseState);
        _player.UpdateRegen(dt);
        
        // Tech Ground Pound landing — camera shake, dust, damage nearby enemies
        if (_player.GroundPoundLanded)
        {
            _player.GroundPoundLanded = false;
            _shakeTimer = 0.25f;
            _shakeIntensity = 8f;
            // Spawn dust particles
            float px = _player.Position.X + Player.Width / 2f;
            float py = _player.Position.Y + Player.Height;
            for (int i = 0; i < 12; i++)
            {
                float angle = (float)(Math.PI + _rng.NextDouble() * Math.PI); // downward-ish spread
                float spd = 40f + (float)_rng.NextDouble() * 80f;
                _particles.Add(new Particle
                {
                    Position = new Vector2(px + (_rng.Next(20) - 10), py - 2),
                    Velocity = new Vector2((float)Math.Cos(angle) * spd, (float)Math.Sin(angle) * spd * 0.3f),
                    Life = 0.4f + (float)_rng.NextDouble() * 0.3f,
                    MaxLife = 0.7f,
                    Color = new Color(180, 160, 130),
                    Size = 2 + _rng.Next(2)
                });
            }
            // Damage enemies in stomp radius
            var stompRect = new Rectangle(
                (int)(px - Player.GroundPoundRadius),
                (int)(py - 8),
                (int)(Player.GroundPoundRadius * 2),
                16);
            DamageCreaturesInRect(stompRect, (int)Player.GroundPoundDamage, 0, -100f, _player.FacingDir, true);
        }
        
        // Movement particles — dash burst, sprint, landing, glide
        {
            float px = _player.Position.X + Player.Width / 2f;
            float py = _player.Position.Y + Player.Height;
            
            // Dash burst — puff at origin on dash start
            if (_player.WantsDashParticles)
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                    float spd = 30f + (float)_rng.NextDouble() * 60f;
                    _particles.Add(new Particle
                    {
                        Position = new Vector2(px + (_rng.Next(8) - 4), py - Player.Height / 2f),
                        Velocity = new Vector2((float)Math.Cos(angle) * spd, (float)Math.Sin(angle) * spd),
                        Life = 0.2f + (float)_rng.NextDouble() * 0.15f,
                        MaxLife = 0.35f,
                        Color = _player.CurrentTier switch {
                            Player.MoveTier.Tech => new Color(200, 220, 255),  // blue-white exhaust
                            Player.MoveTier.Bio => new Color(150, 200, 120),   // green organic
                            _ => new Color(200, 150, 255)                       // purple cipher
                        },
                        Size = 2 + _rng.Next(2)
                    });
                }
            }
            
            // Sprint — trailing dust every few frames
            if (_player.WantsSprintParticles && _rng.NextDouble() < 0.3)
            {
                _particles.Add(new Particle
                {
                    Position = new Vector2(px - _player.FacingDir * 6, py),
                    Velocity = new Vector2(-_player.FacingDir * (20f + (float)_rng.NextDouble() * 30f), -10f - (float)_rng.NextDouble() * 20f),
                    Life = 0.3f + (float)_rng.NextDouble() * 0.2f,
                    MaxLife = 0.5f,
                    Color = new Color(160, 140, 120),
                    Size = 1 + _rng.Next(2)
                });
            }
            
            // Landing puff
            if (_player.WantsLandingParticles)
            {
                for (int i = 0; i < 4; i++)
                {
                    float dir = i < 2 ? -1f : 1f;
                    _particles.Add(new Particle
                    {
                        Position = new Vector2(px + dir * (4 + _rng.Next(6)), py),
                        Velocity = new Vector2(dir * (30f + (float)_rng.NextDouble() * 40f), -15f - (float)_rng.NextDouble() * 25f),
                        Life = 0.25f + (float)_rng.NextDouble() * 0.15f,
                        MaxLife = 0.4f,
                        Color = new Color(180, 170, 150),
                        Size = 1 + _rng.Next(2)
                    });
                }
            }
            
            // Bio glide — trailing wisps
            if (_player.WantsGlideParticles && _rng.NextDouble() < 0.25)
            {
                _particles.Add(new Particle
                {
                    Position = new Vector2(px + (_rng.Next(10) - 5), py - Player.Height / 2f),
                    Velocity = new Vector2(-_player.FacingDir * 15f, 10f + (float)_rng.NextDouble() * 15f),
                    Life = 0.4f + (float)_rng.NextDouble() * 0.2f,
                    MaxLife = 0.6f,
                    Color = new Color(180, 230, 180, 150),
                    Size = 1
                });
            }
        }
        
        // Cipher teleport — damage enemies along path + terrain collision
        if (_player.IsCipherTeleporting)
        {
            // Check creatures along teleport line for damage
            foreach (var creature in _creatures)
            {
                if (creature.Hp <= 0) continue;
                var cr = creature.Rect;
                // Line-rect intersection approximation: check if creature bbox intersects the teleport corridor
                var start = _player.CipherTeleportStart;  
                var end = _player.CipherTeleportEnd;
                // Simple: check if creature center is within ~20px of the line
                var cc = new Vector2(cr.X + cr.Width / 2f, cr.Y + cr.Height / 2f);
                var lineDir = end - start;
                float lineLen = lineDir.Length();
                if (lineLen < 1f) continue;
                lineDir /= lineLen;
                float proj = Vector2.Dot(cc - start, lineDir);
                if (proj < 0 || proj > lineLen) continue;
                var closest = start + lineDir * proj;
                float dist = Vector2.Distance(cc, closest);
                if (dist < 24f) // 24px corridor width
                {
                    creature.Hp -= 4;
                    creature.HitFlash = 0.15f;
                    if (creature.Hp <= 0) creature.Hp = 0;
                }
            }
        }
        
        // Grapple hook collision — raycast along hook path
        if (_player.IsGrappleFiring)
        {
            var steps = _player.GetHookRaySteps(dt);
            bool hit = false;
            foreach (var sp in steps)
            {
                var pt = new Point((int)sp.X, (int)sp.Y);
                
                // Check enemies first (before terrain — hook grabs closest thing)
                foreach (var creature in _creatures)
                {
                    if (creature.Hp <= 0) continue;
                    if (creature is Crawler cr && cr.IsDummy) continue;
                    if (creature.Rect.Contains(pt))
                    {
                        string etype = creature switch { Crawler => "crawler", Bird => "bird", Wingbeater => "wingbeater", _ => "creature" };
                        var center = creature.Position + new Vector2(creature.Rect.Width / 2f, creature.Rect.Height / 2f);
                        _player.GrappleEnemy(-1, etype, center, creature.Id);
                        hit = true; break;
                    }
                }
                if (hit) break;
                
                // Check solid tiles (not background, not liquid, not empty)
                if (_level.TileGridInstance != null)
                {
                    var tile = _level.TileGridInstance.GetTile(pt.X, pt.Y);
                    if (TileProperties.IsSolid(tile))
                    {
                        _player.AttachGrapple(sp);
                        hit = true;
                        break;
                    }
                }
                
                // Check entity-based walls, solid floors, ceilings
                foreach (var r in _level.WallRects)
                    if (r.Contains(pt)) { _player.AttachGrapple(sp); hit = true; break; }
                if (hit) break;
                foreach (var r in _level.SolidFloorRects)
                    if (r.Contains(pt)) { _player.AttachGrapple(sp); hit = true; break; }
                if (hit) break;
                foreach (var r in _level.CeilingRects)
                    if (r.Contains(pt)) { _player.AttachGrapple(sp); hit = true; break; }
                if (hit) break;
            }
            if (!hit)
                _player.AdvanceHook(dt);
        }
        
        // Grapple enemy pull — update each frame while pulling
        if (_player.IsGrapplePulling)
        {
            var playerCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            float pullSpeed = 400f;
            bool done = false;
            
            // Find creature by Guid (safe across list changes)
            Creature target = null;
            if (_player.GrappleCreatureId != Guid.Empty)
                target = _creatures.FirstOrDefault(c => c.Id == _player.GrappleCreatureId);
            
            if (target == null || target.Hp <= 0)
            {
                _player.ReleaseGrapple();
                done = true;
            }
            else if (target is Wingbeater w)
            {
                // Wingbeater: yank downward — hollow bones, can't support grapple
                w.Velocity = new Vector2(w.Velocity.X * 0.5f, 500f);
                w.Hp -= 3;
                _shakeTimer = 0.15f;
                _shakeIntensity = 3f;
                EveAlert("Hollow bones. They can't take that.", 3f);
                _player.ReleaseGrapple();
                done = true;
            }
            else
            {
                // Small creatures: pull toward player
                var cCenter = target.Position + new Vector2(target.Rect.Width / 2f, target.Rect.Height / 2f);
                var dir = playerCenter - cCenter;
                float dist = dir.Length();
                if (dist < 20f)
                {
                    target.Hp -= 2;
                    target.Velocity = new Vector2(0, -150f);
                    _player.ReleaseGrapple();
                    done = true;
                }
                else
                {
                    dir /= dist;
                    target.Position += dir * pullSpeed * dt;
                    target.Velocity = dir * pullSpeed;
                    _player.GrappleAnchor = target.Position + new Vector2(target.Rect.Width / 2f, target.Rect.Height / 2f);
                }
            }
            
            // Release on E press while pulling
            if (!done)
            {
                bool ePressed = kb.IsKeyDown(Keys.E);
                if (ePressed && _prevKb.IsKeyUp(Keys.E))
                    _player.ReleaseGrapple();
            }
        }
        
        // Terrain collision while swinging — push player out of tiles, don't auto-release
        if (_player.IsGrappling && _level.TileGridInstance != null)
        {
            var grid = _level.TileGridInstance;
            var pRect = _player.CollisionRect;
            int tileSize = 32;
            
            // Check tiles the player overlaps
            int minCol = (pRect.Left - grid.OriginX) / tileSize;
            int maxCol = (pRect.Right - 1 - grid.OriginX) / tileSize;
            int minRow = (pRect.Top - grid.OriginY) / tileSize;
            int maxRow = (pRect.Bottom - 1 - grid.OriginY) / tileSize;
            
            for (int row = minRow; row <= maxRow; row++)
            for (int col = minCol; col <= maxCol; col++)
            {
                if (col < 0 || col >= grid.Width || row < 0 || row >= grid.Height) continue;
                if (!TileProperties.IsSolid(grid.Tiles[col, row])) continue;
                
                var tileRect = new Rectangle(
                    grid.OriginX + col * tileSize,
                    grid.OriginY + row * tileSize,
                    tileSize, tileSize);
                var overlap = Rectangle.Intersect(pRect, tileRect);
                if (overlap.Width <= 0 || overlap.Height <= 0) continue;
                
                // Push out along smallest overlap axis
                if (overlap.Width < overlap.Height)
                {
                    float pushX = (pRect.Center.X < tileRect.Center.X) ? -overlap.Width : overlap.Width;
                    _player.Position = new Vector2(_player.Position.X + pushX, _player.Position.Y);
                    _player.GrappleNudgeVel(new Vector2(-_player.Velocity.X * 0.5f, 0)); // bounce off
                }
                else
                {
                    float pushY = (pRect.Center.Y < tileRect.Center.Y) ? -overlap.Height : overlap.Height;
                    _player.Position = new Vector2(_player.Position.X, _player.Position.Y + pushY);
                    _player.GrappleNudgeVel(new Vector2(0, -_player.Velocity.Y * 0.5f)); // bounce off
                }
                pRect = _player.CollisionRect; // re-read after push
            }
            
            // Also push out of entity walls
            foreach (var r in _level.WallRects)
            {
                var overlap = Rectangle.Intersect(pRect, r);
                if (overlap.Width <= 0 || overlap.Height <= 0) continue;
                float pushX = (pRect.Center.X < r.Center.X) ? -overlap.Width : overlap.Width;
                _player.Position = new Vector2(_player.Position.X + pushX, _player.Position.Y);
                _player.GrappleNudgeVel(new Vector2(-_player.Velocity.X * 0.5f, 0));
                pRect = _player.CollisionRect;
            }
            foreach (var r in _level.CeilingRects)
            {
                var overlap = Rectangle.Intersect(pRect, r);
                if (overlap.Width <= 0 || overlap.Height <= 0) continue;
                float pushY = (pRect.Center.Y < r.Center.Y) ? -overlap.Height : overlap.Height;
                _player.Position = new Vector2(_player.Position.X, _player.Position.Y + pushY);
                _player.GrappleNudgeVel(new Vector2(0, -_player.Velocity.Y * 0.5f));
                pRect = _player.CollisionRect;
            }
        }
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

        // Sidearm reload + fire cooldown
        if (_sidearmReloading)
        {
            _sidearmReloadTimer -= dt;
            if (_sidearmReloadTimer <= 0)
            {
                _sidearmReloading = false;
                _sidearmRounds = SidearmClipSize;
            }
        }
        _sidearmFireTimer -= dt;
        _muzzleFlashTimer -= dt;

        // Manual reload (R key)
        if (kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R) && !_prevKb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R)
            && !_sidearmReloading && _sidearmRounds < SidearmClipSize && CurrentRanged == WeaponType.Gun)
        {
            _sidearmReloading = true;
            _sidearmReloadTimer = SidearmReloadTime;
        }

        // Shoot bullets (with ammo system for sidearm)
        if (_player.WantsToShoot && _sidearmFireTimer <= 0)
        {
            bool isGun = CurrentRanged == WeaponType.Gun;
            bool canFire = !isGun || (!_sidearmReloading && _sidearmRounds > 0);
            if (canFire)
            {
                _bullets.Add(new Bullet(PlayerCenter, _player.ShootDirection));
                _sidearmFireTimer = SidearmFireCooldown;
                if (isGun)
                {
                    _sidearmRounds--;
                    // Muzzle flash
                    _muzzleFlashTimer = 0.06f;
                    _muzzleFlashPos = PlayerCenter + _player.ShootDirection * 16f;
                    _muzzleFlashDir = _player.ShootDirection;
                    // Shell casing particle
                    var casingDir = new Vector2(-_player.ShootDirection.Y, _player.ShootDirection.X);
                    _particles.Add(new Particle
                    {
                        Position = PlayerCenter + casingDir * 4f,
                        Velocity = casingDir * (80f + _rng.Next(40)) + new Vector2(0, -60f - _rng.Next(30)),
                        Life = 0.5f, MaxLife = 0.5f,
                        Color = new Color(200, 180, 80),
                        Size = 2
                    });
                    // Auto-reload when empty
                    if (_sidearmRounds <= 0)
                    {
                        _sidearmReloading = true;
                        _sidearmReloadTimer = SidearmReloadTime;
                    }
                }
            }
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
                if (dmg > 0) { _lastDamageSource = "Swarm"; _player.TakeDamage(dmg, _player.Position.X - swarm.HomePosition.X); }
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
        if (_nextLatchDelay > 0) _nextLatchDelay -= dt;
        int latchedCount = 0;
        foreach (var c in _crawlers) if (c.IsLatched) latchedCount++;

        foreach (var c in _crawlers)
        {
            // Update latched crawlers: stick to player, deal tick damage
            if (c.IsLatched)
            {
                c.Position = _player.Position + c.LatchOffset;
                int tickDmg = c.UpdateLatch(dt);
                if (tickDmg > 0 && _spawnInvincibility <= 0 && !_isDead)
                {
                    _lastDamageSource = "Crawler (latched)";
                    _player.TakeDamage(tickDmg, 0);
                }
            }

            c.Update(dt, playerCenter2,
                _level.TileGridInstance, _level.TileGrid?.TileSize ?? 32,
                _level.AllPlatforms, _level.SolidFloorRects, _level.Floor.Y);
            
            // Bombardier spray: spawn hot particles when spray fires
            if (c.BombardierSprayed)
            {
                var sprayOrigin = c.Position + new Vector2(c.EffectiveWidth / 2f, c.EffectiveHeight * 0.8f);
                // Predict player position for aiming
                var playerVel = _player.Velocity;
                float leadTime = 0.3f;
                var predicted = playerCenter2 + playerVel * leadTime;
                var aimDir = predicted - sprayOrigin;
                if (aimDir.Length() > 0.001f) aimDir = Vector2.Normalize(aimDir);
                
                for (int si = 0; si < 8; si++)
                {
                    // Fan spread: ±25° around aim direction
                    float spread = ((float)_rng.NextDouble() - 0.5f) * 0.87f; // ~±25°
                    float cos = MathF.Cos(spread), sin = MathF.Sin(spread);
                    var dir = new Vector2(aimDir.X * cos - aimDir.Y * sin, aimDir.X * sin + aimDir.Y * cos);
                    float speed = 200f + (float)_rng.NextDouble() * 150f;
                    _particles.Add(new Particle
                    {
                        Position = sprayOrigin + dir * 4f,
                        Velocity = dir * speed + new Vector2(0, -30f), // slight upward arc
                        Life = 0.6f + (float)_rng.NextDouble() * 0.3f,
                        MaxLife = 0.9f,
                        Color = new Color(255, 140 + _rng.Next(60), 20), // orange-hot
                        Size = 2 + _rng.Next(2),
                        DamagesPlayer = true,
                        Damage = 1
                    });
                }
                _shakeTimer = 0.08f; _shakeIntensity = 1.5f;
            }

            // Latch-on: aggroed crawler touches player → latches instead of contact damage
            if (_spawnInvincibility <= 0 && !_isDead && !c.IsLatched)
            {
                if (c.CanLatch && latchedCount < MaxLatched && _nextLatchDelay <= 0
                    && c.Rect.Intersects(playerRect2))
                {
                    c.Latch(_player.Position, _rng);
                    latchedCount++;
                    _nextLatchDelay = 0.4f; // stagger: 0.4s between latches
                    _shakeTimer = 0.1f; _shakeIntensity = 2f;
                    // EVE warns about latched crawlers
                    if (_eveOrbActive)
                    {
                        string[] warns = {
                            "Hostile organism attached! Shake it off!",
                            "Crawler latched on! Use melee or dash!",
                            "Parasite detected on suit! Remove it!",
                            "Warning: biological attachment detected!"
                        };
                        EveAlert(warns[_rng.Next(warns.Length)], 2.5f);
                    }
                }
                else
                {
                    // Normal contact damage for non-latchable crawlers (dummies, frozen, etc.)
                    int dmg = c.CheckPlayerDamage(playerRect2, _player.Velocity.Y);
                    if (dmg > 0) { _lastDamageSource = "Crawler"; _player.TakeDamage(dmg, _player.Position.X - c.Position.X); }
                }
            }
            if (_player.MeleeTimer > 0 && c.Alive)
            {
                if (_player.MeleeHitbox.Intersects(c.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    if (c.IsDummy && c.AlwaysCrit) finisher = true;
                    int prevHp = c.Hp;
                    var ws = WeaponStats.Get(_player.CurrentWeapon);
                    int dmg = finisher ? ws.FinisherDamage : ws.Damage;
                    dmg = ApplyScanBonus(dmg, c.IsDummy ? "dummy" : c.Variant.ToString().ToLower());
                    float kbDir = _player.FacingDir;
                    bool killed = _knockbackEnabled
                        ? c.TakeHit(dmg, kbDir * ws.KnockbackForce, ws.KnockbackUp)
                        : c.TakeHit(dmg);
                    bool didHit = c.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) c.MeleeHitCooldown = 0.055f;
                        var hitPt = new Vector2(c.Position.X + c.EffectiveWidth/2f, c.Position.Y + c.EffectiveHeight/2f);
                        SpawnHitSpray(hitPt, _player.FacingDir, GetEnemyHitColor(c.IsDummy ? "dummy" : "crawler"), ws.Weight, finisher);
                        if (killed)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopKill;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.5f; _shakeIntensity = ws.ShakeIntensity * 1.2f; }
                            if (_deathParticlesEnabled) SpawnDeathParticles(new Vector2(c.Position.X + c.EffectiveWidth / 2f, c.Position.Y + c.EffectiveHeight / 2f), new Color(120, 60, 20));
                        }
                        else if (finisher)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopFinisher;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.2f; _shakeIntensity = ws.ShakeIntensity; }
                        }
                        else
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopNormal;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration; _shakeIntensity = ws.ShakeIntensity * 0.8f; }
                        }
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
                if (dmg > 0) { _lastDamageSource = "Thornback"; _player.TakeDamage(dmg, _player.Position.X - t.Position.X); }
            }
            if (_player.MeleeTimer > 0 && t.Alive)
            {
                if (_player.MeleeHitbox.Intersects(t.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    int prevHp = t.Hp;
                    var ws = WeaponStats.Get(_player.CurrentWeapon);
                    int dmg = finisher ? ws.FinisherDamage : ws.Damage;
                    float kbDir = _player.FacingDir;
                    dmg = ApplyScanBonus(dmg, "thornback");
                    bool killed = _knockbackEnabled
                        ? t.TakeHit(dmg, kbDir * ws.KnockbackForce, ws.KnockbackUp)
                        : t.TakeHit(dmg);
                    bool didHit = t.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) t.MeleeHitCooldown = 0.055f;
                        var hitPt = new Vector2(t.Position.X + Thornback.Width/2f, t.Position.Y + Thornback.Height/2f);
                        SpawnHitSpray(hitPt, _player.FacingDir, GetEnemyHitColor("thornback"), ws.Weight, finisher);
                        if (killed)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopKill;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.5f; _shakeIntensity = ws.ShakeIntensity * 1.2f; }
                            if (_deathParticlesEnabled) SpawnDeathParticles(new Vector2(t.Position.X + Thornback.Width / 2f, t.Position.Y + Thornback.Height / 2f), new Color(60, 100, 40));
                        }
                        else if (finisher)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopFinisher;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.2f; _shakeIntensity = ws.ShakeIntensity; }
                        }
                        else
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopNormal;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration; _shakeIntensity = ws.ShakeIntensity * 0.8f; }
                        }
                    }
                }
            }
        }

        // Update hoppers
        foreach (var h in _hoppers)
        {
            h.Update(dt, playerCenter2,
                _level.TileGridInstance, _level.TileGrid?.TileSize ?? 32,
                _level.AllPlatforms, _level.SolidFloorRects, _level.Floor.Y);
            if (_spawnInvincibility <= 0 && !_isDead)
            {
                int dmg = h.CheckPlayerDamage(playerRect2);
                if (dmg > 0) { _lastDamageSource = "Hopper"; _player.TakeDamage(dmg, _player.Position.X - h.Position.X); }
            }
            if (_player.MeleeTimer > 0 && h.Alive)
            {
                if (_player.MeleeHitbox.Intersects(h.Rect))
                {
                    bool finisher = _player.IsComboFinisher;
                    int prevHp = h.Hp;
                    var ws = WeaponStats.Get(_player.CurrentWeapon);
                    int dmg = finisher ? ws.FinisherDamage : ws.Damage;
                    float kbDir = _player.FacingDir;
                    dmg = ApplyScanBonus(dmg, "hopper");
                    bool killed = _knockbackEnabled
                        ? h.TakeHit(dmg, kbDir * ws.KnockbackForce, ws.KnockbackUp)
                        : h.TakeHit(dmg);
                    bool didHit = h.Hp < prevHp || killed;
                    if (didHit)
                    {
                        _player.RegisterComboHit();
                        if (finisher) h.MeleeHitCooldown = 0.055f;
                        var hitPt = new Vector2(h.Position.X + Hopper.Width/2f, h.Position.Y + Hopper.Height/2f);
                        SpawnHitSpray(hitPt, _player.FacingDir, GetEnemyHitColor("hopper"), ws.Weight, finisher);
                        if (killed)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopKill;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.5f; _shakeIntensity = ws.ShakeIntensity * 1.2f; }
                            if (_deathParticlesEnabled) SpawnDeathParticles(new Vector2(h.Position.X + Hopper.Width / 2f, h.Position.Y + Hopper.Height / 2f), new Color(100, 80, 60));
                        }
                        else if (finisher)
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopFinisher;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.2f; _shakeIntensity = ws.ShakeIntensity; }
                        }
                        else
                        {
                            if (_hitStopEnabled) _hitStopTimer = ws.HitStopNormal;
                            if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration; _shakeIntensity = ws.ShakeIntensity * 0.8f; }
                        }
                    }
                }
            }
        }

        // Update birds (non-hostile ambient creatures)
        foreach (var bird in _birds)
            bird.Update(dt, playerCenter2,
                _level.TileGridInstance, _level.TileGrid?.TileSize ?? 32,
                _level.AllPlatforms, _level.SolidFloorRects, _level.Floor.Y);

        // Melee hit detection for birds
        if (_player.MeleeTimer > 0)
        {
            foreach (var bird in _birds)
            {
                if (!bird.Alive) continue;
                if (_player.MeleeHitbox.Intersects(bird.Rect))
                {
                    var ws = WeaponStats.Get(_player.CurrentWeapon);
                    float kbDir = _player.FacingDir;
                    bool killed = bird.TakeHit(ws.Damage, kbDir * ws.KnockbackForce, ws.KnockbackUp);
                    if (killed)
                    {
                        _player.RegisterComboHit();
                        var hitPt = new Vector2(bird.Position.X + Bird.Width / 2f, bird.Position.Y + Bird.Height / 2f);
                        SpawnHitSpray(hitPt, _player.FacingDir, GetEnemyHitColor("bird"), ws.Weight, true);
                        if (_hitStopEnabled) _hitStopTimer = ws.HitStopKill;
                        if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.5f; _shakeIntensity = ws.ShakeIntensity * 1.2f; }
                        if (_deathParticlesEnabled) SpawnDeathParticles(new Vector2(bird.Position.X + Bird.Width / 2f, bird.Position.Y + Bird.Height / 2f), new Color(110, 85, 55));
                    }
                }
            }
        }

        // Track kills before removal
        foreach (var c in _creatures)
        {
            if (!c.Alive && c.Hp <= 0)
            {
                _totalKills++;
                string area = _worldGraph?.GetRoom(_currentRoomId)?.AreaId ?? "unknown";
                _areaKillCounts.TryGetValue(area, out int count);
                _areaKillCounts[area] = count + 1;
                // Check if passive/fleeing creature → EVE silence
                bool isPassive = c is Crawler cr && (cr.Variant == CrawlerVariant.Forager || cr.Variant == CrawlerVariant.Skitter);
                if (isPassive)
                {
                    _passiveCreatureKills++;
                    _eveSilenceTimer += EveSilencePerPassiveKill;
                }
            }
        }

        _creatures.RemoveAll(c => !c.Alive);
        // Sync typed lists (shrinking toward zero over time)
        _birds.RemoveAll(b => !b.Alive);
        _crawlers.RemoveAll(c => !c.Alive);
        _hoppers.RemoveAll(h => !h.Alive);
        _thornbacks.RemoveAll(t => !t.Alive);
        _wingbeaters.RemoveAll(w => !w.Alive);

        // --- Wingbeater update ---
        foreach (var wb in _wingbeaters)
            wb.Update(dt, playerCenter2, _level.Floor.Y);

        // Melee hit detection for wingbeaters
        if (_player.MeleeTimer > 0)
        {
            foreach (var wb in _wingbeaters)
            {
                if (!wb.Alive) continue;
                if (_player.MeleeHitbox.Intersects(wb.Rect))
                {
                    var ws = WeaponStats.Get(_player.CurrentWeapon);
                    float kbDir = _player.FacingDir;
                    bool killed = wb.TakeHit(ws.Damage, kbDir * ws.KnockbackForce, ws.KnockbackUp);
                    var hitPt = new Vector2(wb.Position.X + Wingbeater.Width / 2f, wb.Position.Y + Wingbeater.Height / 2f);
                    SpawnHitSpray(hitPt, _player.FacingDir, GetEnemyHitColor("wingbeater"), ws.Weight, killed);
                    if (killed)
                    {
                        _player.RegisterComboHit();
                        if (_hitStopEnabled) _hitStopTimer = ws.HitStopKill;
                        if (_screenShakeEnabled) { _shakeTimer = ws.ShakeDuration * 1.5f; _shakeIntensity = ws.ShakeIntensity * 1.2f; }
                        if (_deathParticlesEnabled) SpawnDeathParticles(hitPt, new Color(160, 60, 40));
                    }
                    else
                    {
                        if (_hitStopEnabled) _hitStopTimer = ws.HitStopNormal;
                    }
                }
            }
        }

        // Wingbeater contact damage
        if (!_isDead)
        {
            var playerRect3 = _player.CollisionRect;
            foreach (var wb in _wingbeaters)
            {
                if (!wb.Alive) continue;
                int wbDmg = wb.CheckPlayerDamage(playerRect3);
                if (wbDmg > 0 && _spawnInvincibility <= 0f)
                {
                    _lastDamageSource = "Wingbeater";
                    _player.TakeDamage(wbDmg, _player.Position.X - wb.Position.X);
                    if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
                }
            }
        }

        // (wingbeater death removal consolidated into unified RemoveAll block above)

        // --- Ecosystem: Trophic Interactions ---
        _birdHuntTimer -= dt;
        if (_birdHuntTimer <= 0)
        {
            _birdHuntTimer = 2f; // check every 2s
            foreach (var bird in _birds)
            {
                if (!bird.Alive) continue;
                var bCenter = bird.Position + new Vector2(Bird.Width / 2f, Bird.Height / 2f);
                Crawler nearestPrey = null;
                float nearestDist = 200f;
                foreach (var c in _crawlers)
                {
                    if (!c.Alive || c.IsDummy) continue;
                    var cCenter = c.Position + new Vector2(c.EffectiveWidth / 2f, c.EffectiveHeight / 2f);
                    float d = Vector2.Distance(bCenter, cCenter);
                    if (d < nearestDist) { nearestDist = d; nearestPrey = c; }
                }
                if (nearestPrey != null)
                {
                    // Bird dive-kills the crawler
                    nearestPrey.TakeHit(999);
                    var killPos = nearestPrey.Position + new Vector2(nearestPrey.EffectiveWidth / 2f, nearestPrey.EffectiveHeight / 2f);
                    SpawnDeathParticles(killPos, GetEnemyHitColor("crawler"), 6);
                }
            }
        }
        // Crawlers flee from nearby birds
        foreach (var c in _crawlers)
        {
            if (!c.Alive || c.IsDummy) continue;
            var cCenter = c.Position + new Vector2(c.EffectiveWidth / 2f, c.EffectiveHeight / 2f);
            foreach (var bird in _birds)
            {
                if (!bird.Alive) continue;
                var bCenter = bird.Position + new Vector2(Bird.Width / 2f, Bird.Height / 2f);
                if (Vector2.Distance(cCenter, bCenter) < 150f)
                {
                    // Flee: reverse direction away from bird
                    c.Dir = bCenter.X > cCenter.X ? -1 : 1;
                    break;
                }
            }
        }

        // Crawler Swarm: Leaper as pack leader — nearby crawlers mob the player
        _swarmDamageTimer -= dt;
        if (_swarmDamageTimer <= 0)
        {
            _swarmDamageTimer = 0.5f;
            var aliveCrawlers = new List<Crawler>();
            foreach (var c in _crawlers)
                if (c.Alive && !c.IsDummy && !c.Frozen && !c.IsLatched) aliveCrawlers.Add(c);

            // Find leapers that are aggroed (pack leaders)
            var aggroedLeapers = new List<Crawler>();
            foreach (var c in aliveCrawlers)
                if (c.Variant == CrawlerVariant.Leaper && c.Aggroed) aggroedLeapers.Add(c);

            // For each aggroed leaper, recruit nearby crawlers into swarm
            var swarmMembers = new HashSet<Crawler>();
            foreach (var leader in aggroedLeapers)
            {
                var lCenter = leader.Position + new Vector2(leader.EffectiveWidth / 2f, leader.EffectiveHeight / 2f);
                var nearby = new List<Crawler>();
                foreach (var other in aliveCrawlers)
                {
                    if (other == leader) continue;
                    var oCenter = other.Position + new Vector2(other.EffectiveWidth / 2f, other.EffectiveHeight / 2f);
                    if (Vector2.Distance(lCenter, oCenter) < 150f) // recruitment range
                        nearby.Add(other);
                }

                // Need at least 3 nearby (4 total with leader) to trigger swarm
                if (nearby.Count >= 3)
                {
                    swarmMembers.Add(leader);
                    foreach (var n in nearby) swarmMembers.Add(n);
                }
            }

            // Apply swarm state — once frenzied, PERMANENT (no undo)
            float playerTargetX = _player.Position.X + Player.SpriteW / 2f;
            bool swarmJustStarted = false;
            foreach (var c in aliveCrawlers)
            {
                if (swarmMembers.Contains(c) && !c.SwarmActive)
                    swarmJustStarted = true;
                if (swarmMembers.Contains(c))
                    c.SwarmActive = true; // permanent — never cleared

                // All frenzied crawlers track player
                if (c.SwarmActive)
                {
                    c.SwarmTargetX = playerTargetX;

                    // Swarm damage: each swarmer touching player deals tick damage
                    if (_spawnInvincibility <= 0 && !_isDead)
                    {
                        var sRect = c.Rect;
                        var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.SpriteW, Player.SpriteH);
                        if (sRect.Intersects(pRect) && c.DamageCooldown <= 0)
                        {
                            int dmg = c.Variant == CrawlerVariant.Leaper ? 2 : 1;
                            _lastDamageSource = "Crawler Swarm";
                            _player.TakeDamage(dmg, _player.Position.X - c.Position.X);
                            c.DamageCooldown = 0.8f;
                        }
                    }
                }
            }

            // EVE warns when swarm triggers
            if (swarmJustStarted && _eveOrbActive)
            {
                var warns = new[] { "Multiple hostiles coordinating!", "They're swarming!", "They've gone feral — watch yourself!" };
                EveAlert(warns[_rng.Next(warns.Length)], 3f);
                _shakeTimer = 0.15f; _shakeIntensity = 2f;
            }
        }

        // Bullets vs crawlers and thornbacks
        foreach (var b in _bullets)
        {
            if (b.IsDead) continue;
            var bRect = new Rectangle((int)b.Position.X, (int)b.Position.Y, Bullet.Size, Bullet.Size);
            float bDir = b.Direction.X >= 0 ? 1f : -1f;
            float bulletKb = 120f; // light knockback
            float bulletKbUp = -60f;
            foreach (var c in _crawlers)
            {
                if (c.Alive && bRect.Intersects(c.Rect))
                {
                    bool killed = _knockbackEnabled
                        ? c.TakeHit(2, bDir * bulletKb, bulletKbUp)
                        : c.TakeHit(2);
                    var hitPt = new Vector2(c.Position.X + c.EffectiveWidth/2f, c.Position.Y + c.EffectiveHeight/2f);
                    SpawnHitSpray(hitPt, (int)bDir, GetEnemyHitColor(c.IsDummy ? "dummy" : "crawler"), 1, false);
                    if (killed && _deathParticlesEnabled) SpawnDeathParticles(hitPt, new Color(120, 60, 20));
                    if (_hitStopEnabled) _hitStopTimer = 0.02f;
                    b.IsDead = true; break;
                }
            }
            if (b.IsDead) continue;
            foreach (var t in _thornbacks)
            {
                if (t.Alive && bRect.Intersects(t.Rect))
                {
                    t.TakeHit(1);
                    var hitPt = new Vector2(t.Position.X + Thornback.Width/2f, t.Position.Y + Thornback.Height/2f);
                    SpawnHitSpray(hitPt, (int)bDir, GetEnemyHitColor("thornback"), 1, false);
                    if (_hitStopEnabled) _hitStopTimer = 0.02f;
                    b.IsDead = true; break;
                }
            }
            if (b.IsDead) continue;
            foreach (var h in _hoppers)
            {
                if (h.Alive && bRect.Intersects(h.Rect))
                {
                    bool killed = _knockbackEnabled
                        ? h.TakeHit(2, bDir * bulletKb, bulletKbUp)
                        : h.TakeHit(2);
                    var hitPt = new Vector2(h.Position.X + Hopper.Width/2f, h.Position.Y + Hopper.Height/2f);
                    SpawnHitSpray(hitPt, (int)bDir, GetEnemyHitColor("hopper"), 1, false);
                    if (killed && _deathParticlesEnabled) SpawnDeathParticles(hitPt, new Color(60, 120, 40));
                    if (_hitStopEnabled) _hitStopTimer = 0.02f;
                    b.IsDead = true; break;
                }
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
                    if (tgi.GetTileAt(bc, br2) == TileType.Breakable || tgi.GetTileAt(bc, br2) == TileType.BreakableGlass || tgi.GetTileAt(bc, br2) == TileType.BreakableBattery)
                    {
                        var origT = tgi.GetTileAt(bc, br2);
                        _destroyedBreakables.Add((bc, br2, origT));
                        tgi.SetTileAt(bc, br2, TileType.Empty);
                        _level.RebuildTileCollision();
                        int twx = ox + bc * ts, twy = oy + br2 * ts;
                        if (origT == TileType.Breakable)
                        {
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
                        }
                        else if (origT == TileType.BreakableBattery)
                        {
                            _itemPickups.Add(new ItemPickup
                            {
                                Id = $"battery-break-{twx}-{twy}",
                                X = twx + ts / 2f - 8,
                                Y = twy,
                                W = 16, H = 16,
                                ItemType = "battery",
                                Collected = false,
                                HasGravity = true,
                                VelY = -150f
                            });
                        }
                        b.IsDead = true;
                    }
                }
            }
        }
        // Wall-splat check for knocked enemies
        if (_knockbackEnabled)
        {
            foreach (var c in _crawlers)
            {
                if (!c.Alive || c.KnockbackVel.LengthSquared() < 100f) continue;
                float kbSpeed = c.KnockbackVel.Length();
                bool hitWall = false;
                if (c.Position.X < _level.Bounds.Left) { c.Position.X = _level.Bounds.Left; hitWall = true; }
                if (c.Position.X + c.EffectiveWidth > _level.Bounds.Right) { c.Position.X = _level.Bounds.Right - c.EffectiveWidth; hitWall = true; }
                var cRect = c.Rect;
                foreach (var wall in _level.WallRects)
                {
                    if (cRect.Intersects(wall))
                    {
                        if (c.KnockbackVel.X > 0) c.Position.X = wall.Left - c.EffectiveWidth;
                        else c.Position.X = wall.Right;
                        hitWall = true;
                    }
                }
                if (hitWall)
                {
                    int splatDmg = (int)(kbSpeed / 100f);
                    if (splatDmg > 0)
                    {
                        bool killed = c.TakeHit(splatDmg);
                        if (_hitStopEnabled) _hitStopTimer = MathF.Max(_hitStopTimer, 0.08f);
                        if (_screenShakeEnabled) { _shakeTimer = 0.12f; _shakeIntensity = 8f; }
                        if (_deathParticlesEnabled && killed) SpawnDeathParticles(new Vector2(c.Position.X + c.EffectiveWidth/2f, c.Position.Y + c.EffectiveHeight/2f), new Color(120, 60, 20));
                        SpawnDustParticles(new Vector2(c.Position.X + (c.KnockbackVel.X > 0 ? Crawler.Width : 0), c.Position.Y + c.EffectiveHeight / 2f), 6);
                    }
                    c.KnockbackVel = Vector2.Zero;
                }
            }
            foreach (var h in _hoppers)
            {
                if (!h.Alive || h.KnockbackVel.LengthSquared() < 100f) continue;
                float kbSpeed = h.KnockbackVel.Length();
                bool hitWall = false;
                if (h.Position.X < _level.Bounds.Left) { h.Position.X = _level.Bounds.Left; hitWall = true; }
                if (h.Position.X + Hopper.Width > _level.Bounds.Right) { h.Position.X = _level.Bounds.Right - Hopper.Width; hitWall = true; }
                var hRect = h.Rect;
                foreach (var wall in _level.WallRects)
                {
                    if (hRect.Intersects(wall))
                    {
                        if (h.KnockbackVel.X > 0) h.Position.X = wall.Left - Hopper.Width;
                        else h.Position.X = wall.Right;
                        hitWall = true;
                    }
                }
                if (hitWall)
                {
                    int splatDmg = (int)(kbSpeed / 100f);
                    if (splatDmg > 0)
                    {
                        bool killed = h.TakeHit(splatDmg);
                        if (_hitStopEnabled) _hitStopTimer = MathF.Max(_hitStopTimer, 0.08f);
                        if (_screenShakeEnabled) { _shakeTimer = 0.12f; _shakeIntensity = 8f; }
                        if (_deathParticlesEnabled && killed) SpawnDeathParticles(new Vector2(h.Position.X + Hopper.Width/2f, h.Position.Y + Hopper.Height/2f), new Color(100, 80, 60));
                        SpawnDustParticles(new Vector2(h.Position.X + (h.KnockbackVel.X > 0 ? Hopper.Width : 0), h.Position.Y + Hopper.Height / 2f), 6);
                    }
                    h.KnockbackVel = Vector2.Zero;
                }
            }
            foreach (var b in _birds)
            {
                if (!b.Alive || b.KnockbackVel.LengthSquared() < 100f) continue;
                float kbSpeed = b.KnockbackVel.Length();
                bool hitWall = false;
                if (b.Position.X < _level.Bounds.Left) { b.Position.X = _level.Bounds.Left; hitWall = true; }
                if (b.Position.X + Bird.Width > _level.Bounds.Right) { b.Position.X = _level.Bounds.Right - Bird.Width; hitWall = true; }
                var bRect = b.Rect;
                foreach (var wall in _level.WallRects)
                {
                    if (bRect.Intersects(wall))
                    {
                        if (b.KnockbackVel.X > 0) b.Position.X = wall.Left - Bird.Width;
                        else b.Position.X = wall.Right;
                        hitWall = true;
                    }
                }
                if (hitWall)
                {
                    int splatDmg = (int)(kbSpeed / 100f);
                    if (splatDmg > 0)
                    {
                        bool killed = b.TakeHit(splatDmg);
                        if (_hitStopEnabled) _hitStopTimer = MathF.Max(_hitStopTimer, 0.08f);
                        if (_screenShakeEnabled) { _shakeTimer = 0.12f; _shakeIntensity = 8f; }
                        if (_deathParticlesEnabled && killed) SpawnDeathParticles(new Vector2(b.Position.X + Bird.Width/2f, b.Position.Y + Bird.Height/2f), new Color(80, 120, 160));
                        SpawnDustParticles(new Vector2(b.Position.X + (b.KnockbackVel.X > 0 ? Bird.Width : 0), b.Position.Y + Bird.Height / 2f), 6);
                    }
                    b.KnockbackVel = Vector2.Zero;
                }
            }
        }
        } // end _enemiesEnabled

        // Check HP death
        if (_player.Hp <= 0 && !_isDead)
        {
            _isDead = true;
            LogDeath();
        }

        // --- Weather update ---
        UpdateWeather(dt);

        // Update particles (outside hitstop so they animate during freeze)
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            p.Velocity.Y += 400f * dt;
            p.Position += p.Velocity * dt;

            // Hit floor — leave splatter and remove (check solid floors first)
            bool splatted = false;
            foreach (var sf in _level.SolidFloorRects)
            {
                // Particle must be within X bounds, crossing the top surface downward (within 8px tolerance)
                if (p.Position.X >= sf.Left && p.Position.X <= sf.Right
                    && p.Position.Y >= sf.Top && p.Position.Y <= sf.Top + 8
                    && p.Velocity.Y > 0)
                {
                    if (_rng.NextDouble() < 0.6)
                    {
                        if (_splatters.Count < 200)
                            _splatters.Add(new Splatter { Position = new Vector2(p.Position.X, sf.Top - 1), Life = 3.5f, Color = p.Color });
                        _particles.RemoveAt(i);
                        splatted = true;
                        break;
                    }
                }
            }
            if (splatted) continue;
            // Hit slope floor
            if (!splatted && p.Velocity.Y > 0 && _level.TileGridInstance != null)
            {
                float slopeY = _level.TileGridInstance.GetSlopeFloorY(p.Position.X - 1, p.Position.Y - 1, 2, 2);
                if (slopeY < float.MaxValue && p.Position.Y >= slopeY)
                {
                    if (_rng.NextDouble() < 0.6 && _splatters.Count < 200)
                        _splatters.Add(new Splatter { Position = new Vector2(p.Position.X, slopeY - 1), Life = 3.5f, Color = p.Color });
                    _particles.RemoveAt(i);
                    continue;
                }
            }
            if (p.Position.Y >= _level.Floor.Y)
            {
                if (_splatters.Count < 200)
                    _splatters.Add(new Splatter { Position = new Vector2(p.Position.X, _level.Floor.Y - 1), Life = 3.5f, Color = p.Color });
                _particles.RemoveAt(i);
                continue;
            }

            if (p.Life <= 0) { _particles.RemoveAt(i); continue; }
            
            // Bombardier spray particles damage player on contact
            if (p.DamagesPlayer && p.Damage > 0 && _spawnInvincibility <= 0 && !_isDead)
            {
                var pRect = new Rectangle((int)p.Position.X - p.Size / 2, (int)p.Position.Y - p.Size / 2, p.Size, p.Size);
                if (pRect.Intersects(_player.CollisionRect))
                {
                    _player.Hp -= p.Damage;
                    _shakeTimer = 0.05f; _shakeIntensity = 1f;
                    _particles.RemoveAt(i);
                    continue;
                }
            }
            
            _particles[i] = p;
        }

        // Update splatters
        for (int i = _splatters.Count - 1; i >= 0; i--)
        {
            var s = _splatters[i];
            s.Life -= dt;
            if (s.Life <= 0) { _splatters.RemoveAt(i); continue; }
            _splatters[i] = s;
        }

        // Death log timers
        for (int i = _deathLogTimers.Count - 1; i >= 0; i--)
        {
            _deathLogTimers[i] -= dt;
            if (_deathLogTimers[i] <= 0) { _deathLog.RemoveAt(i); _deathLogTimers.RemoveAt(i); }
        }

        // Dust on landing — scaled by fall speed
        if (_dustParticlesEnabled && _player.IsGrounded && !_playerWasGrounded)
        {
            float fallSpeed = MathF.Abs(_playerPrevVelY);
            int dustCount = (int)MathHelper.Clamp(fallSpeed / 40f, 4, 20);
            SpawnDustParticles(new Vector2(_player.Position.X + Player.Width / 2f, _player.Position.Y + Player.Height), dustCount);
        }
        // Dust on dash start
        if (_dustParticlesEnabled && _player.IsDashing && !_playerWasDashing)
            SpawnDustParticles(new Vector2(_player.Position.X + Player.Width / 2f, _player.Position.Y + Player.Height), 6);
        // Battery drain on tech dash start
        if (_player.IsDashing && !_playerWasDashing && _player.CurrentTier == Player.MoveTier.Tech)
            _player.Battery = MathF.Max(0, _player.Battery - TechDashBatteryCost);
        // Movement actions fling off latched crawlers
        bool actionFling = (_player.IsDashing && !_playerWasDashing)
            || _player.IsSliding || _player.IsUppercutting
            || _player.IsVaultKicking || _player.IsBladeDashing
            || _player.IsCartwheeling || _player.IsFlipping;
        if (actionFling)
        {
            foreach (var c in _crawlers)
            {
                if (c.IsLatched && _rng.NextDouble() < 0.6) // 60% chance each
                    c.Detach(_player.FacingDir * 200f, -150f);
            }
        }
        _playerWasGrounded = _player.IsGrounded;
        _playerWasDashing = _player.IsDashing;
        // Battery drain on grapple fire
        if (_player.IsGrappleFiring && !_playerWasGrappleFiring)
            _player.Battery = MathF.Max(0, _player.Battery - GrappleBatteryCost);
        _playerWasGrappleFiring = _player.IsGrappleFiring;

        // Segmented suit regen — slowly repairs to next 25% mark (disabled in hazard zones)
        if (_player.SuitIntegrity < 100f && _currentEnvType == "neutral")
        {
            float nextMark = MathF.Ceiling(_player.SuitIntegrity / 25f) * 25f;
            if (nextMark > _player.SuitIntegrity)
            {
                float regenRate = 1.5f; // % per second
                _player.SuitIntegrity = MathF.Min(nextMark, _player.SuitIntegrity + regenRate * dt);
            }
        }

        // Suit spark particles when integrity is low
        if (_player.SuitIntegrity < 25f)
        {
            _suitSparkTimer -= dt;
            float sparkFreq = 0.5f + (_player.SuitIntegrity / 25f) * 1.5f; // sparks every 0.5-2s
            if (_suitSparkTimer <= 0)
            {
                _suitSparkTimer = sparkFreq;
                var sparkPos = _player.Position + new Vector2(
                    _rng.Next(0, Player.Width),
                    _rng.Next(0, Player.Height));
                // Spawn 2-4 tiny yellow-white spark particles
                for (int s = 0; s < 2 + _rng.Next(3); s++)
                {
                    _particles.Add(new Particle
                    {
                        Position = sparkPos,
                        Velocity = new Vector2(
                            (_rng.NextSingle() - 0.5f) * 80f,
                            -_rng.NextSingle() * 60f - 20f),
                        Life = 0.2f + _rng.NextSingle() * 0.15f,
                        MaxLife = 0.35f,
                        Size = 1,
                        Color = _rng.NextDouble() > 0.5 ? Color.Yellow : Color.White
                    });
                }
            }
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

        // Auto-collect hearts and battery cells on touch
        if (!_isDead)
        {
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            for (int i = 0; i < _itemPickups.Count; i++)
            {
                var item = _itemPickups[i];
                if (!item.Collected && item.ItemType == "heart" && pRect.Intersects(item.Rect))
                {
                    item.Collected = true;
                    _player.Hp = Math.Min(_player.Hp + 3, _player.MaxHp);
                }
                if (!item.Collected && item.ItemType == "battery" && pRect.Intersects(item.Rect))
                {
                    item.Collected = true;
                    _player.Battery = MathF.Min(100f, _player.Battery + BatteryCellRestoreAmount);
                    SetEditorStatus($"Battery +{(int)BatteryCellRestoreAmount}%");
                }
            }
        }

        // Environmental region effects
        _player.EnvSpeedMult = 1f;
        _currentEnvType = "neutral";
        if (_level.EnvRegions.Length > 0)
        {
            var playerCenter = new Point((int)(_player.Position.X + 8), (int)(_player.Position.Y + 12));
            for (int i = 0; i < _level.EnvRegionRects.Length; i++)
            {
                if (_level.EnvRegionRects[i].Contains(playerCenter))
                {
                    _currentEnvType = _level.EnvRegions[i].Type;
                    break;
                }
            }

            float suitDeg = 0f, hpDrain = 0f;
            float speedMult = 1f;
            float suit = _player.SuitIntegrity;

            switch (_currentEnvType)
            {
                case "cold":
                    suitDeg = 0.5f;
                    if (suit < 25f) { hpDrain = 1.5f; speedMult = 0.85f; }
                    else if (suit < 50f) hpDrain = 0.5f;
                    break;
                case "hot":
                    suitDeg = 1.0f;
                    if (suit < 25f) hpDrain = 1.5f;
                    else if (suit < 50f) hpDrain = 0.5f;
                    break;
                case "extreme_cold":
                    suitDeg = 1.5f;
                    if (suit < 25f) { hpDrain = 3f; speedMult = 0.7f; }
                    else if (suit < 50f) { hpDrain = 1.5f; speedMult = 0.8f; }
                    else if (suit < 75f) hpDrain = 0.5f;
                    break;
                case "extreme_hot":
                    suitDeg = 2.0f;
                    if (suit < 50f) hpDrain = 2f;
                    else if (suit < 75f) hpDrain = 0.5f;
                    break;
                case "ionized":
                    float baseDrain = 1.0f;
                    bool nearMetal = IsPlayerNearMetalTile();
                    float metalMult = nearMetal ? 2.5f : 1f;
                    _player.Battery = MathF.Max(0f, _player.Battery - baseDrain * metalMult * dt);
                    suitDeg = 0.3f;
                    break;
            }

            if (suitDeg > 0f)
                _player.SuitIntegrity = MathF.Max(0f, _player.SuitIntegrity - suitDeg * dt);

            if (hpDrain > 0f)
            {
                _envHpDrainAccum += hpDrain * dt;
                if (_envHpDrainAccum >= 1f)
                {
                    int dmg = (int)_envHpDrainAccum;
                    _envHpDrainAccum -= dmg;
                    _player.Hp = Math.Max(0, _player.Hp - dmg);
                }
            }
            else
            {
                _envHpDrainAccum = 0f;
            }

            _player.EnvSpeedMult = speedMult;
        }

        // Ionized zone visual effects
        if (_currentEnvType == "ionized")
        {
            // Ambient static sparks (2-4 per second)
            if (_rng.NextDouble() < 3.0 * dt)
            {
                _particles.Add(new Particle
                {
                    Position = _camera.Position + new Vector2(_rng.Next(0, ViewW), _rng.Next(0, ViewH)),
                    Velocity = new Vector2(_rng.Next(-30, 30), _rng.Next(-30, 30)),
                    Life = 0.1f + (float)_rng.NextDouble() * 0.1f,
                    MaxLife = 0.2f,
                    Color = _rng.NextDouble() > 0.5 ? new Color(200, 180, 255) : Color.White,
                    Size = _rng.Next(1, 3)
                });
            }

            // Metal tile sparks toward player
            if (IsPlayerNearMetalTile() && _rng.NextDouble() < 4.0 * dt)
            {
                var metalPos = GetNearestMetalTilePos();
                if (metalPos.HasValue)
                {
                    var dir = Vector2.Normalize(_player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f) - metalPos.Value);
                    _particles.Add(new Particle
                    {
                        Position = metalPos.Value + new Vector2(_rng.Next(-12, 12), _rng.Next(-12, 12)),
                        Velocity = dir * (60f + (float)_rng.NextDouble() * 40f) + new Vector2(_rng.Next(-20, 20), _rng.Next(-20, 20)),
                        Life = 0.15f + (float)_rng.NextDouble() * 0.1f,
                        MaxLife = 0.25f,
                        Color = new Color(180, 220, 255),
                        Size = 1
                    });
                }
            }

            // Arc strikes
            _arcSpawnTimer -= dt;
            if (_arcSpawnTimer <= 0f)
            {
                _arcSpawnTimer = 1f + (float)_rng.NextDouble() * 2f;
                Vector2 arcStart, arcEnd;
                var metalPos = GetNearestMetalTilePos();
                if (metalPos.HasValue && _rng.NextDouble() > 0.3)
                {
                    arcStart = metalPos.Value;
                    arcEnd = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f) + new Vector2(_rng.Next(-30, 30), _rng.Next(-30, 30));
                }
                else
                {
                    arcStart = _camera.Position + new Vector2(_rng.Next(20, ViewW - 20), _rng.Next(20, ViewH - 20));
                    arcEnd = arcStart + new Vector2(_rng.Next(-80, 80), _rng.Next(-80, 80));
                }
                int segs = 6;
                float[] offsets = new float[segs];
                for (int i = 0; i < segs; i++) offsets[i] = (float)(_rng.NextDouble() * 16 - 8);
                _arcStrikes.Add(new ArcStrike { Start = arcStart, End = arcEnd, Timer = 0.15f, MaxTimer = 0.15f, Offsets = offsets });
            }

            for (int i = _arcStrikes.Count - 1; i >= 0; i--)
            {
                var a = _arcStrikes[i];
                a.Timer -= dt;
                if (a.Timer <= 0f) { _arcStrikes.RemoveAt(i); continue; }
                _arcStrikes[i] = a;
            }
        }
        else
        {
            _arcStrikes.Clear();
        }

        // Switch interaction (W key near switch)
        // Shelter proximity check
        _nearShelter = false;
        _currentShelter = null;
        _shelterPromptText = null;
        if (_level.Shelters != null)
        {
            var pCenter = _player.Position + new Microsoft.Xna.Framework.Vector2(Player.Width / 2f, Player.Height / 2f);
            foreach (var sh in _level.Shelters)
            {
                float dx = MathF.Abs(pCenter.X - sh.X);
                float dy = MathF.Abs(pCenter.Y - sh.Y);
                if (dx < 40 && dy < 40)
                {
                    _nearShelter = true;
                    _currentShelter = sh;
                    _shelterPromptText = $"[W] Rest at {sh.Name}";
                    break;
                }
            }
        }

        // Shelter rest (hold W near shelter)
        if (_nearShelter && _currentShelter != null && kb.IsKeyDown(Keys.W) && !_dialogueOpen)
        {
            _shelterRestTimer += dt;
            if (_shelterRestTimer >= 0.8f && !_isResting) // hold for 0.8s
            {
                _isResting = true;
                // Save at shelter
                var save = _saveData ?? SaveData.Load() ?? new SaveData();
                save.ShelterLevel = save.CurrentLevel;
                save.ShelterX = _currentShelter.X;
                save.ShelterY = _currentShelter.Y - Player.Height; // spawn above shelter point
                save.SpawnX = _player.Position.X;
                save.SpawnY = _player.Position.Y;
                save.Hp = _player.Hp;
                save.SuitIntegrity = _player.SuitIntegrity;
                save.Battery = _player.Battery;
                save.Save();
                _saveData = save;

                // Heal + feedback
                _player.Hp = _player.MaxHp;
                _shakeTimer = 0.1f; _shakeIntensity = 1f;
                EveAlertOnce("Shelter secured. Biosigns stabilizing.", 3f);

                // Fade effect
                _restFadeAlpha = 1f;
            }
        }
        else
        {
            _shelterRestTimer = 0;
            _isResting = false;
        }
        if (_restFadeAlpha > 0) _restFadeAlpha -= dt * 0.8f;

        if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W) && !_dialogueOpen)
        {
            var pRect = new Rectangle((int)_player.Position.X - 10, (int)_player.Position.Y, Player.Width + 20, Player.Height);
            foreach (var sw in _level.Switches)
            {
                bool isToggle = sw.Action.StartsWith("toggle-") || sw.Action.StartsWith("grant-");
                if (!isToggle && _activatedSwitches.Contains(sw.Id)) continue;
                var swRect = new Rectangle((int)sw.X, (int)sw.Y, sw.W, sw.H);
                if (pRect.Intersects(swRect))
                {
                    if (!isToggle) _activatedSwitches.Add(sw.Id);
                    // Execute switch action
                    switch (sw.Action)
                    {
                        case "unfreeze-crawlers":
                            foreach (var c in _crawlers) c.Frozen = false;
                            break;
                        case "toggle-rain":
                            _weatherRain = !_weatherRain;
                            if (!_weatherRain) _weatherStorm = false;
                            if (_eveOrbActive) EveAlert(_weatherRain ? "Atmospheric moisture detected. Rain incoming." : "Rain subsiding.", 2.5f);
                            break;
                        case "toggle-storm":
                            _weatherStorm = !_weatherStorm;
                            if (_weatherStorm) _weatherRain = true;
                            if (_eveOrbActive) EveAlert(_weatherStorm ? "Electrical storm detected! Seek shelter!" : "Storm clearing.", 2.5f);
                            break;
                        case "toggle-wind":
                            _weatherWind = !_weatherWind;
                            if (_eveOrbActive) EveAlert(_weatherWind ? "High wind advisory. Watch your footing." : "Wind died down.", 2.5f);
                            break;
                        case "toggle-all-weather":
                            bool allOn = _weatherRain && _weatherStorm && _weatherWind;
                            _weatherRain = !allOn;
                            _weatherStorm = !allOn;
                            _weatherWind = !allOn;
                            if (_eveOrbActive) EveAlert(!allOn ? "Full atmospheric event! Incredible!" : "Systems normalizing.", 3f);
                            break;
                        // --- Ability shrine toggles ---
                        case "grant-slide": _enableSlide = !_enableSlide;
                            if (_eveOrbActive) EveAlert(_enableSlide ? "Slide technique acquired!" : "Slide technique disabled.", 2f); break;
                        case "grant-dash": _enableDash = !_enableDash;
                            if (_eveOrbActive) EveAlert(_enableDash ? "Sprint module online!" : "Sprint module offline.", 2f); break;
                        case "grant-double-jump": _enableDoubleJump = !_enableDoubleJump;
                            if (_eveOrbActive) EveAlert(_enableDoubleJump ? "Aerial boost initialized!" : "Aerial boost offline.", 2f); break;
                        case "grant-wall-climb": _enableWallClimb = !_enableWallClimb;
                            if (_eveOrbActive) EveAlert(_enableWallClimb ? "Wall grip engaged!" : "Wall grip disengaged.", 2f); break;
                        case "grant-drop-through": _enableDropThrough = !_enableDropThrough;
                            if (_eveOrbActive) EveAlert(_enableDropThrough ? "Platform phase enabled!" : "Platform phase disabled.", 2f); break;
                        case "grant-vault-kick": _enableVaultKick = !_enableVaultKick;
                            if (_eveOrbActive) EveAlert(_enableVaultKick ? "Vault kick unlocked!" : "Vault kick locked.", 2f); break;
                        case "grant-uppercut": _enableUppercut = !_enableUppercut;
                            if (_eveOrbActive) EveAlert(_enableUppercut ? "Rising strike acquired!" : "Rising strike disabled.", 2f); break;
                        case "grant-cartwheel": _enableCartwheel = !_enableCartwheel;
                            if (_eveOrbActive) EveAlert(_enableCartwheel ? "Evasive roll online!" : "Evasive roll offline.", 2f); break;
                        case "grant-flip": _enableFlip = !_enableFlip;
                            if (_eveOrbActive) EveAlert(_enableFlip ? "Aerial flip mastered!" : "Aerial flip disabled.", 2f); break;
                        case "grant-blade-dash": _enableBladeDash = !_enableBladeDash;
                            if (_eveOrbActive) EveAlert(_enableBladeDash ? "Blade dash technique learned!" : "Blade dash technique locked.", 2f); break;
                        case "grant-spin-melee": _enableSpinMelee = !_enableSpinMelee;
                            if (_eveOrbActive) EveAlert(_enableSpinMelee ? "Spin attack activated!" : "Spin attack deactivated.", 2f); break;
                        case "grant-rope-climb": _enableRopeClimb = !_enableRopeClimb;
                            if (_eveOrbActive) EveAlert(_enableRopeClimb ? "Rope ascension enabled!" : "Rope ascension disabled.", 2f); break;
                    }
                    // Screen shake + particles for feedback
                    if (_screenShakeEnabled) { _shakeTimer = 0.15f; _shakeIntensity = 3f; }
                    SpawnDustParticles(new Vector2(sw.X + sw.W / 2f, sw.Y + sw.H / 2f), 10);
                    break;
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
                        case "knife": EquipMelee(WeaponType.Knife); break;
                        case "grapple": _player.HasGrapple = true; EveAlert("Partially functional. Battery drain, but better than nothing.", 4f); break;
                        case "map-module": _hasMapModule = true; EveAlert("Cartographic module. Damaged, but I can compensate.", 4f); break;
                        case "stick": EquipMelee(WeaponType.Stick); break;
                        case "whip": EquipMelee(WeaponType.Whip); break;
                        case "dagger": EquipMelee(WeaponType.Dagger); break;
                        case "sword": EquipMelee(WeaponType.Sword); break;
                        case "greatsword": EquipMelee(WeaponType.GreatSword); break;
                        case "axe": EquipMelee(WeaponType.Axe); break;
                        case "club": EquipMelee(WeaponType.Club); break;
                        case "greatclub": EquipMelee(WeaponType.GreatClub); break;
                        case "hammer": EquipMelee(WeaponType.Hammer); break;
                        case "woodshield": EquipMelee(WeaponType.WoodShield); break;
                        case "sling": EquipRanged(WeaponType.Sling); break;
                        case "bow": EquipRanged(WeaponType.Bow); break;
                        case "gun": EquipRanged(WeaponType.Gun); break;
                        case "heart":
                            _player.Hp = Math.Min(_player.Hp + 3, _player.MaxHp);
                            break;
                    }
                    break; // pick up one at a time
                }
            }
            // Auto-save after pickup
            if (_saveData != null) { SyncInventoryToSave(); _saveData.Save(); }
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
                        _lastDamageSource = "Spikes";
                        _player.TakeDamage(3, _player.Position.X - spike.Center.X);
                        if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
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
                        case TileType.RetractSpikesUp:
                        case TileType.RetractSpikesDown:
                        case TileType.RetractSpikesLeft:
                        case TileType.RetractSpikesRight:
                            if (!tgi.RetractExtended) continue;
                            spikeRect = new Rectangle(twx, twy, ts, ts);
                            break;
                        case TileType.RetractHalfSpikesUp:
                            if (!tgi.RetractExtended) continue;
                            spikeRect = new Rectangle(twx, twy + ts / 2, ts, ts / 2); break;
                        case TileType.RetractHalfSpikesDown:
                            if (!tgi.RetractExtended) continue;
                            spikeRect = new Rectangle(twx, twy, ts, ts / 2); break;
                        case TileType.RetractHalfSpikesLeft:
                            if (!tgi.RetractExtended) continue;
                            spikeRect = new Rectangle(twx + ts / 2, twy, ts / 2, ts); break;
                        case TileType.RetractHalfSpikesRight:
                            if (!tgi.RetractExtended) continue;
                            spikeRect = new Rectangle(twx, twy, ts / 2, ts); break;
                        default: continue;
                    }
                    
                    if (pRect.Intersects(spikeRect))
                    {
                        _lastDamageSource = "Spikes";
                        _player.TakeDamage(3, _player.Position.X - spikeRect.Center.X);
                        if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
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
                                    _lastDamageSource = "Hazard";
                                    _player.TakeDamage(1, _player.Position.X - tileRect.Center.X);
                                    if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
                                }
                                break;
                            case TileType.DamageNoKBTile:
                            case TileType.DamageFloorTile:
                                if (_spawnInvincibility <= 0f)
                                {
                                    // Damage without knockback — just reduce HP directly
                                    if (_player.DamageCooldown <= 0)
                                    {
                                        _lastDamageSource = "Hazard";
                                        _player.Hp -= 5;
                                        _player.DamageCooldown = 1.0f;
                                        if (_player.Hp <= 0) { _player.Hp = 0; _isDead = true; LogDeath(); }
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
                            case TileType.Fire:
                                if (_spawnInvincibility <= 0f)
                                {
                                    _lastDamageSource = "Fire";
                                    _player.TakeDamage(1, _player.Position.X - tileRect.Center.X);
                                    if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
                                }
                                break;
                            case TileType.ElectricShock:
                                // Intermittent: active when retract spikes are extended
                                if (_level.TileGridInstance.RetractExtended && _spawnInvincibility <= 0f)
                                {
                                    _lastDamageSource = "Electric shock";
                                    _player.TakeDamage(2, _player.Position.X - tileRect.Center.X);
                                    if (_player.Hp <= 0) { _isDead = true; LogDeath(); }
                                }
                                break;
                            case TileType.Puddle:
                                _player.PuddleSlowTimer = 0.2f; // refreshed each frame while in puddle
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
                        var bt = tgi.GetTileAt(col, row);
                        if (bt == TileType.Breakable || bt == TileType.BreakableGlass || bt == TileType.BreakableBattery)
                        {
                            _destroyedBreakables.Add((col, row, bt));
                            tgi.SetTileAt(col, row, TileType.Empty);
                            _level.RebuildTileCollision();
                            int twx = ox + col * ts, twy = oy + row * ts;
                            if (bt == TileType.Breakable)
                            {
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
                            else if (bt == TileType.BreakableBattery)
                            {
                                _itemPickups.Add(new ItemPickup
                                {
                                    Id = $"battery-break-{twx}-{twy}",
                                    X = twx + ts / 2f - 8,
                                    Y = twy,
                                    W = 16, H = 16,
                                    ItemType = "battery",
                                    Collected = false,
                                    HasGravity = true,
                                    VelY = -150f
                                });
                            }
                            // BreakableGlass shatters with no drop
                        }
                    }
                }
            }
        }

        // Breakable tile destruction (uppercut)
        if (_player.IsUppercutting)
        {
            var uppercutRect = _player.UppercutHitbox;
            var tgi = _level.TileGridInstance;
            if (tgi != null)
            {
                int ts = tgi.TileSize;
                int ox = tgi.OriginX, oy = tgi.OriginY;
                int startCol = Math.Max(0, (uppercutRect.Left - ox) / ts);
                int endCol = Math.Min(tgi.Width - 1, (uppercutRect.Right - ox) / ts);
                int startRow = Math.Max(0, (uppercutRect.Top - oy) / ts);
                int endRow = Math.Min(tgi.Height - 1, (uppercutRect.Bottom - oy) / ts);
                for (int row = startRow; row <= endRow; row++)
                {
                    for (int col = startCol; col <= endCol; col++)
                    {
                        var bt = tgi.GetTileAt(col, row);
                        if (bt == TileType.Breakable || bt == TileType.BreakableGlass || bt == TileType.BreakableBattery)
                        {
                            _destroyedBreakables.Add((col, row, bt));
                            tgi.SetTileAt(col, row, TileType.Empty);
                            _level.RebuildTileCollision();
                            int twx = ox + col * ts, twy = oy + row * ts;
                            if (bt == TileType.Breakable)
                            {
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
                            }
                            else if (bt == TileType.BreakableBattery)
                            {
                                _itemPickups.Add(new ItemPickup
                                {
                                    Id = $"battery-break-{twx}-{twy}",
                                    X = twx + ts / 2f - 8,
                                    Y = twy,
                                    W = 16, H = 16,
                                    ItemType = "battery",
                                    Collected = false,
                                    HasGravity = true,
                                    VelY = -150f
                                });
                            }
                        }
                    }
                }
            }

            // Uppercut hits enemies
            int uppercutDmg = 15;
            DamageCreaturesInRect(uppercutRect, uppercutDmg, _player.FacingDir * 200f, -300f, _player.FacingDir);
        }

        // Vault kick enemy damage
        if (_player.IsVaultKicking)
        {
            var vkRect = _player.VaultKickHitbox;
            int vkDmg = 12;
            float vkKbX = _player.FacingDir * 250f;
            float vkKbY = -200f;
            DamageCreaturesInRect(vkRect, vkDmg, vkKbX, vkKbY, _player.FacingDir, false);
        }

        // === Edge-of-screen room transitions (Metroid style) ===
        // Only transitions where tiles are open at the edge (not through solid walls)
        if (!_isDead && !_roomTransitioning && _level.Neighbors != null)
        {
            var bounds = _level.Bounds;
            float px = _player.Position.X;
            float py = _player.Position.Y;
            float pcx = px + Player.Width / 2f;
            float pcy = py + Player.Height / 2f;
            string neighbor = null;
            string dir = null;
            
            var tg = _level.TileGridInstance;
            float boundsW = bounds.Right - bounds.Left;
            float boundsH = bounds.Bottom - bounds.Top;
            
            if (px < bounds.Left - 4 && _level.Neighbors.HasNeighbor("left"))
            {
                int checkX = bounds.Left + 2;
                bool open = tg == null || !TileProperties.IsSolid(tg.GetTile(checkX, (int)pcy));
                float edgePos = boundsH > 0 ? (pcy - bounds.Top) / boundsH : 0.5f;
                string resolved = _level.Neighbors.ResolveEdge("left", edgePos);
                if (open && resolved != "") { neighbor = resolved; dir = "left"; }
            }
            else if (px + Player.Width > bounds.Right + 4 && _level.Neighbors.HasNeighbor("right"))
            {
                int checkX = bounds.Right - 2;
                bool open = tg == null || !TileProperties.IsSolid(tg.GetTile(checkX, (int)pcy));
                float edgePos = boundsH > 0 ? (pcy - bounds.Top) / boundsH : 0.5f;
                string resolved = _level.Neighbors.ResolveEdge("right", edgePos);
                if (open && resolved != "") { neighbor = resolved; dir = "right"; }
            }
            else if (py < bounds.Top - 4 && _level.Neighbors.HasNeighbor("up"))
            {
                int checkY = bounds.Top + 2;
                bool open = tg == null || !TileProperties.IsSolid(tg.GetTile((int)pcx, checkY));
                float edgePos = boundsW > 0 ? (pcx - bounds.Left) / boundsW : 0.5f;
                string resolved = _level.Neighbors.ResolveEdge("up", edgePos);
                if (open && resolved != "") { neighbor = resolved; dir = "up"; }
            }
            else if (py + Player.Height > bounds.Bottom + 4 && _level.Neighbors.HasNeighbor("down"))
            {
                int checkY = bounds.Bottom - 2;
                bool open = tg == null || !TileProperties.IsSolid(tg.GetTile((int)pcx, checkY));
                float edgePos = boundsW > 0 ? (pcx - bounds.Left) / boundsW : 0.5f;
                string resolved = _level.Neighbors.ResolveEdge("down", edgePos);
                if (open && resolved != "") { neighbor = resolved; dir = "down"; }
            }
            
            if (neighbor != null)
            {
                float savedAbsY = py;
                float savedAbsX = px;
                float savedVelX = _player.Velocity.X;
                float savedVelY = _player.Velocity.Y;
                
                string nextPath = $"Content/levels/{neighbor}.json";
                if (System.IO.File.Exists(nextPath))
                {
                    // Save old camera position for pan start
                    _roomTransCamStart = _camera.Position;
                    _roomTransDir = dir;
                    
                    // Calculate where the player will walk FROM (just inside the edge)
                    // and the old viewport size for camera offset
                    float oldViewW = _camera.EffectiveViewW;
                    float oldViewH = _camera.EffectiveViewH;
                    
                    if (_saveData != null) { SyncInventoryToSave(); _saveData.Save(); }
                    LoadLevel(nextPath);
                    _editorSaveFile = nextPath;
                    _camera = MakeCamera();
                    Restart();
                    
                    var nb = _level.Bounds;
                    
                    // Player spawn on opposite edge
                    switch (dir)
                    {
                        case "left":
                            _roomTransPlayerEnd = new Vector2(nb.Right - Player.Width - 32, savedAbsY);
                            _roomTransPlayerStart = new Vector2(nb.Right + 8, savedAbsY);
                            break;
                        case "right":
                            _roomTransPlayerEnd = new Vector2(nb.Left + 32, savedAbsY);
                            _roomTransPlayerStart = new Vector2(nb.Left - Player.Width - 8, savedAbsY);
                            break;
                        case "up":
                            _roomTransPlayerEnd = new Vector2(savedAbsX, nb.Bottom - Player.Height - 32);
                            _roomTransPlayerStart = new Vector2(savedAbsX, nb.Bottom + 8);
                            break;
                        case "down":
                            _roomTransPlayerEnd = new Vector2(savedAbsX, nb.Top + 32);
                            _roomTransPlayerStart = new Vector2(savedAbsX, nb.Top - Player.Height - 8);
                            break;
                    }
                    
                    // Clamp to new room bounds
                    _roomTransPlayerEnd = new Vector2(
                        MathHelper.Clamp(_roomTransPlayerEnd.X, nb.Left + 4, nb.Right - Player.Width - 4),
                        MathHelper.Clamp(_roomTransPlayerEnd.Y, nb.Top + 4, nb.Bottom - Player.Height - 4));
                    
                    // Camera end: centered on player end position
                    float evw = _camera.EffectiveViewW;
                    float evh = _camera.EffectiveViewH;
                    _roomTransCamEnd = new Vector2(
                        MathHelper.Clamp(_roomTransPlayerEnd.X + Player.Width / 2f - evw / 2f,
                            nb.Left, Math.Max(nb.Left, nb.Right - evw)),
                        MathHelper.Clamp(_roomTransPlayerEnd.Y + Player.Height / 2f - evh / 2f,
                            nb.Top, Math.Max(nb.Top, nb.Bottom - evh)));
                    
                    // Camera start: offset from end by one viewport in the transition direction
                    switch (dir)
                    {
                        case "left":
                            _roomTransCamStart = new Vector2(_roomTransCamEnd.X + evw, _roomTransCamEnd.Y);
                            break;
                        case "right":
                            _roomTransCamStart = new Vector2(_roomTransCamEnd.X - evw, _roomTransCamEnd.Y);
                            break;
                        case "up":
                            _roomTransCamStart = new Vector2(_roomTransCamEnd.X, _roomTransCamEnd.Y + evh);
                            break;
                        case "down":
                            _roomTransCamStart = new Vector2(_roomTransCamEnd.X, _roomTransCamEnd.Y - evh);
                            break;
                    }
                    
                    // Duration scales with room size (bigger rooms = slightly slower pan)
                    float roomSize = (dir == "left" || dir == "right") ? evw : evh;
                    _roomTransDuration = MathHelper.Clamp(roomSize / 800f * 0.5f, 0.3f, 0.8f);
                    _roomTransFirstVisit = false; // TODO: track visited rooms
                    _roomTransFlicker = false;     // TODO: per-room flag from level data
                    
                    _roomTransTimer = 0f;
                    _roomTransitioning = true;
                    _player.Position = _roomTransPlayerStart;
                    _player.Velocity = new Vector2(savedVelX, savedVelY);
                    _camera.Position = _roomTransCamStart;
                    _gameState = GameState.Playing;
                }
            }
        }
        
        // Update room transition pan
        if (_roomTransitioning)
        {
            _roomTransTimer += dt;
            float t = Math.Min(1f, _roomTransTimer / _roomTransDuration);
            
            // Ease-in-out (smoothstep)
            float st = t * t * (3f - 2f * t);
            
            // Pan camera
            _camera.Position = Vector2.Lerp(_roomTransCamStart, _roomTransCamEnd, st);
            
            // Slide player in from edge
            _player.Position = Vector2.Lerp(_roomTransPlayerStart, _roomTransPlayerEnd, st);
            _player.Velocity = Vector2.Zero; // frozen during pan
            
            // Tech suppression flicker (random scanline offsets near end of pan)
            // TODO: implement when _roomTransFlicker is true
            
            if (t >= 1f)
            {
                _roomTransitioning = false;
                _camera.SnapTo(_player.Position, Player.Width, Player.Height);
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
                        // Save current player state BEFORE Restart() destroys the player
                        if (_saveData != null) { SyncInventoryToSave(); _saveData.Save(); }
                        
                        LoadLevel(nextPath);
                        _editorSaveFile = nextPath;
                        _camera = MakeCamera();
                        Restart();

                        // Restore player state from save data (Restart() creates new Player)
                        if (_saveData != null)
                        {
                            _player.HasGrapple = _saveData.CollectedItems?.Any(id => id.StartsWith("grapple")) == true;
                            _hasMapModule = _saveData.CollectedItems?.Any(id => id.StartsWith("map-module")) == true;
                            _player.CurrentTier = (Player.MoveTier)Math.Clamp(_saveData.MoveTier, 0, 2);
                            _player.ApplyTierConstants();
                            _wakeUpComplete = true;
                            _player.IsLyingDown = false;
                            // Restore inventory
                            LoadInventoryFromSave();
                            if (_saveData.Flags.ContainsKey("eveOrbActive"))
                                _eveOrbActive = _saveData.Flags["eveOrbActive"];
                        }

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
    // --- Left/Right hand weapon helpers ---
    private void AddWeaponToInventory(WeaponType w)
    {
        if (!_weaponInventory.Contains(w)) _weaponInventory.Add(w);
        // Auto-equip to first empty hand slot
        if ((_rightActiveSlot1 ? _rightHand1 : _rightHand2) == WeaponType.None)
        {
            if (_rightActiveSlot1) _rightHand1 = w;
            else _rightHand2 = w;
        }
        else if ((_leftActiveSlot1 ? _leftHand1 : _leftHand2) == WeaponType.None)
        {
            if (_leftActiveSlot1) _leftHand1 = w;
            else _leftHand2 = w;
        }
        else if ((!_rightActiveSlot1 ? _rightHand1 : _rightHand2) == WeaponType.None)
        {
            if (!_rightActiveSlot1) _rightHand1 = w;
            else _rightHand2 = w;
        }
        else if ((!_leftActiveSlot1 ? _leftHand1 : _leftHand2) == WeaponType.None)
        {
            if (!_leftActiveSlot1) _leftHand1 = w;
            else _leftHand2 = w;
        }
        // else just in inventory, player equips manually
    }

    private void RemoveWeaponFromInventory(WeaponType w)
    {
        _weaponInventory.Remove(w);
        if (_rightHand1 == w) _rightHand1 = WeaponType.None;
        if (_rightHand2 == w) _rightHand2 = WeaponType.None;
        if (_leftHand1 == w) _leftHand1 = WeaponType.None;
        if (_leftHand2 == w) _leftHand2 = WeaponType.None;
    }

    // Legacy helpers — now route through unified inventory
    private void EquipMelee(WeaponType w) => AddWeaponToInventory(w);
    private void UnequipMelee(WeaponType w) => RemoveWeaponFromInventory(w);
    private void EquipRanged(WeaponType w) => AddWeaponToInventory(w);
    private void UnequipRanged(WeaponType w) => RemoveWeaponFromInventory(w);

    private void LoadInventoryFromSave()
    {
        if (_saveData == null) return;
        _saveData.MigrateWeapons();
        _weaponInventory = _saveData.WeaponInventory.ConvertAll(s => Enum.TryParse<WeaponType>(s, out var wt) ? wt : WeaponType.None);
        _weaponInventory.RemoveAll(w => w == WeaponType.None);
        _rightHand1 = Enum.TryParse<WeaponType>(_saveData.RightHand1, out var rh1) ? rh1 : WeaponType.None;
        _rightHand2 = Enum.TryParse<WeaponType>(_saveData.RightHand2, out var rh2) ? rh2 : WeaponType.None;
        _leftHand1 = Enum.TryParse<WeaponType>(_saveData.LeftHand1, out var lh1) ? lh1 : WeaponType.None;
        _leftHand2 = Enum.TryParse<WeaponType>(_saveData.LeftHand2, out var lh2) ? lh2 : WeaponType.None;
        _rightActiveSlot1 = _saveData.RightActiveSlot1;
        _leftActiveSlot1 = _saveData.LeftActiveSlot1;
    }

    private void ClearInventory()
    {
        _weaponInventory.Clear();
        _rightHand1 = WeaponType.None;
        _rightHand2 = WeaponType.None;
        _leftHand1 = WeaponType.None;
        _leftHand2 = WeaponType.None;
        _rightActiveSlot1 = true;
        _leftActiveSlot1 = true;
    }

    // CurrentMelee = whichever active hand has a melee weapon (prefer left for melee)
    private WeaponType CurrentMelee
    {
        get
        {
            if (ActiveLeft != WeaponType.None && !WeaponStats.Get(ActiveLeft).IsRanged && !WeaponStats.Get(ActiveLeft).IsShield) return ActiveLeft;
            if (ActiveRight != WeaponType.None && !WeaponStats.Get(ActiveRight).IsRanged && !WeaponStats.Get(ActiveRight).IsShield) return ActiveRight;
            return WeaponType.None;
        }
    }

    // CurrentRanged = whichever active hand has a ranged weapon (prefer right for ranged)
    private WeaponType CurrentRanged
    {
        get
        {
            if (ActiveRight != WeaponType.None && WeaponStats.Get(ActiveRight).IsRanged) return ActiveRight;
            if (ActiveLeft != WeaponType.None && WeaponStats.Get(ActiveLeft).IsRanged) return ActiveLeft;
            return WeaponType.None;
        }
    }

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

    private void LogDeath()
    {
        if (!_deathLogEnabled) return;
        string levelName = _level?.Name ?? "unknown";
        _deathLog.Add($"Player was slain by {_lastDamageSource} in {levelName}");
        _deathLogTimers.Add(8f);
        if (_deathLog.Count > 5) { _deathLog.RemoveAt(0); _deathLogTimers.RemoveAt(0); }
    }

    private void DrawParallaxBackground(Matrix shakeOff)
    {
        if (_parallaxLayers == null || _parallaxLayers.Length == 0) return;
        float camX = _camera.Position.X;
        float[] factors = _parallaxLayers.Length switch
        {
            1 => new[] { 0.1f },
            2 => new[] { 0.1f, 0.4f },
            3 => new[] { 0.05f, 0.25f, 0.5f },
            4 => new[] { 0.05f, 0.15f, 0.35f, 0.6f },
            5 => new[] { 0.02f, 0.1f, 0.25f, 0.45f, 0.7f },
            _ => new[] { 0.02f, 0.1f, 0.25f, 0.45f, 0.7f },
        };
        for (int i = 0; i < _parallaxLayers.Length && i < factors.Length; i++)
        {
            var tex = _parallaxLayers[i];
            if (tex == null) continue;
            float px = camX * factors[i];
            int tw = tex.Width;
            int th = tex.Height;
            // Anchor layer to bottom of screen
            int drawY = ViewH - th;
            int startX = (int)(-px % tw);
            if (startX > 0) startX -= tw;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            for (int x = startX; x < ViewW; x += tw)
            {
                _spriteBatch.Draw(tex, new Rectangle(x, drawY, tw, th), Color.White);
            }
            _spriteBatch.End();
        }
    }

    private void SpawnDeathParticles(Vector2 center, Color color, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float speed = 80f + (float)(_rng.NextDouble() * 200f);
            float life = 0.3f + (float)(_rng.NextDouble() * 0.4f);
            _particles.Add(new Particle
            {
                Position = center,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 100f),
                Life = life,
                MaxLife = life,
                Color = color,
                Size = 2 + _rng.Next(3)
            });
        }
    }

    private void SpawnDustParticles(Vector2 position, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float vx = (float)(_rng.NextDouble() * 160 - 80);
            float vy = -(float)(_rng.NextDouble() * 20 + 5); // mostly horizontal, slight upward drift
            float life = 0.2f + (float)(_rng.NextDouble() * 0.25f);
            _particles.Add(new Particle
            {
                Position = position,
                Velocity = new Vector2(vx, vy),
                Life = life,
                MaxLife = life,
                Color = new Color(180, 170, 150),
                Size = 2 + _rng.Next(2)
            });
        }
    }

    private static Color GetEnemyHitColor(string type) => type switch
    {
        "crawler" => new Color(120, 60, 20),
        "dummy" => new Color(140, 100, 160),
        "hopper" => new Color(100, 80, 60),
        "thornback" => new Color(60, 100, 30),
        "bird" => new Color(80, 120, 160),
        "wingbeater" => new Color(160, 60, 40),
        "swarm" => Color.OrangeRed,
        _ => new Color(200, 50, 50)
    };

    private static string GetCreatureTypeName(Creature c) => c switch
    {
        Crawler cr => cr.IsDummy ? "dummy" : "crawler",
        Bird => "bird",
        Wingbeater => "wingbeater",
        Hopper => "hopper",
        Thornback => "thornback",
        _ => "creature"
    };

    /// <summary>Apply area damage to all creatures intersecting a hitbox. Returns number hit.</summary>
    private int DamageCreaturesInRect(Rectangle hitbox, int damage, float kbX, float kbY, int facingDir, bool hitStop = true)
    {
        int hitCount = 0;
        foreach (var c in _creatures)
        {
            if (!c.Alive) continue;
            if (c is Crawler cr && cr.IsLatched) continue;
            if (!hitbox.Intersects(c.Rect)) continue;
            
            int prevHp = c.Hp;
            bool killed = _knockbackEnabled
                ? c.TakeHit(damage, kbX, kbY)
                : c.TakeHit(damage);
            if (c.Hp < prevHp || killed)
            {
                var hitPt = new Vector2(c.Position.X + c.Rect.Width / 2f, c.Position.Y + c.Rect.Height / 2f);
                SpawnHitSpray(hitPt, facingDir, GetEnemyHitColor(GetCreatureTypeName(c)), 1, false);
                if (hitStop && _hitStopEnabled) _hitStopTimer = 0.04f;
                hitCount++;
            }
        }
        return hitCount;
    }

    private int ApplyScanBonus(int dmg, string enemyType)
    {
        if (_scanProgress.TryGetValue(enemyType, out int lvl) && lvl >= 3)
            return (int)(dmg * 1.25f);
        return dmg;
    }

    private void SpawnHitSpray(Vector2 hitPoint, int facingDir, Color color, float weaponWeight, bool isFinisher)
    {
        int count = (int)MathHelper.Clamp(3 + weaponWeight * 5, 3, 8);
        if (isFinisher) count *= 2;
        float baseSpeed = 120f + weaponWeight * 180f;

        for (int i = 0; i < count; i++)
        {
            float angle = facingDir > 0 ? 0f : MathF.PI;
            angle += (float)(_rng.NextDouble() - 0.5) * MathF.PI * 0.5f;
            float speed = baseSpeed * (0.6f + (float)_rng.NextDouble() * 0.8f);
            float life = 0.15f + (float)_rng.NextDouble() * 0.15f;
            if (isFinisher) life *= 1.3f;

            _particles.Add(new Particle
            {
                Position = hitPoint + new Vector2((float)(_rng.NextDouble() * 6 - 3), (float)(_rng.NextDouble() * 6 - 3)),
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 40f),
                Life = life,
                MaxLife = life,
                Color = color,
                Size = 1 + _rng.Next(isFinisher ? 3 : 2)
            });
        }

        int sparkCount = isFinisher ? 3 : 1 + _rng.Next(2);
        for (int i = 0; i < sparkCount; i++)
        {
            float angle = (float)(_rng.NextDouble() * MathF.PI * 2);
            float speed = 60f + (float)(_rng.NextDouble() * 100f);
            float life = 0.08f + (float)(_rng.NextDouble() * 0.07f);
            _particles.Add(new Particle
            {
                Position = hitPoint,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 50f),
                Life = life,
                MaxLife = life,
                Color = new Color(255, 255, (int)(180 + _rng.NextDouble() * 75)),
                Size = 1 + _rng.Next(2)
            });
        }

        // Crush weapons spawn dust cloud
        bool isCrush = _player.CurrentWeapon is WeaponType.Club or WeaponType.GreatClub or WeaponType.Hammer;
        if (isCrush)
            SpawnDustParticles(hitPoint, isFinisher ? 6 : 3);
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

    private void DrawOutlinedString(SpriteFontBase font, string text, Vector2 position, Color color, Color outline = default)
    {
        if (string.IsNullOrEmpty(text)) return;
        text = SafeText(text);
        if (outline == default) outline = new Color(0, 0, 0, 200);
        for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
                if (ox != 0 || oy != 0)
                    _spriteBatch.DrawString(font, text, position + new Vector2(ox, oy), outline);
        _spriteBatch.DrawString(font, text, position, color);
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

        // Check time/underground buttons first (before tile placement consumes click)
        _editorTimeBtnClicked = false;
        UpdateEditorTimeButtons();
        // Ctrl+Z = undo
        if ((kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)) && kb.IsKeyDown(Keys.Z) && _prevKb.IsKeyUp(Keys.Z))
            EditorUndo();

        // --- Item placement palette (P key in editor) ---
        if (kb.IsKeyDown(Keys.P) && _prevKb.IsKeyUp(Keys.P))
        {
            _itemPaletteOpen = !_itemPaletteOpen;
            if (_itemPaletteOpen) { _itemPaletteCursor = 0; _entityPaletteOpen = false; }
            return;
        }

        if (_itemPaletteOpen)
        {
            if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                _itemPaletteCursor = (_itemPaletteCursor - 1 + ItemPaletteTypes.Length) % ItemPaletteTypes.Length;
            if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                _itemPaletteCursor = (_itemPaletteCursor + 1) % ItemPaletteTypes.Length;
            if (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter) ||
                kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space))
            {
                int _eox = _level.TileGridInstance?.OriginX ?? 0;
                int _eoy = _level.TileGridInstance?.OriginY ?? 0;
                float cx = _editorGridSnap ? _eox + MathF.Floor((_editorCursor.X - _eox) / 32) * 32 : _editorCursor.X;
                float cy = _editorGridSnap ? _eoy + MathF.Floor((_editorCursor.Y - _eoy) / 32) * 32 : _editorCursor.Y;
                string itype = ItemPaletteTypes[_itemPaletteCursor];
                if (itype == "shelter")
                {
                    // Snap shelter to ground surface (use height=0 so Y = actual ground level)
                    float groundY = SnapToSurface(cx + 16, cy, 4, 0);
                    if (groundY > 9000) groundY = cy; // fallback
                    var shelterList = new List<ShelterData>(_level.Shelters ?? Array.Empty<ShelterData>());
                    shelterList.Add(new ShelterData { Id = $"shelter-{shelterList.Count}", X = cx + 16, Y = groundY, Name = "Leaf Shelter" });
                    _level.Shelters = shelterList.ToArray();
                    SetEditorStatus($"Placed shelter at ({(int)cx}, {(int)groundY})");
                }
                else
                {
                    var itemList = new List<ItemData>(_level.Items);
                    itemList.Add(new ItemData { Id = $"{itype}-{itemList.Count}", Type = itype, X = cx, Y = cy });
                    _level.Items = itemList.ToArray();
                    SetEditorStatus($"Placed item: {itype} at ({(int)cx}, {(int)cy})");
                }
                _itemPaletteOpen = false;
                SaveLevel();
            }
            if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                _itemPaletteOpen = false;
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
            else if (_itemPaletteOpen)
            {
                _itemPaletteOpen = false;
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
            _enemyCategoryCursor = 0;
            _enemyVariantCursor = 0;
            _enemyVariantExpanded = false;
        }

        // Entity palette input — accordion style
        if (_entityPaletteOpen)
        {
            if (_enemyVariantExpanded)
            {
                // Navigating variants within a category
                var variants = EnemyCategories[_enemyCategoryCursor].variants;
                if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                    _enemyVariantCursor = (_enemyVariantCursor - 1 + variants.Length) % variants.Length;
                if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                    _enemyVariantCursor = (_enemyVariantCursor + 1) % variants.Length;
                if (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left) ||
                    kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A))
                    _enemyVariantExpanded = false; // collapse back to categories
            }
            else
            {
                // Navigating categories
                if (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W))
                    _enemyCategoryCursor = (_enemyCategoryCursor - 1 + EnemyCategories.Length) % EnemyCategories.Length;
                if (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S))
                    _enemyCategoryCursor = (_enemyCategoryCursor + 1) % EnemyCategories.Length;
                if (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right) ||
                    kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D))
                {
                    if (EnemyCategories[_enemyCategoryCursor].variants.Length > 1)
                    {
                        _enemyVariantExpanded = true;
                        _enemyVariantCursor = 0;
                    }
                }
            }

            bool place = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) ||
                         (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space)) ||
                         (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released);
            if (place)
            {
                string etype = SelectedEnemyType;
                int _eox = _level.TileGridInstance?.OriginX ?? 0;
                int _eoy = _level.TileGridInstance?.OriginY ?? 0;
                var worldMouse2 = Vector2.Transform(new Vector2(Mouse.GetState().X, Mouse.GetState().Y), Matrix.Invert(_camera.TransformMatrix));
                float cx = _editorGridSnap ? _eox + MathF.Floor((worldMouse2.X - _eox) / 32) * 32 : worldMouse2.X;
                float cy = _editorGridSnap ? _eoy + MathF.Floor((worldMouse2.Y - _eoy) / 32) * 32 : worldMouse2.Y;

                if (etype == "tree")
                {
                    var oList = new List<EnvObjectData>(_level.Objects);
                    float treeSnapY = SnapToSurface(cx, cy, 40, 80);
                    oList.Add(new EnvObjectData { Id = $"tree-{oList.Count}", Type = "tree", X = cx, Y = treeSnapY, W = 40, H = 80 });
                    _level.Objects = oList.ToArray();
                    SetEditorStatus($"Placed tree");
                }
                else
                {
                    var eList = new List<EnemySpawnData>(_level.Enemies);
                    eList.Add(new EnemySpawnData { Id = $"{etype}-{eList.Count}", Type = etype, X = cx, Y = cy, Count = etype == "swarm" ? 10 : 0 });
                    _level.Enemies = eList.ToArray();
                    SetEditorStatus($"Placed {etype}");
                }
                SaveLevel();
                SpawnEnemiesFromLevel();
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
            IsMouseVisible = false;
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

        // Grid visibility toggle (G key); snap is always on
        if (kb.IsKeyDown(Keys.G) && _prevKb.IsKeyUp(Keys.G))
            _editorShowGrid = !_editorShowGrid;

        // Camera follows cursor
        _camera.SnapTo(_editorCursor, 0, 0, unclamped: true);

        // Convert mouse to world coordinates
        var worldMouse = Vector2.Transform(
            new Vector2(mouse.X, mouse.Y),
            Matrix.Invert(_camera.TransformMatrix));

        int _snapOx = _level.TileGridInstance?.OriginX ?? 0;
        int _snapOy = _level.TileGridInstance?.OriginY ?? 0;
        var snapped = _editorGridSnap
            ? new Vector2(
                _snapOx + MathF.Floor((worldMouse.X - _snapOx) / _editorGridSize) * _editorGridSize,
                _snapOy + MathF.Floor((worldMouse.Y - _snapOy) / _editorGridSize) * _editorGridSize)
            : worldMouse;

        // Initialize tile grid if needed when entering tile paint mode
        if ((_editorTool == EditorTool.TilePaint || kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)) && _level.TileGridInstance == null)
        {
            int gridW = (_level.Bounds.Right - _level.Bounds.Left) / 32 + 2;
            int gridH = (_level.Bounds.Bottom - _level.Bounds.Top) / 32 + 2;
            _level.TileGridInstance = new TileGrid(gridW, gridH, 32, _level.Bounds.Left, _level.Bounds.Top);
        }

        // Tile paint mode — continuous paint/erase with mouse
        // Works in TilePaint mode directly, or in any mode with Shift held
        bool tilePaintActive = _editorTool == EditorTool.TilePaint;
        bool shiftTilePaint = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        if ((tilePaintActive || shiftTilePaint) && _level.TileGridInstance != null)
        {
            var tg = _level.TileGridInstance;
            // Snap to 32x32 tile grid
            int ox = _level.TileGridInstance?.OriginX ?? 0;
            int oy = _level.TileGridInstance?.OriginY ?? 0;
            int tileSnappedX = ox + (int)MathF.Floor((worldMouse.X - ox) / 32f) * 32;
            int tileSnappedY = oy + (int)MathF.Floor((worldMouse.Y - oy) / 32f) * 32;

            // Click on tile palette to select (palette drawn at top-right)
            bool paletteClicked = false;
            {
                int colsPerRow = 16, tileW = 20, tileH = 20;
                int startX = ViewW - colsPerRow * tileW - 10;
                int startY = 50;
                int paletteRows = (TileProperties.PaletteTiles.Length + colsPerRow - 1) / colsPerRow;
                int endY = startY + paletteRows * tileH;
                var ms = Mouse.GetState();
                if (ms.X >= startX && ms.Y >= startY && ms.Y < endY)
                {
                    paletteClicked = true;
                    if (ms.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                    {
                        int mx = ms.X, my = ms.Y;
                        int col = (mx - startX) / tileW;
                        int row = (my - startY) / tileH;
                        int idx = row * colsPerRow + col;
                        if (idx >= 0 && idx < TileProperties.PaletteTiles.Length && col < colsPerRow)
                        {
                            _tilePaletteCursor = idx;
                            _selectedTileType = TileProperties.PaletteTiles[idx];
                            SetEditorStatus($"Tile: {_selectedTileType}");
                        }
                    }
                }
            }

            // Left click = paint single OR start drag-fill (Ctrl+click = drag-fill rectangle)
            bool ctrlHeld = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            if (mouse.LeftButton == ButtonState.Pressed && !kb.IsKeyDown(Keys.T) && !paletteClicked)
            {
                var (tx, ty) = tg.WorldToTile(tileSnappedX, tileSnappedY);
                if (tx >= 0 && ty >= 0)
                {
                    if (ctrlHeld)
                    {
                        // Ctrl+drag: rectangle fill preview
                        if (!_dragFilling)
                        {
                            _dragFilling = true;
                            _dragFillStartCol = tx;
                            _dragFillStartRow = ty;
                        }
                        _dragFillEndCol = tx;
                        _dragFillEndRow = ty;
                    }
                    else
                    {
                        // Normal paint: single tile with undo
                        if (_prevMouse.LeftButton == ButtonState.Released) BeginUndoBatch();
                        EditorSetTile(tg, tx, ty, _selectedTileType);
                    }
                }
            }
            // Release after Ctrl drag = commit rectangle fill
            if (_dragFilling && (mouse.LeftButton == ButtonState.Released || !ctrlHeld))
            {
                BeginUndoBatch();
                int minC = Math.Min(_dragFillStartCol, _dragFillEndCol);
                int maxC = Math.Max(_dragFillStartCol, _dragFillEndCol);
                int minR = Math.Min(_dragFillStartRow, _dragFillEndRow);
                int maxR = Math.Max(_dragFillStartRow, _dragFillEndRow);
                for (int r = minR; r <= maxR; r++)
                    for (int c = minC; c <= maxC; c++)
                        EditorSetTile(tg, c, r, _selectedTileType);
                CommitUndoBatch();
                int count = (maxC - minC + 1) * (maxR - minR + 1);
                SetEditorStatus($"Filled {count} tiles");
                _dragFilling = false;
            }
            // Commit single paint undo on release
            if (!ctrlHeld && mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed && !_dragFilling)
                CommitUndoBatch();

            // Right click/hold = erase with undo (Ctrl+right-drag = rectangle erase)
            if (mouse.RightButton == ButtonState.Pressed)
            {
                var (tx, ty) = tg.WorldToTile(tileSnappedX, tileSnappedY);
                if (ctrlHeld)
                {
                    if (!_dragErasing)
                    {
                        _dragErasing = true;
                        _dragEraseStartCol = tx;
                        _dragEraseStartRow = ty;
                    }
                    _dragEraseEndCol = tx;
                    _dragEraseEndRow = ty;
                }
                else
                {
                    if (_prevMouse.RightButton == ButtonState.Released) BeginUndoBatch();
                    if (tx >= 0 && ty >= 0)
                        EditorSetTile(tg, tx, ty, TileType.Empty);
                }
            }
            // Release after Ctrl+right drag = commit rectangle erase
            if (_dragErasing && (mouse.RightButton == ButtonState.Released || !ctrlHeld))
            {
                BeginUndoBatch();
                int minC = Math.Min(_dragEraseStartCol, _dragEraseEndCol);
                int maxC = Math.Max(_dragEraseStartCol, _dragEraseEndCol);
                int minR = Math.Min(_dragEraseStartRow, _dragEraseEndRow);
                int maxR = Math.Max(_dragEraseStartRow, _dragEraseEndRow);
                for (int r = minR; r <= maxR; r++)
                    for (int c = minC; c <= maxC; c++)
                        EditorSetTile(tg, c, r, TileType.Empty);
                CommitUndoBatch();
                int count = (maxC - minC + 1) * (maxR - minR + 1);
                SetEditorStatus($"Erased {count} tiles");
                _dragErasing = false;
            }
            if (!ctrlHeld && mouse.RightButton == ButtonState.Released && _prevMouse.RightButton == ButtonState.Pressed && !_dragErasing)
                CommitUndoBatch();

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

        // Enemy/Item type cycling with [ and ]
        if (_editorTool == EditorTool.Enemy)
        {
            // [ and ] cycle through all variants across all categories
            if (kb.IsKeyDown(Keys.OemOpenBrackets) && _prevKb.IsKeyUp(Keys.OemOpenBrackets))
            {
                _enemyVariantCursor--;
                if (_enemyVariantCursor < 0)
                {
                    _enemyCategoryCursor = (_enemyCategoryCursor - 1 + EnemyCategories.Length) % EnemyCategories.Length;
                    _enemyVariantCursor = EnemyCategories[_enemyCategoryCursor].variants.Length - 1;
                }
                SetEditorStatus($"Enemy: {SelectedEnemyType}");
            }
            if (kb.IsKeyDown(Keys.OemCloseBrackets) && _prevKb.IsKeyUp(Keys.OemCloseBrackets))
            {
                _enemyVariantCursor++;
                if (_enemyVariantCursor >= EnemyCategories[_enemyCategoryCursor].variants.Length)
                {
                    _enemyCategoryCursor = (_enemyCategoryCursor + 1) % EnemyCategories.Length;
                    _enemyVariantCursor = 0;
                }
                SetEditorStatus($"Enemy: {SelectedEnemyType}");
            }
        }
        if (_editorTool == EditorTool.Item)
        {
            if (kb.IsKeyDown(Keys.OemOpenBrackets) && _prevKb.IsKeyUp(Keys.OemOpenBrackets))
            { _editorItemCursor = (_editorItemCursor - 1 + ItemTypes.Length) % ItemTypes.Length; SetEditorStatus($"Item: {ItemTypes[_editorItemCursor]}"); }
            if (kb.IsKeyDown(Keys.OemCloseBrackets) && _prevKb.IsKeyUp(Keys.OemCloseBrackets))
            { _editorItemCursor = (_editorItemCursor + 1) % ItemTypes.Length; SetEditorStatus($"Item: {ItemTypes[_editorItemCursor]}"); }
        }

        // Continue to T-drag and other tools (no early return)

        // Right click, X+Left click, or Delete key — delete nearest object
        {
            bool deleteClick = (mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released) ||
                               (kb.IsKeyDown(Keys.X) && mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released) ||
                               (kb.IsKeyDown(Keys.Delete) && _prevKb.IsKeyUp(Keys.Delete));
            if (deleteClick)
            {
                var wp = new Point((int)worldMouse.X, (int)worldMouse.Y);
                if (TryDeleteAt(wp))
                {
                    SetEditorStatus("Deleted");
                    SaveLevel();
                    SpawnEnemiesFromLevel();
                    RespawnItemsFromLevel();
                }
                else
                    SetEditorStatus("Nothing to delete here");
            }
        }

        // Left click — place / start drag (not when G is held for grab, not when X is held for delete)
        if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released && !kb.IsKeyDown(Keys.T) && !kb.IsKeyDown(Keys.X))
        {
            if (_editorTool == EditorTool.Spawn)
            {
                _level.PlayerSpawn = new PointData { X = (int)snapped.X, Y = (int)snapped.Y };
                SetEditorStatus("Spawn point set");
            }
            else if (_editorTool == EditorTool.Enemy)
            {
                string etype = SelectedEnemyType;
                int nextId = _level.Enemies.Length;
                var eList = new List<EnemySpawnData>(_level.Enemies);
                eList.Add(new EnemySpawnData { Id = $"{etype}-{nextId}", Type = etype, X = snapped.X, Y = snapped.Y });
                _level.Enemies = eList.ToArray();
                SetEditorStatus($"Enemy: {etype} placed ([/] to cycle)");
                SaveLevel();
                SpawnEnemiesFromLevel();
            }
            else if (_editorTool == EditorTool.Item)
            {
                string itype = ItemTypes[_editorItemCursor];
                int nextId = _level.Items.Length;
                var iList = new List<ItemData>(_level.Items);
                iList.Add(new ItemData { Id = $"{itype}-{nextId}", Type = itype, X = snapped.X, Y = snapped.Y });
                _level.Items = iList.ToArray();
                SetEditorStatus($"Item: {itype} placed ([/] to cycle type)");
                SaveLevel();
                RespawnItemsFromLevel();
            }
            else
            {
                _editorDragging = true;
                // Exits snap to grid like everything else
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
            
            // Ignore micro-drags (accidental clicks) — require at least 16px in one dimension
            if (w < 16 && h < 16) { /* cancelled */ }
            else
            {
            if (w < 16) w = 32; // minimum sizes
            if (h < 12) h = 12;

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
                case EditorTool.EnvRegion:
                    var erList = new List<EnvironmentRegion>(_level.EnvRegions);
                    erList.Add(new EnvironmentRegion { X = x, Y = y, W = w, H = h, Type = "cold" });
                    _level.EnvRegions = erList.ToArray();
                    _level.Build();
                    SetEditorStatus("Env region added (cold) — [F] to cycle type");
                    break;
            }
            } // end else (non-micro-drag)
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
            // Check player spawn point
            if (_editorMovingEntity == null && _level.PlayerSpawn != null)
            {
                var sp = _level.PlayerSpawn;
                if (new Rectangle(sp.X - 16, sp.Y - 24, 32, 48).Contains(mp))
                { _editorMovingEntity = sp; _editorMoveOffset = new Vector2(worldMouse.X - sp.X, worldMouse.Y - sp.Y); SetEditorStatus("Grabbed spawn point"); }
            }
            // Check shelters
            if (_editorMovingEntity == null && _level.Shelters != null)
                foreach (var sh in _level.Shelters)
                    if (new Rectangle((int)sh.X - 16, (int)sh.Y - 24, 32, 32).Contains(mp))
                    { _editorMovingEntity = sh; _editorMoveOffset = new Vector2(worldMouse.X - sh.X, worldMouse.Y - sh.Y); SetEditorStatus($"Grabbed shelter {sh.Name}"); break; }
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
            else if (_editorMovingEntity is PointData pd) { pd.X = (int)nx; pd.Y = (int)ny; }
            else if (_editorMovingEntity is ShelterData shd) { shd.X = nx; shd.Y = ny; }
        }
        // Release — drop entity and rebuild
        if ((mouse.LeftButton == ButtonState.Released || !kb.IsKeyDown(Keys.T)) && _editorMovingEntity != null)
        {
            // Snap shelter to ground on release
            if (_editorMovingEntity is ShelterData shDrop)
                shDrop.Y = SnapToSurface(shDrop.X, shDrop.Y - 32, 4, 0);
            _level.Build();
            SaveLevel();
            SpawnEnemiesFromLevel();
            RespawnItemsFromLevel();
            SetEditorStatus("Placed");
            _editorMovingEntity = null;
        }

        // F — cycle last wall's climb side: 0 (both) → 1 (right) → -1 (left)
        if (kb.IsKeyDown(Keys.F) && _prevKb.IsKeyUp(Keys.F) && _editorTool == EditorTool.Wall && _level.Walls.Length > 0)
        {
            var last = _level.Walls[_level.Walls.Length - 1];
            last.ClimbSide = last.ClimbSide switch { 0 => 1, 1 => -1, -1 => 99, _ => 0 };
            _level.Walls[_level.Walls.Length - 1] = last;
            _level.Build();
            string sideStr = last.ClimbSide switch { 0 => "both", 1 => "right", -1 => "left", _ => "none (solid)" };
            SetEditorStatus($"Wall climb side: {sideStr}");
        }

        // F — cycle last env region type
        if (kb.IsKeyDown(Keys.F) && _prevKb.IsKeyUp(Keys.F) && _editorTool == EditorTool.EnvRegion && _level.EnvRegions.Length > 0)
        {
            var last = _level.EnvRegions[_level.EnvRegions.Length - 1];
            last.Type = last.Type switch { "neutral" => "cold", "cold" => "hot", "hot" => "extreme_cold", "extreme_cold" => "extreme_hot", "extreme_hot" => "ionized", _ => "neutral" };
            _level.EnvRegions[_level.EnvRegions.Length - 1] = last;
            _level.Build();
            SetEditorStatus($"Env region type: {last.Type}");
        }

        // Tab — type-to-search exit target (hover mouse over an exit)
        // Shift+Tab — search targetExitId
        // Ctrl+Tab — search exit's own id
        if (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab) && !_exitSearchOpen)
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
                        OpenExitSearch(i, ExitSearchMode.ExitId);
                    else if (shift)
                        OpenExitSearch(i, ExitSearchMode.TargetExitId);
                    else
                        OpenExitSearch(i, ExitSearchMode.TargetLevel);
                    break;
                }
            }
        }

        // Exit search overlay input (arrow keys, enter, escape handled by TextInput; arrows here)
        if (_exitSearchOpen)
        {
            if (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up))
                _exitSearchCursor = Math.Max(0, _exitSearchCursor - 1);
            if (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down))
                _exitSearchCursor = Math.Min(_exitSearchResults.Length - 1, _exitSearchCursor + 1);
            if (kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape))
                _exitSearchOpen = false;
            return; // block all other editor input while search is open
        }
    }

    private bool TryDeleteAt(Point p)
    {
        // Expand hit area for small objects
        int tolerance = 8;
        
        // Check platforms
        for (int i = _level.Platforms.Length - 1; i >= 0; i--)
        {
            var r = _level.Platforms[i];
            var expanded = new Rectangle(r.X - tolerance, r.Y - tolerance, r.W + tolerance * 2, r.H + tolerance * 2);
            if (expanded.Contains(p))
            {
                _entityUndoStack.Add(("platform", i, _level.Platforms[i]));
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
                _entityUndoStack.Add(("spike", i, _level.Spikes[i]));
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
                _entityUndoStack.Add(("wall", i, _level.Walls[i]));
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
                _entityUndoStack.Add(("exit", i, _level.Exits[i]));
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
            var enemyRect = new Rectangle((int)e.X - tolerance, (int)e.Y - tolerance, size + tolerance * 2, h + tolerance * 2);
            if (enemyRect.Contains(p))
            {
                _entityUndoStack.Add(("enemy", i, _level.Enemies[i]));
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
        // Check shelters
        if (_level.Shelters != null)
        {
            for (int i = _level.Shelters.Length - 1; i >= 0; i--)
            {
                var sh = _level.Shelters[i];
                if (new Rectangle((int)sh.X - 16, (int)sh.Y - 24, 32, 32).Contains(p))
                {
                    var list = new List<ShelterData>(_level.Shelters);
                    list.RemoveAt(i);
                    _level.Shelters = list.ToArray();
                    return true;
                }
            }
        }
        // Check items (knives, grapples, etc.)
        for (int i = _level.Items.Length - 1; i >= 0; i--)
        {
            var item = _level.Items[i];
            if (new Rectangle((int)item.X - tolerance, (int)item.Y - tolerance, item.W + tolerance * 2, item.H + tolerance * 2).Contains(p))
            {
                var list = new List<ItemData>(_level.Items);
                list.RemoveAt(i);
                _level.Items = list.ToArray();
                // Also remove from runtime pickups
                _itemPickups.RemoveAll(ip => ip.Id == item.Id);
                return true;
            }
        }
        // Check env regions
        for (int i = _level.EnvRegions.Length - 1; i >= 0; i--)
        {
            var er = _level.EnvRegions[i];
            if (new Rectangle(er.X, er.Y, er.W, er.H).Contains(p))
            {
                var list = new List<EnvironmentRegion>(_level.EnvRegions);
                list.RemoveAt(i);
                _level.EnvRegions = list.ToArray();
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
        
        // Check tile grid surfaces
        if (_level.TileGridInstance != null)
        {
            int ts = _level.TileGrid.TileSize;
            int startCol = (int)(x / ts);
            int endCol = (int)((x + entityW - 1) / ts);
            int startRow = (int)(y / ts);
            int endRow = (int)(bestY / ts) + 1;
            for (int ty = startRow; ty <= endRow && ty < _level.TileGrid.Height; ty++)
            {
                for (int tx = startCol; tx <= endCol; tx++)
                {
                    if (tx < 0 || tx >= _level.TileGrid.Width) continue;
                    var tile = _level.TileGridInstance.GetTileAt(tx, ty);
                    if (TileProperties.IsSolid(tile))
                    {
                        float surfaceY = ty * ts - entityH;
                        if (surfaceY >= y - 20 && surfaceY < bestY)
                            bestY = surfaceY;
                    }
                }
            }
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
        _saveData.WeaponInventory = _weaponInventory.ConvertAll(w => w.ToString());
        _saveData.LeftHand = ActiveLeft.ToString();
        _saveData.RightHand = ActiveRight.ToString();
        _saveData.RightHand1 = _rightHand1.ToString();
        _saveData.RightHand2 = _rightHand2.ToString();
        _saveData.LeftHand1 = _leftHand1.ToString();
        _saveData.LeftHand2 = _leftHand2.ToString();
        _saveData.RightActiveSlot1 = _rightActiveSlot1;
        _saveData.LeftActiveSlot1 = _leftActiveSlot1;
        // Keep legacy fields in sync for backward compat
        _saveData.MeleeInventory = _weaponInventory.Where(w => !WeaponStats.Get(w).IsRanged).Select(w => w.ToString()).ToList();
        _saveData.RangedInventory = _weaponInventory.Where(w => WeaponStats.Get(w).IsRanged).Select(w => w.ToString()).ToList();
        _saveData.MeleeIndex = 0;
        _saveData.RangedIndex = 0;
        _saveData.MoveTier = (int)_player.CurrentTier;
        _saveData.Hp = _player.Hp;
        _saveData.SuitIntegrity = _player.SuitIntegrity;
        _saveData.Battery = _player.Battery;
        _saveData.Bestiary = _bestiary;
        _saveData.EvolutionFlags = _evolutionFlags;
        _saveData.CipherScanUnlocked = _cipherScanUnlocked;
        _saveData.WorldTime = _worldTime;
    }

    // --- Day-Night Cycle Helpers ---
    private string GetTimeOfDay()
    {
        if (_worldTime >= 5f && _worldTime < 7f) return "dawn";
        if (_worldTime >= 7f && _worldTime < 17f) return "day";
        if (_worldTime >= 17f && _worldTime < 19f) return "dusk";
        return "night";
    }

    private Color GetAmbientOverlay()
    {
        if (_levelIsUnderground)
            return new Color(5, 5, 20) * 0.85f; // always dark underground
        
        if (_worldTime >= 5f && _worldTime < 6f)
        {
            float t = (_worldTime - 5f);
            return Color.Lerp(new Color(5, 5, 20) * 0.85f, new Color(180, 100, 40) * 0.25f, t);
        }
        else if (_worldTime >= 6f && _worldTime < 7.5f)
        {
            float t = (_worldTime - 6f) / 1.5f;
            return Color.Lerp(new Color(180, 100, 40) * 0.25f, Color.Transparent, t);
        }
        else if (_worldTime >= 7.5f && _worldTime < 11f)
        {
            return Color.Transparent;
        }
        else if (_worldTime >= 11f && _worldTime < 14f)
        {
            float t = 1f - MathF.Abs(_worldTime - 12.5f) / 1.5f;
            return new Color(200, 160, 60) * (0.08f * t);
        }
        else if (_worldTime >= 14f && _worldTime < 17f)
        {
            float t = (_worldTime - 14f) / 3f;
            return new Color(160, 80, 40) * (0.06f * t);
        }
        else if (_worldTime >= 17f && _worldTime < 18.5f)
        {
            float t = (_worldTime - 17f) / 1.5f;
            return Color.Lerp(new Color(160, 80, 40) * 0.1f, new Color(80, 40, 120) * 0.35f, t);
        }
        else if (_worldTime >= 18.5f && _worldTime < 20f)
        {
            float t = (_worldTime - 18.5f) / 1.5f;
            return Color.Lerp(new Color(80, 40, 120) * 0.35f, new Color(5, 5, 20) * 0.75f, t);
        }
        else if (_worldTime >= 20f && _worldTime < 22f)
        {
            float t = (_worldTime - 20f) / 2f;
            return Color.Lerp(new Color(5, 5, 20) * 0.75f, new Color(5, 5, 20) * 0.88f, t);
        }
        else
        {
            return new Color(5, 5, 20) * 0.88f;
        }
    }

    private bool _editorTimeBtnClicked;
    private float _editorTimeBtnTarget;

    private void UpdateEditorTimeButtons()
    {
        var ms = Mouse.GetState();
        int btnSize = 24;
        int btnX = ViewW - btnSize - 8;
        int btnY = 60;
        float[] times = { 6f, 12f, 18f, 0f };
        for (int i = 0; i < times.Length; i++)
        {
            var r = new Rectangle(btnX, btnY + i * (btnSize + 4), btnSize, btnSize);
            if (r.Contains(ms.X, ms.Y) && ms.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
            {
                _worldTime = times[i];
                _editorTimeBtnClicked = true;
                SetEditorStatus($"Time → {(int)times[i]:00}:00");
            }
        }
        // Underground toggle
        int ugY = btnY + 4 * (btnSize + 4) + 4;
        var ugRect = new Rectangle(btnX - 8, ugY, btnSize + 16, btnSize);
        if (ugRect.Contains(ms.X, ms.Y) && ms.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
        {
            _level.IsUnderground = !_level.IsUnderground;
            _levelIsUnderground = _level.IsUnderground;
            _editorTimeBtnClicked = true;
        }
        // Evolution wave toggle buttons
        int evoY = ugY + btnSize + 6;
        for (int w = 1; w <= 2; w++)
        {
            var evoRect = new Rectangle(btnX - 8, evoY, btnSize + 16, btnSize);
            if (evoRect.Contains(ms.X, ms.Y) && ms.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
            {
                string flag = $"evolution_wave_{w}";
                if (_evolutionFlags.Contains(flag))
                    _evolutionFlags.Remove(flag);
                else
                    TriggerEvolutionWave(flag);
                _editorTimeBtnClicked = true;
                SetEditorStatus(_evolutionFlags.Contains(flag) ? $"Evolution Wave {w} ON" : $"Evolution Wave {w} OFF");
            }
            evoY += btnSize + 4;
        }
        // Cipher scan toggle button
        {
            var cipherRect = new Rectangle(btnX - 8, evoY, btnSize + 16, btnSize);
            if (cipherRect.Contains(ms.X, ms.Y) && ms.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
            {
                _cipherScanUnlocked = !_cipherScanUnlocked;
                _scanL2Available = true; // also enable L2 for testing
                _editorTimeBtnClicked = true;
                SetEditorStatus(_cipherScanUnlocked ? "Cipher Scan ON" : "Cipher Scan OFF");
            }
        }
        // Keyboard shortcuts: Ctrl+1-4 for time
        var kb = Keyboard.GetState();
        bool ctrlHeld = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        if (ctrlHeld && kb.IsKeyDown(Keys.D1) && _prevKb.IsKeyUp(Keys.D1)) { _worldTime = 6f; SetEditorStatus("Time -> 06:00 dawn"); }
        if (ctrlHeld && kb.IsKeyDown(Keys.D2) && _prevKb.IsKeyUp(Keys.D2)) { _worldTime = 12f; SetEditorStatus("Time -> 12:00 noon"); }
        if (ctrlHeld && kb.IsKeyDown(Keys.D3) && _prevKb.IsKeyUp(Keys.D3)) { _worldTime = 18f; SetEditorStatus("Time -> 18:00 dusk"); }
        if (ctrlHeld && kb.IsKeyDown(Keys.D4) && _prevKb.IsKeyUp(Keys.D4)) { _worldTime = 0f; SetEditorStatus("Time -> 00:00 midnight"); }
        // Ctrl+5/6 trigger evolution waves for testing
        if (ctrlHeld && kb.IsKeyDown(Keys.D5) && _prevKb.IsKeyUp(Keys.D5)) { TriggerEvolutionWave("evolution_wave_1"); SetEditorStatus("Evolution Wave 1 triggered"); }
        if (ctrlHeld && kb.IsKeyDown(Keys.D6) && _prevKb.IsKeyUp(Keys.D6)) { TriggerEvolutionWave("evolution_wave_2"); SetEditorStatus("Evolution Wave 2 triggered"); }
    }

    private void DrawEditorTimeButton(int x, int y, int size, string label, float targetTime, Color color)
    {
        var mouseState = Mouse.GetState();
        var btnRect = new Rectangle(x, y, size, size);
        bool hover = btnRect.Contains(mouseState.X, mouseState.Y);

        _spriteBatch.Draw(_pixel, btnRect, (hover ? Color.White * 0.2f : Color.Black * 0.5f));
        DrawHollowRect(x, y, size, size, hover ? color : color * 0.5f);
        var labelSize = _fontSmall.MeasureString(label);
        _spriteBatch.DrawString(_fontSmall, label, new Vector2(x + size / 2f - labelSize.X / 2f, y + size / 2f - labelSize.Y / 2f), hover ? color : color * 0.7f);
    }

    // --- Editor undo helpers ---
    private void BeginUndoBatch() { _currentUndoBatch = new(); }
    private void RecordTileChange(int col, int row, TileType oldTile, TileType newTile)
    {
        if (_currentUndoBatch != null && oldTile != newTile)
            _currentUndoBatch.Add((col, row, oldTile, newTile));
    }
    private void CommitUndoBatch()
    {
        if (_currentUndoBatch != null && _currentUndoBatch.Count > 0)
        {
            _undoStack.Add(_currentUndoBatch);
            if (_undoStack.Count > MaxUndoSteps) _undoStack.RemoveAt(0);
        }
        _currentUndoBatch = null;
    }
    private void EditorUndo()
    {
        // Try entity undo first (most recent action)
        if (_entityUndoStack.Count > 0)
        {
            var (type, index, data) = _entityUndoStack[^1];
            _entityUndoStack.RemoveAt(_entityUndoStack.Count - 1);
            switch (type)
            {
                case "platform":
                    var pList = new List<RectData>(_level.Platforms);
                    pList.Insert(Math.Min(index, pList.Count), (RectData)data);
                    _level.Platforms = pList.ToArray();
                    break;
                case "spike":
                    var sList = new List<RectData>(_level.Spikes);
                    sList.Insert(Math.Min(index, sList.Count), (RectData)data);
                    _level.Spikes = sList.ToArray();
                    break;
                case "wall":
                    var wList = new List<WallData>(_level.Walls);
                    wList.Insert(Math.Min(index, wList.Count), (WallData)data);
                    _level.Walls = wList.ToArray();
                    break;
                case "enemy":
                    var eList = new List<EnemySpawnData>(_level.Enemies);
                    eList.Insert(Math.Min(index, eList.Count), (EnemySpawnData)data);
                    _level.Enemies = eList.ToArray();
                    break;
                case "item":
                    var iList = new List<ItemData>(_level.Items);
                    iList.Insert(Math.Min(index, iList.Count), (ItemData)data);
                    _level.Items = iList.ToArray();
                    break;
                case "exit":
                    var exList = new List<ExitData>(_level.Exits);
                    exList.Insert(Math.Min(index, exList.Count), (ExitData)data);
                    _level.Exits = exList.ToArray();
                    break;
            }
            _level.Build();
            SaveLevel();
            SetEditorStatus($"Undo: restored {type}");
            return;
        }
        
        if (_undoStack.Count == 0 || _level.TileGridInstance == null) return;
        var batch = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        var tg = _level.TileGridInstance;
        foreach (var (col, row, oldTile, _) in batch)
            tg.SetTileAt(col, row, oldTile);
        SetEditorStatus($"Undo ({batch.Count} tiles)");
    }

    // --- Exit target type-to-search ---
    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (!_exitSearchOpen) return;
        char c = e.Character;
        if (c == '\b') // backspace
        {
            if (_exitSearchQuery.Length > 0)
                _exitSearchQuery = _exitSearchQuery[..^1];
        }
        else if (c == '\r' || c == '\n') // enter — confirm selection
        {
            ExitSearchConfirm();
            return;
        }
        else if (c == '\x1b') // escape — cancel
        {
            _exitSearchOpen = false;
            return;
        }
        else if (c >= ' ' && c <= '~') // printable
        {
            _exitSearchQuery += c;
        }
        ExitSearchUpdateResults();
    }

    private void OpenExitSearch(int exitIndex, ExitSearchMode mode)
    {
        _exitSearchOpen = true;
        _exitSearchExitIndex = exitIndex;
        _exitSearchMode = mode;
        _exitSearchQuery = "";
        _exitSearchCursor = 0;
        ExitSearchUpdateResults();
    }

    private void ExitSearchUpdateResults()
    {
        string[] candidates;
        if (_exitSearchMode == ExitSearchMode.TargetLevel)
        {
            var levelsDir = "Content/levels";
            var files = System.IO.Directory.Exists(levelsDir)
                ? System.IO.Directory.GetFiles(levelsDir, "*.json")
                : Array.Empty<string>();
            var names = new List<string> { "(none)" };
            foreach (var f in files)
                names.Add(System.IO.Path.GetFileNameWithoutExtension(f));
            candidates = names.ToArray();
        }
        else if (_exitSearchMode == ExitSearchMode.TargetExitId)
        {
            var exit = _level.Exits[_exitSearchExitIndex];
            var ids = new List<string> { "(none)" };
            if (!string.IsNullOrEmpty(exit.TargetLevel) && exit.TargetLevel != "__overworld__")
            {
                string targetPath = $"Content/levels/{exit.TargetLevel}.json";
                if (System.IO.File.Exists(targetPath))
                {
                    try
                    {
                        var targetLevel = LevelData.Load(targetPath);
                        foreach (var te in targetLevel.Exits)
                            if (!string.IsNullOrEmpty(te.Id)) ids.Add(te.Id);
                    }
                    catch { }
                }
            }
            candidates = ids.ToArray();
        }
        else // ExitId
        {
            candidates = new[] { "exit-left", "exit-right", "exit-top", "exit-bottom",
                "exit-overworld", "exit-door-1", "exit-door-2", "exit-to-crashsite",
                "exit-to-ship", "exit-to-hub" };
            // Include current id if not already listed
            var exit = _level.Exits[_exitSearchExitIndex];
            if (!string.IsNullOrEmpty(exit.Id) && !candidates.Contains(exit.Id))
                candidates = candidates.Prepend(exit.Id).ToArray();
        }

        if (string.IsNullOrEmpty(_exitSearchQuery))
            _exitSearchResults = candidates;
        else
            _exitSearchResults = candidates.Where(n =>
                n.Contains(_exitSearchQuery, StringComparison.OrdinalIgnoreCase)).ToArray();

        _exitSearchCursor = Math.Clamp(_exitSearchCursor, 0, Math.Max(0, _exitSearchResults.Length - 1));
    }

    private void ExitSearchConfirm()
    {
        if (_exitSearchResults.Length == 0 || _exitSearchExitIndex < 0 || _exitSearchExitIndex >= _level.Exits.Length)
        {
            _exitSearchOpen = false;
            return;
        }
        string selected = _exitSearchResults[_exitSearchCursor];
        if (selected == "(none)") selected = "";

        switch (_exitSearchMode)
        {
            case ExitSearchMode.TargetLevel:
                _level.Exits[_exitSearchExitIndex].TargetLevel = selected;
                SetEditorStatus($"Exit target: {(selected == "" ? "(none)" : selected)}");
                break;
            case ExitSearchMode.TargetExitId:
                _level.Exits[_exitSearchExitIndex].TargetExitId = selected;
                SetEditorStatus($"Target exit ID: {(selected == "" ? "(none)" : selected)}");
                break;
            case ExitSearchMode.ExitId:
                _level.Exits[_exitSearchExitIndex].Id = selected;
                SetEditorStatus($"Exit ID: {selected}");
                break;
        }
        _level.Build();
        _exitSearchOpen = false;
    }

    // --- Editor: set tile with undo tracking ---
    private void EditorSetTile(TileGrid tg, int col, int row, TileType tile)
    {
        var old = tg.GetTileAt(col, row);
        if (old != tile)
        {
            RecordTileChange(col, row, old, tile);
            tg.SetTileAt(col, row, tile);
        }
    }

    private void SaveLevel()
    {
        var dir = System.IO.Path.GetDirectoryName(_editorSaveFile);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        // Restore destroyed breakables before saving (runtime-only changes)
        if (_level.TileGridInstance != null)
        {
            foreach (var (col, row, origType) in _destroyedBreakables)
                _level.TileGridInstance.SetTileAt(col, row, origType);
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
            foreach (var (col, row, origType) in _destroyedBreakables)
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
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: _camera.TransformMatrix * shakeOffset);

        // Draw floor
        int floorY = _level.Floor.Y;
        int floorH = _level.Floor.Height;
        int bL = _level.Bounds.Left;
        int bR = _level.Bounds.Right;
        if (floorH > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, floorH), new Color(70, 50, 30));
            _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, 2), new Color(110, 80, 50));
        }

        // Draw grid
        if (_editorShowGrid)
        {
            var camInv = Matrix.Invert(_camera.TransformMatrix);
            var topLeft = Vector2.Transform(Vector2.Zero, camInv);
            var botRight = Vector2.Transform(new Vector2(ViewW, ViewH), camInv);
            int gs = _editorGridSize;
            int ox = _level.TileGridInstance?.OriginX ?? 0;
            int oy = _level.TileGridInstance?.OriginY ?? 0;
            int startX = ox + ((int)(topLeft.X - ox) / gs) * gs;
            int startY = oy + ((int)(topLeft.Y - oy) / gs) * gs;
            if (topLeft.X - ox < 0) startX -= gs;
            if (topLeft.Y - oy < 0) startY -= gs;
            for (int gx = startX; gx < (int)botRight.X; gx += gs)
                _spriteBatch.Draw(_pixel, new Rectangle(gx, (int)topLeft.Y, 1, (int)(botRight.Y - topLeft.Y)), Color.White * 0.30f);
            for (int gy = startY; gy < (int)botRight.Y; gy += gs)
                _spriteBatch.Draw(_pixel, new Rectangle((int)topLeft.X, gy, (int)(botRight.X - topLeft.X), 1), Color.White * 0.30f);
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
                    else if (TileProperties.IsLiquid(tile))
                    {
                        if (!(_useShaderWater && tile == TileType.Water))
                            DrawLiquidTile(wx, wy, tg.TileSize, tile, tg, tx, ty);
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
                        else if (tile == TileType.Fire)
                        {
                            // Animated fire — wider flame with flicker using game time
                            float t = _totalTime;
                            int flicker = (int)(MathF.Sin(t * 8f + cx * 0.1f) * 2);
                            int[] flameW = { 3, 5, 8, 10, 12, 12, 10, 8, 6, 4, 2 };
                            var inner = new Color(255, 200, 50);
                            for (int i = 0; i < flameW.Length; i++)
                            {
                                int fy = cy + 10 - i * 2 + (i > 5 ? flicker : 0);
                                int fw = flameW[i] + (i < 6 ? flicker : -flicker);
                                if (fw < 1) fw = 1;
                                var fc = i > 6 ? inner : bright;
                                _spriteBatch.Draw(_pixel, new Rectangle(cx - fw/2, fy, fw, 2), fc);
                            }
                        }
                        else if (tile == TileType.BrokenPipe)
                        {
                            // Pipe body: thick vertical bar
                            var pipeColor = new Color(100, 110, 120);
                            _spriteBatch.Draw(_pixel, new Rectangle(cx - 5, wy, 11, ts), pipeColor);
                            // Pipe highlight stripe
                            _spriteBatch.Draw(_pixel, new Rectangle(cx - 2, wy, 2, ts), new Color(140, 150, 160));
                            // Crack in the middle
                            _spriteBatch.Draw(_pixel, new Rectangle(cx + 3, cy - 4, 3, 8), new Color(40, 40, 40));
                            
                            // Animated leak particles spraying right from the crack
                            float t = _totalTime;
                            var leakBright = new Color(200, 220, 255);
                            for (int d = 0; d < 6; d++)
                            {
                                // Each particle loops across the right half of the tile
                                float phase = (t * 2.5f + d * 0.4f) % 1.5f;
                                if (phase > 1f) continue; // gap between bursts
                                int lx = cx + 6 + (int)(phase * (ts / 2));
                                int ly = cy - 3 + (int)(MathF.Sin(d * 1.7f + t * 5f) * 4f);
                                float alpha = 1f - phase; // fade out as they travel
                                int size = phase < 0.3f ? 3 : 2;
                                _spriteBatch.Draw(_pixel, new Rectangle(lx, ly, size, size), leakBright * alpha);
                            }
                        }
                        else if (tile == TileType.ElectricShock)
                        {
                            // Lightning bolt — only bright when retract-active
                            var eColor = (_level?.TileGridInstance?.RetractExtended ?? false) ? new Color(255, 255, 100) : bright * 0.3f;
                            // Zigzag bolt
                            int bx = cx - 2;
                            int by = wy + 4;
                            int[] zigX = { 0, 4, -2, 3, 0 };
                            for (int i = 0; i < zigX.Length - 1; i++)
                            {
                                int x1 = bx + zigX[i], x2 = bx + zigX[i + 1];
                                int y1 = by + i * 5, y2 = by + (i + 1) * 5;
                                int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
                                if (steps == 0) steps = 1;
                                for (int s = 0; s <= steps; s++)
                                {
                                    int px = x1 + (x2 - x1) * s / steps;
                                    int py = y1 + (y2 - y1) * s / steps;
                                    _spriteBatch.Draw(_pixel, new Rectangle(px, py, 3, 2), eColor);
                                }
                            }
                        }
                        else if (tile == TileType.Puddle)
                        {
                            // Flat puddle: shallow ellipse at bottom of tile
                            var pColor = new Color(60, 120, 180) * 0.6f;
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + 2, wy + ts - 6, ts - 4, 4), pColor);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + 5, wy + ts - 8, ts - 10, 2), pColor * 0.4f);
                            // Ripple
                            float t = _totalTime;
                            int rippleX = wx + 6 + ((int)(t * 10f + cx) % (ts - 12));
                            _spriteBatch.Draw(_pixel, new Rectangle(rippleX, wy + ts - 8, 4, 1), Color.White * 0.3f);
                        }
                        else if (tile == TileType.BreakableGlass)
                        {
                            // Translucent fill with grid lines
                            _spriteBatch.Draw(_pixel, rect, new Color(180, 220, 240) * 0.25f);
                            var glassLine = bright * 0.5f;
                            // Diagonal crack lines
                            for (int i = 0; i < ts; i += 8)
                            {
                                _spriteBatch.Draw(_pixel, new Rectangle(wx + i, wy, 1, ts), glassLine);
                            }
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts / 2, ts, 1), glassLine);
                        }
                        else if (tile == TileType.BreakableBattery)
                        {
                            // Yellow-tinted crate with lightning bolt symbol
                            _spriteBatch.Draw(_pixel, rect, new Color(220, 200, 60) * 0.3f);
                            // Lightning bolt: zigzag down the center
                            var bolt = new Color(255, 230, 50);
                            int bx = cx - 2;
                            _spriteBatch.Draw(_pixel, new Rectangle(bx + 2, cy - 8, 4, 4), bolt);
                            _spriteBatch.Draw(_pixel, new Rectangle(bx, cy - 4, 4, 4), bolt);
                            _spriteBatch.Draw(_pixel, new Rectangle(bx + 2, cy, 4, 4), bolt);
                            _spriteBatch.Draw(_pixel, new Rectangle(bx, cy + 4, 4, 4), bolt);
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
                int gox = _level.TileGridInstance?.OriginX ?? 0;
                int goy = _level.TileGridInstance?.OriginY ?? 0;
                int gx = gox + (int)MathF.Floor((wm.X - gox) / 32f) * 32;
                int gy = goy + (int)MathF.Floor((wm.Y - goy) / 32f) * 32;
                _spriteBatch.Draw(_pixel, new Rectangle(gx, gy, 32, 32), TileProperties.GetColor(_selectedTileType) * 0.4f);
                DrawHollowRect(gx, gy, 32, 32, Color.White * 0.5f);

                // Drag-fill preview rectangle
                if (_dragFilling && _level.TileGridInstance != null)
                {
                    int ox = _level.TileGridInstance.OriginX;
                    int oy = _level.TileGridInstance.OriginY;
                    int minC = Math.Min(_dragFillStartCol, _dragFillEndCol);
                    int maxC = Math.Max(_dragFillStartCol, _dragFillEndCol);
                    int minR = Math.Min(_dragFillStartRow, _dragFillEndRow);
                    int maxR = Math.Max(_dragFillStartRow, _dragFillEndRow);
                    int rx = ox + minC * 32, ry = oy + minR * 32;
                    int rw = (maxC - minC + 1) * 32, rh = (maxR - minR + 1) * 32;
                    _spriteBatch.Draw(_pixel, new Rectangle(rx, ry, rw, rh), TileProperties.GetColor(_selectedTileType) * 0.25f);
                    DrawHollowRect(rx, ry, rw, rh, Color.Yellow * 0.7f);
                    int count = (maxC - minC + 1) * (maxR - minR + 1);
                    _spriteBatch.DrawString(_fontSmall, $"{count} tiles", new Vector2(rx, ry - 16), Color.Yellow * 0.8f);
                }
                // Drag-erase preview
                if (_dragErasing && _level.TileGridInstance != null)
                {
                    int ox = _level.TileGridInstance.OriginX;
                    int oy = _level.TileGridInstance.OriginY;
                    int minC = Math.Min(_dragEraseStartCol, _dragEraseEndCol);
                    int maxC = Math.Max(_dragEraseStartCol, _dragEraseEndCol);
                    int minR = Math.Min(_dragEraseStartRow, _dragEraseEndRow);
                    int maxR = Math.Max(_dragEraseStartRow, _dragEraseEndRow);
                    int rx = ox + minC * 32, ry = oy + minR * 32;
                    int rw = (maxC - minC + 1) * 32, rh = (maxR - minR + 1) * 32;
                    _spriteBatch.Draw(_pixel, new Rectangle(rx, ry, rw, rh), Color.Red * 0.15f);
                    DrawHollowRect(rx, ry, rw, rh, Color.Red * 0.7f);
                    int count = (maxC - minC + 1) * (maxR - minR + 1);
                    _spriteBatch.DrawString(_fontSmall, $"{count} erase", new Vector2(rx, ry - 16), Color.Red * 0.8f);
                }
            }
        }

        // Re-draw ceilings over background tiles so they stay visible in editor
        foreach (var c in _level.Ceilings)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y, c.W, c.H), new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(c.X, c.Y + c.H - 2, c.W, 2), new Color(90, 90, 90));
        }

        // Noise-based water shader pass (editor)
        if (_useShaderWater && _level.TileGridInstance != null)
            DrawWaterShaderPass(_level.TileGridInstance, _camera.TransformMatrix);

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
                    "knife" => new Color(200, 200, 210),
                    "grapple" => new Color(70, 90, 110),
                    "stick" => new Color(139, 90, 43),
                    "whip" => new Color(100, 60, 30),
                    "dagger" => new Color(180, 180, 200),
                    "sling" => Color.DarkKhaki,
                    "sword" => Color.Silver,
                    "greatsword" => new Color(200, 200, 220),
                    "axe" => Color.DarkGray,
                    "club" => new Color(110, 70, 35),
                    "greatclub" => new Color(90, 55, 25),
                    "hammer" => new Color(160, 160, 170),
                    "bow" => new Color(160, 120, 60),
                    "gun" => Color.SlateGray,
                    "map-module" => Color.Cyan,
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
                if (_eveOrbActive) continue; // already orbiting, don't draw NPC
                float cx = npc.X + npc.W / 2f;
                float cy = npc.Y + npc.H / 2f;
                // Dormant EVE — flickering, damaged look
                float flicker = 0.3f + 0.2f * MathF.Sin(_totalTime * 1.5f) + 0.1f * MathF.Sin(_totalTime * 7f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 8), (int)(cy - 8), 16, 16), Color.Cyan * (0.15f * flicker));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - 5), (int)(cy - 5), 10, 10), Color.Cyan * (0.4f * flicker));
                // Occasional spark
                if (MathF.Sin(_totalTime * 11f) > 0.9f)
                {
                    int sx = (int)(cx + MathF.Sin(_totalTime * 17f) * 6);
                    int sy = (int)(cy + MathF.Cos(_totalTime * 13f) * 6);
                    _spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 1, 1), Color.Yellow * 0.8f);
                }
                // Interact prompt when close
                float dist = Vector2.Distance(_player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f), new Vector2(cx, cy));
                if (dist < 48f)
                    DrawOutlinedString(_fontSmall, "[W]", new Vector2(cx - 8, cy - 24), Color.Cyan * (0.5f + 0.3f * MathF.Sin(_totalTime * 3f)));
            }
            else
            {
                Color npcColor = ParseNpcColor(npc.Color);
                _spriteBatch.Draw(_pixel, new Rectangle(npc.X, npc.Y, npc.W, npc.H), npcColor * 0.7f);
                _spriteBatch.DrawString(_font, SafeText(npc.Name), new Vector2(npc.X, npc.Y - 16), npcColor * 0.8f);
            }
        }

        // Draw shelters in editor
        if (_level.Shelters != null)
        {
            foreach (var sh in _level.Shelters)
            {
                int sx = (int)sh.X - 16, sy = (int)sh.Y - 30;
                _spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 32, 32), new Color(50, 90, 35) * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle(sx + 12, sy + 12, 8, 16), new Color(90, 60, 30) * 0.8f);
                DrawOutlinedString(_fontSmall, SafeText(sh.Name), new Vector2(sx - 4, sy - 14), Color.Green * 0.9f);
            }
        }

        // Draw enemy spawns in editor
        foreach (var e in _level.Enemies)
        {
            Color ec = e.Type switch
            {
                "swarm" => Color.OrangeRed,
                "forager" or "crawler" => new Color(80, 50, 20),
                "skitter" => new Color(60, 80, 50),
                "leaper" => new Color(140, 80, 20),
                "bombardier" => new Color(120, 60, 20),
                "hopper" => new Color(80, 140, 60),
                "thornback" => new Color(60, 100, 30),
                "dummy" => new Color(140, 100, 160),
                "bird" => new Color(100, 130, 170),
                "wingbeater" => new Color(160, 80, 80),
                _ => Color.White
            };
            int size = e.Type == "thornback" ? 32 : (e.Type == "hopper" ? 20 : (e.Type == "swarm" ? 20 : 16));
            _spriteBatch.Draw(_pixel, new Rectangle((int)e.X, (int)e.Y, size, size), ec * 0.6f);
            _spriteBatch.DrawString(_fontSmall, SafeText(e.Type), new Vector2(e.X, e.Y - 14), ec * 0.8f);
            // Off-screen indicator: draw arrow at edge of view
            var cam = _camera.TransformMatrix;
            var screenPos = Vector2.Transform(new Vector2(e.X, e.Y), cam);
            if (screenPos.Y > ViewH - 20)
            {
                var edgeWorld = Vector2.Transform(new Vector2(screenPos.X, ViewH - 24), Matrix.Invert(cam));
                _spriteBatch.Draw(_pixel, new Rectangle((int)edgeWorld.X - 6, (int)edgeWorld.Y, 12, 4), ec);
                _spriteBatch.DrawString(_fontSmall, SafeText($"▼ {e.Type}"), new Vector2(edgeWorld.X - 20, edgeWorld.Y - 14), ec);
            }
            else if (screenPos.Y < 20)
            {
                var edgeWorld = Vector2.Transform(new Vector2(screenPos.X, 24), Matrix.Invert(cam));
                _spriteBatch.Draw(_pixel, new Rectangle((int)edgeWorld.X - 6, (int)edgeWorld.Y, 12, 4), ec);
                _spriteBatch.DrawString(_fontSmall, SafeText($"▲ {e.Type}"), new Vector2(edgeWorld.X - 20, edgeWorld.Y + 6), ec);
            }
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

        // Draw spatial labels
        foreach (var lbl in _level.Labels)
        {
            var lblFont = lbl.Size switch { "large" => _fontLarge, "normal" => _font, _ => _fontSmall };
            var lblColor = lbl.Color switch
            {
                "Green" => Color.LimeGreen,
                "Yellow" => Color.Yellow,
                "Red" => Color.OrangeRed,
                "Cyan" => Color.Cyan,
                "Orange" => Color.Orange,
                "Gray" => Color.Gray,
                "Purple" => Color.MediumPurple,
                _ => Color.White
            };
            DrawOutlinedString(lblFont, lbl.Text, new Vector2(lbl.X, lbl.Y), lblColor * 0.9f);
        }

        // Draw drag preview
        if (_editorDragging)
        {
            var worldMouse = Vector2.Transform(new Vector2(mouse.X, mouse.Y), Matrix.Invert(_camera.TransformMatrix));
            int _dox = _level.TileGridInstance?.OriginX ?? 0;
            int _doy = _level.TileGridInstance?.OriginY ?? 0;
            var dragEnd = _editorGridSnap
                ? new Vector2(_dox + MathF.Floor((worldMouse.X - _dox) / _editorGridSize) * _editorGridSize, _doy + MathF.Floor((worldMouse.Y - _doy) / _editorGridSize) * _editorGridSize)
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
                EditorTool.EnvRegion => Color.CornflowerBlue * 0.2f,
                _ => Color.White * 0.2f
            };

            if (_editorTool == EditorTool.Rope)
                _spriteBatch.Draw(_pixel, new Rectangle((int)_editorDragStart.X - 1, py, 3, ph), previewColor);
            else if (_editorTool == EditorTool.Platform || _editorTool == EditorTool.Spike || _editorTool == EditorTool.Ceiling)
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, 12), previewColor);
            else
                _spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), previewColor);
        }

        // Draw env regions
        for (int i = 0; i < _level.EnvRegions.Length; i++)
        {
            var er = _level.EnvRegions[i];
            var rect = new Rectangle(er.X, er.Y, er.W, er.H);
            Color regionColor = er.Type switch
            {
                "cold" => Color.CornflowerBlue * 0.15f,
                "hot" => Color.OrangeRed * 0.15f,
                "extreme_cold" => Color.Blue * 0.2f,
                "extreme_hot" => Color.Red * 0.2f,
                "ionized" => new Color(100, 60, 180) * 0.15f,
                _ => Color.White * 0.05f
            };
            _spriteBatch.Draw(_pixel, rect, regionColor);
            // Border
            _spriteBatch.Draw(_pixel, new Rectangle(er.X, er.Y, er.W, 1), regionColor * 3f);
            _spriteBatch.Draw(_pixel, new Rectangle(er.X, er.Y + er.H, er.W, 1), regionColor * 3f);
            _spriteBatch.Draw(_pixel, new Rectangle(er.X, er.Y, 1, er.H), regionColor * 3f);
            _spriteBatch.Draw(_pixel, new Rectangle(er.X + er.W, er.Y, 1, er.H), regionColor * 3f);
            // Label
            string label = er.Type;
            var labelSize = _fontSmall.MeasureString(label);
            _spriteBatch.DrawString(_fontSmall, label, new Vector2(er.X + er.W / 2f - labelSize.X / 2f, er.Y + er.H / 2f - labelSize.Y / 2f), Color.White * 0.5f);
        }

        // Draw world bounds outline
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Top, bR - bL, 1), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Bottom, bR - bL, 1), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bL, _level.Bounds.Top, 1, _level.Bounds.Bottom - _level.Bounds.Top), Color.Yellow * 0.2f);
        _spriteBatch.Draw(_pixel, new Rectangle(bR, _level.Bounds.Top, 1, _level.Bounds.Bottom - _level.Bounds.Top), Color.Yellow * 0.2f);

        _spriteBatch.End();

        // Screen-space UI
        _spriteBatch.Begin();

        // Day-night ambient overlay (over world, under editor UI)
        {
            var editorAmbient = GetAmbientOverlay();
            if (editorAmbient.A > 0)
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), editorAmbient);
        }

        // Toolbar
        string[] toolNames = { "0:Flr", "1:Plt", "2:Rop", "3:Wal", "4:Spk", "5:Ext", "6:Spn", "7:WS", "8:OW", "9:Ceil", "Q:Tile", "", "", "Env" };
        float toolX = 10;
        for (int i = 0; i < toolNames.Length; i++)
        {
            bool active = (int)_editorTool == i;
            _spriteBatch.DrawString(_fontSmall, toolNames[i], new Vector2(toolX, 10), active ? Color.Yellow : Color.Gray * 0.6f);
            toolX += _fontSmall.MeasureString(toolNames[i]).X + 8;
        }
        // E/P palette indicators
        _spriteBatch.DrawString(_fontSmall, "E:Enemy", new Vector2(toolX, 10), _entityPaletteOpen ? Color.Yellow : Color.Gray * 0.6f);
        toolX += _fontSmall.MeasureString("E:Enemy").X + 8;
        _spriteBatch.DrawString(_fontSmall, "P:Item", new Vector2(toolX, 10), _spawnMenuOpen ? Color.Yellow : Color.Gray * 0.6f);

        // Grid snap indicator
        _spriteBatch.DrawString(_font, $"Grid: {(_editorShowGrid ? "ON" : "OFF")} [G]", new Vector2(10, 30), _editorShowGrid ? Color.LightGreen : Color.Gray * 0.5f);

        // Level name
        _spriteBatch.DrawString(_font, SafeText($"Level: {_level.Name}"), new Vector2(10, 50), Color.White * 0.6f);

        // Cursor world position
        _spriteBatch.DrawString(_font, $"Pos: {(int)_editorCursor.X}, {(int)_editorCursor.Y}", new Vector2(10, 70), Color.White * 0.4f);

        // Status message
        if (_editorStatusTimer > 0)
            _spriteBatch.DrawString(_fontSmall, SafeText(_editorStatusMsg), new Vector2(10, ViewH - 30), Color.Yellow);

        // --- Editor time-of-day controls ---
        {
            int btnSize = 24;
            int btnX = ViewW - btnSize - 8;
            int btnY = 60;
            string timeStr = $"{(int)_worldTime:00}:{(int)((_worldTime % 1) * 60):00}";
            var timeSize = _fontSmall.MeasureString(timeStr);
            _spriteBatch.DrawString(_fontSmall, timeStr, new Vector2(btnX + btnSize / 2f - timeSize.X / 2f, btnY - 16), Color.White * 0.7f);

            DrawEditorTimeButton(btnX, btnY, btnSize, "6", 6f, new Color(255, 160, 60));
            btnY += btnSize + 4;
            DrawEditorTimeButton(btnX, btnY, btnSize, "12", 12f, new Color(255, 220, 100));
            btnY += btnSize + 4;
            DrawEditorTimeButton(btnX, btnY, btnSize, "18", 18f, new Color(160, 100, 200));
            btnY += btnSize + 4;
            DrawEditorTimeButton(btnX, btnY, btnSize, "0", 0f, new Color(140, 140, 200));
            btnY += btnSize + 8;

            // Underground toggle (click handled in UpdateEditorTimeButtons)
            {
                var mouseState = Mouse.GetState();
                var ugRect = new Rectangle(btnX - 2, btnY, btnSize + 4, btnSize);
                bool ugHover = ugRect.Contains(mouseState.X, mouseState.Y);
                var ugColor = _levelIsUnderground ? Color.Cyan : Color.Gray * 0.5f;
                _spriteBatch.Draw(_pixel, ugRect, (ugHover ? Color.White * 0.2f : Color.Black * 0.5f));
                var ugText = "UG";
                var ugSize2 = _fontSmall.MeasureString(ugText);
                _spriteBatch.DrawString(_fontSmall, ugText, new Vector2(btnX + btnSize / 2f - ugSize2.X / 2f, btnY + btnSize / 2f - ugSize2.Y / 2f), ugColor);
                if (_levelIsUnderground)
                {
                    var ugLabel = "UNDERGROUND";
                    var ugLabelSize = _fontSmall.MeasureString(ugLabel);
                    _spriteBatch.DrawString(_fontSmall, ugLabel, new Vector2(btnX - ugLabelSize.X - 8, btnY + btnSize / 2f - ugLabelSize.Y / 2f), Color.Cyan * 0.8f);
                }
            }
            // Evolution wave toggle buttons
            btnY += btnSize + 6;
            for (int w = 1; w <= 2; w++)
            {
                var mouseState2 = Mouse.GetState();
                var evoRect = new Rectangle(btnX - 2, btnY, btnSize + 4, btnSize);
                string flag = $"evolution_wave_{w}";
                bool evoActive = _evolutionFlags.Contains(flag);
                bool evoHover = evoRect.Contains(mouseState2.X, mouseState2.Y);
                var evoColor = evoActive ? new Color(200, 100, 255) : Color.Gray * 0.5f;
                _spriteBatch.Draw(_pixel, evoRect, (evoHover ? Color.White * 0.2f : Color.Black * 0.5f));
                var evoText = $"E{w}";
                var evoSize = _fontSmall.MeasureString(evoText);
                _spriteBatch.DrawString(_fontSmall, evoText, new Vector2(btnX + btnSize / 2f - evoSize.X / 2f, btnY + btnSize / 2f - evoSize.Y / 2f), evoColor);
                if (evoActive)
                {
                    var evoLabel = $"WAVE {w}";
                    var evoLabelSize = _fontSmall.MeasureString(evoLabel);
                    _spriteBatch.DrawString(_fontSmall, evoLabel, new Vector2(btnX - evoLabelSize.X - 8, btnY + btnSize / 2f - evoLabelSize.Y / 2f), new Color(200, 100, 255) * 0.8f);
                }
                btnY += btnSize + 4;
            }
            // Cipher scan toggle button
            {
                var mouseState3 = Mouse.GetState();
                var cRect = new Rectangle(btnX - 2, btnY, btnSize + 4, btnSize);
                bool cActive = _cipherScanUnlocked;
                bool cHover = cRect.Contains(mouseState3.X, mouseState3.Y);
                var cColor = cActive ? new Color(180, 100, 255) : Color.Gray * 0.5f;
                _spriteBatch.Draw(_pixel, cRect, (cHover ? Color.White * 0.2f : Color.Black * 0.5f));
                var cText = "C3";
                var cSize = _fontSmall.MeasureString(cText);
                _spriteBatch.DrawString(_fontSmall, cText, new Vector2(btnX + btnSize / 2f - cSize.X / 2f, btnY + btnSize / 2f - cSize.Y / 2f), cColor);
                if (cActive)
                {
                    var cLabel = "CIPHER";
                    var cLabelSize = _fontSmall.MeasureString(cLabel);
                    _spriteBatch.DrawString(_fontSmall, cLabel, new Vector2(btnX - cLabelSize.X - 8, btnY + btnSize / 2f - cLabelSize.Y / 2f), new Color(180, 100, 255) * 0.8f);
                }
            }
        }
        // Exit search overlay
        if (_exitSearchOpen)
        {
            int boxW = 320, boxH = Math.Min(300, 50 + _exitSearchResults.Length * 20);
            int boxX = (ViewW - boxW) / 2, boxY = (ViewH - boxH) / 2;
            // Background
            _spriteBatch.Draw(_pixel, new Rectangle(boxX - 2, boxY - 2, boxW + 4, boxH + 4), Color.White * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(boxX, boxY, boxW, boxH), new Color(20, 20, 40));
            // Title
            string modeLabel = _exitSearchMode switch
            {
                ExitSearchMode.TargetLevel => "Target Level",
                ExitSearchMode.TargetExitId => "Target Exit ID",
                ExitSearchMode.ExitId => "Exit ID",
                _ => "Search"
            };
            _spriteBatch.DrawString(_fontSmall, SafeText(modeLabel), new Vector2(boxX + 8, boxY + 4), Color.LimeGreen);
            // Search input
            string prompt = "> " + _exitSearchQuery + "_";
            _spriteBatch.DrawString(_fontSmall, SafeText(prompt), new Vector2(boxX + 8, boxY + 22), Color.White);
            // Results
            int maxVisible = Math.Min(_exitSearchResults.Length, 12);
            int scrollStart = Math.Max(0, _exitSearchCursor - 6);
            for (int i = 0; i < maxVisible && scrollStart + i < _exitSearchResults.Length; i++)
            {
                int ri = scrollStart + i;
                bool selected = ri == _exitSearchCursor;
                int ry = boxY + 42 + i * 20;
                if (selected)
                    _spriteBatch.Draw(_pixel, new Rectangle(boxX + 4, ry, boxW - 8, 18), Color.LimeGreen * 0.25f);
                var col = selected ? Color.White : Color.Gray;
                _spriteBatch.DrawString(_fontSmall, SafeText(_exitSearchResults[ri]), new Vector2(boxX + 12, ry + 1), col);
            }
        }

        // Controls hint
        string controlsHint = _editorTool switch
        {
            EditorTool.TilePaint => "[=]Play [Q]Tools [Click]Paint [Ctrl+Drag]Fill [RClick]Erase [[ ]]Tile [Ctrl+Z]Undo",
            EditorTool.Enemy => $"[=]Play [Q]Tools [Click]Place [[ ]]Type: {SelectedEnemyType}",
            EditorTool.Item => $"[=]Play [Q]Tools [Click]Place [[ ]]Type: {ItemTypes[_editorItemCursor]}",
            EditorTool.EnvRegion => "[=]Play [Q]Tools [Drag]Place [RClick]Del [F]Cycle Type",
            _ => "[=]Play [Esc]Menu [Q]Tile [E]Enemy [P]Item [Drag]Place [RClick]Del [Tab]Target [Ctrl+1-4]Time",
        };
        _spriteBatch.DrawString(_fontSmall, controlsHint, new Vector2(10, ViewH - 16), Color.Gray * 0.45f);

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

            string[] paletteNames = { "Solid Floor", "Platform", "Rope", "Wall", "Spike", "Exit", "Spawn", "Wall Spike", "Overworld Exit", "Ceiling", "Tile Paint", "Enemy (E)", "Item (P)", "Env Region" };
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
            // Count visible rows
            int rowCount = 0;
            for (int i = 0; i < EnemyCategories.Length; i++)
            {
                rowCount++; // category row
                if (_enemyVariantExpanded && i == _enemyCategoryCursor && EnemyCategories[i].variants.Length > 1)
                    rowCount += EnemyCategories[i].variants.Length;
            }

            float epalW = 220, lineH = 26;
            float epalH = rowCount * lineH + 30;
            float epalX = ViewW / 2f - epalW / 2f, epalY = ViewH / 2f - epalH / 2f;
            _spriteBatch.Draw(_pixel, new Rectangle((int)epalX, (int)epalY, (int)epalW, (int)epalH), Color.Black * 0.85f);
            _spriteBatch.DrawString(_fontSmall, SafeText("ENEMIES [E]  W/S=nav  Right=expand"), new Vector2(epalX + 8, epalY + 4), Color.White);

            float drawY = epalY + 24;
            for (int i = 0; i < EnemyCategories.Length; i++)
            {
                var (cat, variants) = EnemyCategories[i];
                bool isCatSelected = i == _enemyCategoryCursor && !_enemyVariantExpanded;
                bool isCatActive = i == _enemyCategoryCursor;
                string arrow = (variants.Length > 1) ? (isCatActive && _enemyVariantExpanded ? "▼ " : "▶ ") : "  ";
                string catLabel = $"{arrow}{cat}";

                if (isCatSelected)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)epalX + 4, (int)drawY, (int)epalW - 8, (int)lineH), Color.Yellow * 0.15f);

                Color catColor = isCatSelected ? Color.Yellow : isCatActive ? Color.White : Color.Gray;
                _spriteBatch.DrawString(_fontSmall, SafeText(catLabel), new Vector2(epalX + 10, drawY + 4), catColor);

                // Show default variant name on right if not expanded
                if (!(_enemyVariantExpanded && isCatActive) && variants.Length > 0)
                {
                    string defaultV = isCatActive ? variants[_enemyVariantCursor < variants.Length ? _enemyVariantCursor : 0] : variants[0];
                    _spriteBatch.DrawString(_fontSmall, SafeText(defaultV), new Vector2(epalX + epalW - 80, drawY + 4), Color.Gray * 0.5f);
                }

                drawY += lineH;

                // Expanded variants
                if (_enemyVariantExpanded && isCatActive && variants.Length > 1)
                {
                    for (int v = 0; v < variants.Length; v++)
                    {
                        bool vSelected = v == _enemyVariantCursor;
                        if (vSelected)
                            _spriteBatch.Draw(_pixel, new Rectangle((int)epalX + 20, (int)drawY, (int)epalW - 24, (int)lineH), Color.Cyan * 0.12f);

                        _spriteBatch.DrawString(_fontSmall, SafeText($"   {variants[v]}"),
                            new Vector2(epalX + 26, drawY + 4),
                            vSelected ? Color.Cyan : Color.Gray * 0.7f);
                        drawY += lineH;
                    }
                }
            }

            // Hint at bottom
            _spriteBatch.DrawString(_fontSmall, SafeText("Enter/Click=place  Esc=close  Left=collapse"),
                new Vector2(epalX + 8, drawY + 4), Color.Gray * 0.4f);
        }

        // Item placement palette (P key)
        if (_itemPaletteOpen)
        {
            float ipalW = 180, ipalH = ItemPaletteTypes.Length * 28 + 20;
            float ipalX = ViewW / 2f - ipalW / 2f, ipalY = ViewH / 2f - ipalH / 2f;
            _spriteBatch.Draw(_pixel, new Rectangle((int)ipalX, (int)ipalY, (int)ipalW, (int)ipalH), Color.Black * 0.85f);
            _spriteBatch.DrawString(_font, SafeText("ITEMS [P]"), new Vector2(ipalX + 40, ipalY + 4), Color.White);

            for (int i = 0; i < ItemPaletteTypes.Length; i++)
            {
                float itemY = ipalY + 24 + i * 28;
                bool selected = i == _itemPaletteCursor;
                if (selected)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)ipalX + 4, (int)itemY, (int)ipalW - 8, 26), Color.Cyan * 0.15f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)ipalX + 10, (int)itemY + 8, 10, 10), Color.Goldenrod);
                _spriteBatch.DrawString(_font, SafeText(ItemPaletteTypes[i]), new Vector2(ipalX + 26, itemY + 4), selected ? Color.Cyan : Color.Gray);
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
                // Show window size as first item, then remaining graphics settings
                int totalItems = 1 + _graphicsSettings.Length; // window size + CRT etc
                for (int i = 0; i < totalItems; i++)
                {
                    bool selected = i == _settingsItemCursor;
                    string prefix = selected ? "> " : "  ";
                    Color color;
                    string txt;
                    if (i == 0)
                    {
                        var (cw, ch, clabel) = WindowSizes[_windowSizeIndex];
                        txt = $"{prefix}Window Size: {clabel}";
                        color = selected ? Color.Yellow : Color.Gray;
                    }
                    else
                    {
                        var s = _graphicsSettings[i - 1];
                        string value = s.Get() ? "  ON" : "  OFF";
                        color = selected ? Color.Yellow : Color.Gray;
                        if (s.Get()) color = selected ? Color.Yellow : Color.LightGreen;
                        txt = $"{prefix}{s.Label}{value}";
                    }
                    var ts = _font.MeasureString(txt);
                    _spriteBatch.DrawString(_font, SafeText(txt), new Vector2(cx - ts.X / 2, startY + i * lineHeight), color);
                }
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
        bool left = (kb.IsKeyDown(Keys.A) && _prevKb.IsKeyUp(Keys.A)) || (kb.IsKeyDown(Keys.Left) && _prevKb.IsKeyUp(Keys.Left));
        bool right = (kb.IsKeyDown(Keys.D) && _prevKb.IsKeyUp(Keys.D)) || (kb.IsKeyDown(Keys.Right) && _prevKb.IsKeyUp(Keys.Right));
        bool up = (kb.IsKeyDown(Keys.W) && _prevKb.IsKeyUp(Keys.W)) || (kb.IsKeyDown(Keys.Up) && _prevKb.IsKeyUp(Keys.Up));
        bool down = (kb.IsKeyDown(Keys.S) && _prevKb.IsKeyUp(Keys.S)) || (kb.IsKeyDown(Keys.Down) && _prevKb.IsKeyUp(Keys.Down));
        bool confirm = (kb.IsKeyDown(Keys.Enter) && _prevKb.IsKeyUp(Keys.Enter)) || (kb.IsKeyDown(Keys.Space) && _prevKb.IsKeyUp(Keys.Space));
        bool esc = kb.IsKeyDown(Keys.Escape) && _prevKb.IsKeyUp(Keys.Escape);
        if (_equipPickerOpen)
        {
            // Weapon picker sub-list
            if (esc)
            {
                _equipPickerOpen = false;
                return;
            }
            int count = _weaponInventory.Count + 1; // +1 for "None" option
            if (up) _equipPickerIndex = (_equipPickerIndex - 1 + count) % count;
            if (down) _equipPickerIndex = (_equipPickerIndex + 1) % count;
            if (confirm)
            {
                WeaponType chosen = _equipPickerIndex == 0 ? WeaponType.None : _weaponInventory[_equipPickerIndex - 1];
                // Equip chosen weapon to current slot
                // If weapon is already in another slot, unequip from there
                if (chosen != WeaponType.None)
                {
                    if (_rightHand1 == chosen) _rightHand1 = WeaponType.None;
                    if (_rightHand2 == chosen) _rightHand2 = WeaponType.None;
                    if (_leftHand1 == chosen) _leftHand1 = WeaponType.None;
                    if (_leftHand2 == chosen) _leftHand2 = WeaponType.None;
                }
                // Assign to current slot
                if (_equipCursorX == 0 && _equipCursorY == 0) _leftHand1 = chosen;
                else if (_equipCursorX == 0 && _equipCursorY == 1) _leftHand2 = chosen;
                else if (_equipCursorX == 2 && _equipCursorY == 0) _rightHand1 = chosen;
                else if (_equipCursorX == 2 && _equipCursorY == 1) _rightHand2 = chosen;
                _equipPickerOpen = false;
            }
            return;
        }

        // Body view navigation
        if (esc)
        {
            _inventoryOpen = false;
            return;
        }
        // Tab also closes
        if (kb.IsKeyDown(Keys.Tab) && _prevKb.IsKeyUp(Keys.Tab))
        {
            _inventoryOpen = false;
            return;
        }

        if (_invFocusSidebar)
        {
            // Sidebar focused: W/S switch categories, D goes back to center
            if (up) { _invCategory = (_invCategory - 1 + 4) % 4; _invRightScroll = 0; }
            if (down) { _invCategory = (_invCategory + 1) % 4; _invRightScroll = 0; }
            if (right) { _invFocusSidebar = false; }
        }
        else if (_invCategory == 0)
        {
            // Equipment category: WASD navigates slots, A from leftmost goes to sidebar
            if (left)
            {
                if (_equipCursorX == 0) _invFocusSidebar = true;
                else _equipCursorX--;
            }
            if (right) _equipCursorX = Math.Min(2, _equipCursorX + 1);
            if (up) _equipCursorY = Math.Max(0, _equipCursorY - 1);
            if (down) _equipCursorY = Math.Min(2, _equipCursorY + 1);

            // Confirm on hand slots opens picker
            bool isHandSlot = (_equipCursorX == 0 || _equipCursorX == 2) && _equipCursorY <= 1;
            if (confirm && isHandSlot)
            {
                _equipPickerOpen = true;
                _equipPickerIndex = 0;
            }
        }
        else if (_invCategory == 3)
        {
            // Log/Bestiary: W/S navigates species list or scrolls right panel
            if (_bestiaryFocusRight)
            {
                // Right panel scrolling mode
                if (left) { _bestiaryFocusRight = false; }
                if (up) _bestiaryRightScroll = Math.Max(0, _bestiaryRightScroll - 16);
                if (down) _bestiaryRightScroll += 16;
            }
            else
            {
                if (left) { _invFocusSidebar = true; }
                int entryCount = _bestiary.Entries.Count;
                if (entryCount > 0)
                {
                    if (right) { _bestiaryFocusRight = true; _bestiaryRightScroll = 0; }
                    if (up) { _bestiarySelectedIndex = Math.Max(0, _bestiarySelectedIndex - 1); _bestiaryRightScroll = 0; }
                    if (down) { _bestiarySelectedIndex = Math.Min(entryCount - 1, _bestiarySelectedIndex + 1); _bestiaryRightScroll = 0; }
                }
            }
        }
        else
        {
            // Other categories: W/S scrolls right panel, A goes to sidebar
            if (left) { _invFocusSidebar = true; }
            if (up) _invRightScroll = Math.Max(0, _invRightScroll - 1);
            if (down) _invRightScroll++;
        }
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
        // === Equipment Screen with Sidebars ===
        // Full-screen dark overlay
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), new Color(5, 8, 15) * 0.92f);

        // Frame
        var frameColor = new Color(40, 80, 120);
        var accentColor = new Color(80, 180, 255);
        int pad = 16;
        DrawHollowRect(pad, pad, ViewW - pad * 2, ViewH - pad * 2, frameColor);

        // Title
        string title = _invCategoryNames[_invCategory].ToUpper();
        var titleSize = _fontLarge.MeasureString(title);
        DrawOutlinedString(_fontLarge, title, new Vector2(ViewW / 2f - titleSize.X / 2, pad + 8), new Color(120, 200, 255));
        _spriteBatch.Draw(_pixel, new Rectangle(pad + 4, pad + 30, ViewW - pad * 2 - 8, 1), frameColor * 0.4f);

        // Layout dimensions
        int contentY = pad + 36;
        int contentH = ViewH - pad * 2 - 70;
        int leftW = 150;
        int rightW = 220;
        int leftX = pad + 4;
        int rightX = ViewW - pad - rightW - 4;
        int centerX = leftX + leftW + 8;
        int centerW = rightX - centerX - 8;

        // === LEFT PANEL - Category Tabs ===
        _spriteBatch.Draw(_pixel, new Rectangle(leftX, contentY, leftW, contentH), new Color(15, 25, 40) * 0.6f);
        DrawHollowRect(leftX, contentY, leftW, contentH, frameColor * 0.4f);

        int tabH = 30;
        for (int i = 0; i < 4; i++)
        {
            int tabY = contentY + 8 + i * (tabH + 4);
            bool selected = i == _invCategory;
            if (selected)
            {
                Color bgColor = _invFocusSidebar ? new Color(40, 80, 120) * 0.9f : new Color(30, 60, 90) * 0.8f;
                _spriteBatch.Draw(_pixel, new Rectangle(leftX + 2, tabY, leftW - 4, tabH), bgColor);
                Color barColor = _invFocusSidebar ? Color.Yellow : accentColor;
                _spriteBatch.Draw(_pixel, new Rectangle(leftX + 2, tabY, 3, tabH), barColor);
            }
            Color tabColor = selected ? (_invFocusSidebar ? Color.Yellow : Color.White) : Color.Gray * 0.6f;
            _spriteBatch.DrawString(_font, SafeText(_invCategoryNames[i]), new Vector2(leftX + 14, tabY + 8), tabColor);
        }

        // === RIGHT PANEL ===
        _spriteBatch.Draw(_pixel, new Rectangle(rightX, contentY, rightW, contentH), new Color(15, 25, 40) * 0.6f);
        DrawHollowRect(rightX, contentY, rightW, contentH, frameColor * 0.4f);

        // === CENTER + RIGHT CONTENT based on category ===
        if (_invCategory == 0)
        {
            // Equipment: body silhouette + slots (center) + weapon stats (right)
            DrawEquipmentCenter(centerX, contentY, centerW, contentH);
            DrawEquipmentRight(rightX, contentY, rightW, contentH);
        }
        else if (_invCategory == 1)
        {
            // Suit: stats readout center + detail right
            DrawSuitCenter(centerX, contentY, centerW, contentH);
            DrawSuitRight(rightX, contentY, rightW, contentH);
        }
        else if (_invCategory == 2)
        {
            // Tools
            DrawToolsCenter(centerX, contentY, centerW, contentH);
            DrawToolsRight(rightX, contentY, rightW, contentH);
        }
        else
        {
            // Log: placeholder
            DrawLogCenter(centerX, contentY, centerW, contentH);
            DrawLogRight(rightX, contentY, rightW, contentH);
        }

        // Footer
        int footerY = ViewH - pad - 26;
        _spriteBatch.Draw(_pixel, new Rectangle(pad + 4, footerY - 4, ViewW - pad * 2 - 8, 1), frameColor * 0.3f);
        string controls;
        if (_equipPickerOpen)
            controls = "[W/S] Select  [Enter] Equip  [Esc] Back";
        else if (_invCategory == 0)
            controls = "[WASD] Navigate  [Enter] Open Slot  [Tab/Esc] Close";
        else if (_invCategory == 3)
            controls = "[W/S] Navigate  [A] Sidebar  [Tab/Esc] Close";
        else
            controls = "[WASD] Navigate/Category  [Tab/Esc] Close";
        var ctrlSize = _font.MeasureString(controls);
        _spriteBatch.DrawString(_font, SafeText(controls), new Vector2(ViewW / 2f - ctrlSize.X / 2, footerY), Color.Gray * 0.5f);
    }

    private void DrawEquipmentCenter(int cx, int cy, int cw, int ch)
    {
        // Body silhouette (centered, taller and more prominent — Super Metroid inspired)
        int bodyX = cx + cw / 2;
        int bodyY = cy + ch / 2 - 10;
        var silColor = new Color(60, 120, 180) * 0.4f;
        // Head
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 8, bodyY - 80, 16, 16), silColor);
        // Neck
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 3, bodyY - 64, 6, 4), silColor * 0.5f);
        // Torso
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 14, bodyY - 60, 28, 48), silColor);
        // Left arm
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 26, bodyY - 56, 10, 38), silColor * 0.7f);
        // Right arm
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX + 16, bodyY - 56, 10, 38), silColor * 0.7f);
        // Left leg
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 12, bodyY - 10, 10, 52), silColor * 0.8f);
        // Right leg
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX + 2, bodyY - 10, 10, 52), silColor * 0.8f);

        // Connection lines from body to slots (drawn before slots so they appear behind)
        var lineColor = new Color(40, 80, 120) * 0.3f;

        int slotW = 110, slotH = 26;

        // Armor slots — spread out around body, connected by lines
        // Helmet: above head, centered
        int helmetX = bodyX - slotW / 2;
        int helmetY = bodyY - 115;
        // Line from head top to helmet
        for (int lny = helmetY + slotH; lny < bodyY - 80; lny += 2)
            _spriteBatch.Draw(_pixel, new Rectangle(bodyX, lny, 1, 1), lineColor);

        // Chest: right side, aligned with torso
        int chestX = bodyX + 50;
        int chestY = bodyY - 50;
        // Line from torso to chest slot
        for (int lnx = bodyX + 14; lnx < chestX; lnx += 2)
            _spriteBatch.Draw(_pixel, new Rectangle(lnx, chestY + slotH / 2, 1, 1), lineColor);

        // Legs/Feet: below body, centered
        int legsX = bodyX - slotW / 2;
        int legsY = bodyY + 52;
        // Line from legs to slot
        for (int lny = bodyY + 42; lny < legsY; lny += 2)
            _spriteBatch.Draw(_pixel, new Rectangle(bodyX, lny, 1, 1), lineColor);

        // Hand slots — spread far from body
        // Left hands: far left
        int lx = cx + 8;
        int ly1 = bodyY - 44;
        int ly2 = ly1 + slotH + 10;
        // Lines from left arm to left slots
        for (int lnx = lx + slotW; lnx < bodyX - 26; lnx += 2)
            _spriteBatch.Draw(_pixel, new Rectangle(lnx, bodyY - 40, 1, 1), lineColor);

        // Right hands: far right
        int rsx = cx + cw - slotW - 8;
        int ry1 = bodyY - 44;
        int ry2 = ry1 + slotH + 10;
        // Lines from right arm to right slots
        for (int lnx = bodyX + 26; lnx < rsx; lnx += 2)
            _spriteBatch.Draw(_pixel, new Rectangle(lnx, bodyY - 40, 1, 1), lineColor);

        // Draw hand slots
        DrawEquipSlot(lx, ly1, slotW, slotH, "L1", _leftHand1, _equipCursorX == 0 && _equipCursorY == 0, _leftActiveSlot1);
        DrawEquipSlot(lx, ly2, slotW, slotH, "L2", _leftHand2, _equipCursorX == 0 && _equipCursorY == 1, !_leftActiveSlot1);
        DrawEquipSlot(rsx, ry1, slotW, slotH, "R1", _rightHand1, _equipCursorX == 2 && _equipCursorY == 0, _rightActiveSlot1);
        DrawEquipSlot(rsx, ry2, slotW, slotH, "R2", _rightHand2, _equipCursorX == 2 && _equipCursorY == 1, !_rightActiveSlot1);

        // Draw armor slots
        DrawLockedSlot(helmetX, helmetY, slotW, slotH, "Helmet", _equipCursorX == 1 && _equipCursorY == 0);
        DrawLockedSlot(chestX, chestY, slotW, slotH, "Chest", _equipCursorX == 1 && _equipCursorY == 1);
        DrawLockedSlot(legsX, legsY, slotW, slotH, "Legs", _equipCursorX == 1 && _equipCursorY == 2);

        // Weapon picker overlay
        if (_equipPickerOpen)
        {
            int pickerW = 140, pickerH = 24 + (_weaponInventory.Count + 1) * 20;
            int pickerX, pickerY;
            if (_equipCursorX == 0) { pickerX = lx + slotW + 8; pickerY = ly1; }
            else { pickerX = rsx - pickerW - 8; pickerY = ry1; }
            pickerX = Math.Clamp(pickerX, cx + 4, cx + cw - pickerW - 4);
            pickerY = Math.Clamp(pickerY, cy + 4, cy + ch - pickerH - 4);

            _spriteBatch.Draw(_pixel, new Rectangle(pickerX - 2, pickerY - 2, pickerW + 4, pickerH + 4), Color.White * 0.15f);
            _spriteBatch.Draw(_pixel, new Rectangle(pickerX, pickerY, pickerW, pickerH), new Color(10, 15, 25) * 0.95f);
            DrawHollowRect(pickerX, pickerY, pickerW, pickerH, new Color(80, 160, 220));

            _spriteBatch.DrawString(_font, "SELECT WEAPON", new Vector2(pickerX + 6, pickerY + 4), new Color(120, 200, 255));
            int py = pickerY + 24;
            bool noneSelected = _equipPickerIndex == 0;
            if (noneSelected)
                _spriteBatch.Draw(_pixel, new Rectangle(pickerX + 2, py - 1, pickerW - 4, 18), new Color(40, 80, 120) * 0.5f);
            _spriteBatch.DrawString(_font, SafeText(noneSelected ? "> (Empty)" : "  (Empty)"), new Vector2(pickerX + 6, py), noneSelected ? Color.Yellow : Color.Gray * 0.6f);
            py += 20;
            for (int i = 0; i < _weaponInventory.Count; i++)
            {
                bool sel = _equipPickerIndex == i + 1;
                if (sel)
                    _spriteBatch.Draw(_pixel, new Rectangle(pickerX + 2, py - 1, pickerW - 4, 18), new Color(40, 80, 120) * 0.5f);
                string wName = _weaponInventory[i].ToString();
                string slotTag = "";
                if (_weaponInventory[i] == _rightHand1) slotTag = " [R1]";
                else if (_weaponInventory[i] == _rightHand2) slotTag = " [R2]";
                else if (_weaponInventory[i] == _leftHand1) slotTag = " [L1]";
                else if (_weaponInventory[i] == _leftHand2) slotTag = " [L2]";
                Color wc = sel ? Color.Yellow : Color.White * 0.7f;
                _spriteBatch.DrawString(_font, SafeText($"{(sel ? "> " : "  ")}{wName}{slotTag}"), new Vector2(pickerX + 6, py), wc);
                py += 20;
            }
        }
    }

    private void DrawEquipmentRight(int rx, int ry, int rw, int rh)
    {
        // Show stats of selected weapon
        int tx = rx + 10;
        int ty = ry + 10;
        var labelColor = new Color(80, 180, 255) * 0.8f;
        var valColor = Color.White * 0.8f;

        // Determine which weapon is selected by cursor
        WeaponType selWeapon = WeaponType.None;
        if (_equipCursorX == 0 && _equipCursorY == 0) selWeapon = _leftHand1;
        else if (_equipCursorX == 0 && _equipCursorY == 1) selWeapon = _leftHand2;
        else if (_equipCursorX == 2 && _equipCursorY == 0) selWeapon = _rightHand1;
        else if (_equipCursorX == 2 && _equipCursorY == 1) selWeapon = _rightHand2;

        if (selWeapon == WeaponType.None)
        {
            _spriteBatch.DrawString(_font, "No weapon selected", new Vector2(tx, ty), Color.Gray * 0.5f);
            return;
        }

        var ws = WeaponStats.Get(selWeapon);
        DrawOutlinedString(_font, ws.DisplayName, new Vector2(tx, ty), new Color(120, 200, 255));
        ty += 22;
        _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 120) * 0.4f);
        ty += 6;

        void StatLine(string label, string val) {
            _spriteBatch.DrawString(_font, SafeText(label), new Vector2(tx, ty), labelColor);
            float labelW = _font.MeasureString(label).X;
            _spriteBatch.DrawString(_font, SafeText(val), new Vector2(tx + Math.Max(labelW + 8, 100), ty), valColor);
            ty += 16;
        }

        StatLine("Damage:", ws.Damage.ToString());
        StatLine("Finisher:", ws.FinisherDamage.ToString());
        StatLine("Speed:", $"{ws.AttackSpeed:F2}s");
        StatLine("Knockback:", $"{ws.KnockbackForce:F0}");
        StatLine("Weight:", $"{ws.Weight:P0}");
        StatLine("Type:", ws.IsRanged ? "Ranged" : "Melee");
        if (ws.IsRanged && selWeapon == WeaponType.Gun)
        {
            ty += 4;
            StatLine("Ammo:", $"{_sidearmRounds}/{SidearmClipSize}");
        }
    }

    private void DrawSuitCenter(int cx, int cy, int cw, int ch)
    {
        // Character silhouette + stat readouts
        int bodyX = cx + cw / 2;
        int bodyY = cy + ch / 2 - 40;
        var silColor = new Color(60, 120, 180) * 0.3f;
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 8, bodyY - 70, 16, 16), silColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 14, bodyY - 52, 28, 44), silColor);
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 24, bodyY - 48, 10, 34), silColor * 0.7f);
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX + 14, bodyY - 48, 10, 34), silColor * 0.7f);
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX - 10, bodyY - 6, 8, 50), silColor * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(bodyX + 2, bodyY - 6, 8, 50), silColor * 0.8f);

        // Stat readouts around silhouette
        int statX = cx + 16;
        int statY = cy + 20;
        var labelColor = new Color(80, 180, 255) * 0.9f;
        var valColor = Color.White * 0.85f;
        int lineH = 22;

        void DrawStat(string label, string val, Color vc) {
            _spriteBatch.DrawString(_font, SafeText(label), new Vector2(statX, statY), labelColor);
            float labelW = _font.MeasureString(label).X;
            _spriteBatch.DrawString(_font, SafeText(val), new Vector2(statX + Math.Max(labelW + 8, 100), statY), vc);
            statY += lineH;
        }

        DrawStat("HP:", $"{_player.Hp}/{_player.MaxHp}", _player.Hp > _player.MaxHp / 2 ? Color.LimeGreen : Color.Red);
        DrawStat("Suit:", $"{(int)_player.SuitIntegrity}%", _player.SuitIntegrity > 50 ? valColor : Color.Yellow);
        DrawStat("Battery:", $"{(int)_player.Battery}%", _player.Battery > 25 ? valColor : Color.Red);
        DrawStat("Tier:", _player.CurrentTier.ToString(), new Color(120, 200, 255));
        var save = _saveData;
        if (save != null)
        {
            DrawStat("Deaths:", save.DeathCount.ToString(), Color.Gray * 0.7f);
        }
    }

    private void DrawSuitRight(int rx, int ry, int rw, int rh)
    {
        int tx = rx + 10;
        int ty = ry + 10;
        int maxW = rw - 20;
        DrawOutlinedString(_font, "SUIT DETAILS", new Vector2(tx, ty), new Color(120, 200, 255));
        ty += 22;
        _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, maxW, 1), new Color(40, 80, 120) * 0.4f);
        ty += 8;

        var detailColor = Color.Gray * 0.6f;
        ty = DrawWrappedString(_font, "Suit integrity protects against environmental hazards and impacts.", tx, ty, maxW, detailColor, 14);
        ty += 6;
        ty = DrawWrappedString(_font, "Battery powers tech abilities: dash, grapple, shield, and EVE scans.", tx, ty, maxW, detailColor, 14);
        ty += 6;
        ty = DrawWrappedString(_font, "Tier affects movement physics and abilities.", tx, ty, maxW, detailColor, 14);
        ty += 4;
        ty = DrawWrappedString(_font, "  Tech: precision, dash", tx, ty, maxW, detailColor, 14);
        ty = DrawWrappedString(_font, "  Bio: agility, regen", tx, ty, maxW, detailColor, 14);
        ty = DrawWrappedString(_font, "  Cipher: power, speed", tx, ty, maxW, detailColor, 14);
    }

    private void DrawToolsCenter(int cx, int cy, int cw, int ch)
    {
        int tx = cx + 16;
        int ty = cy + 16;
        int lineH = 28;

        DrawOutlinedString(_font, "TOOLS", new Vector2(tx, ty), new Color(120, 200, 255));
        ty += 24;

        // Build list of acquired tools
        var tools = new List<(string name, string desc)>();
        if (_player.HasGrapple) tools.Add(("Grapple Gun", "Magnetic tether. Drains battery."));
        // Add more tools here as they're acquired:
        // if (_player.HasMap) tools.Add(("Map Module", "Area mapping system."));

        if (tools.Count == 0)
        {
            _spriteBatch.DrawString(_font, SafeText("No tools acquired yet."), new Vector2(tx, ty), Color.Gray * 0.5f);
            _spriteBatch.DrawString(_font, SafeText("Keep exploring."), new Vector2(tx, ty + 16), Color.Gray * 0.4f);
            return;
        }

        // Clamp scroll
        _invRightScroll = Math.Clamp(_invRightScroll, 0, tools.Count - 1);

        for (int i = 0; i < tools.Count; i++)
        {
            bool sel = _invRightScroll == i;
            if (sel)
                _spriteBatch.Draw(_pixel, new Rectangle(tx - 4, ty - 2, cw - 24, lineH), new Color(30, 60, 90) * 0.5f);
            _spriteBatch.DrawString(_font, SafeText(sel ? $"> {tools[i].name}" : $"  {tools[i].name}"), new Vector2(tx, ty), Color.White * 0.8f);
            ty += lineH;
        }
    }

    private void DrawToolsRight(int rx, int ry, int rw, int rh)
    {
        int tx = rx + 10;
        int ty = ry + 10;
        int maxW = rw - 20;
        DrawOutlinedString(_font, "DETAILS", new Vector2(tx, ty), new Color(120, 200, 255));
        ty += 22;
        _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, maxW, 1), new Color(40, 80, 120) * 0.4f);
        ty += 8;

        // Build same tool list as center
        var tools = new List<string>();
        if (_player.HasGrapple) tools.Add("grapple");

        if (tools.Count == 0) return;
        int idx = Math.Clamp(_invRightScroll, 0, tools.Count - 1);

        var detailColor = Color.Gray * 0.6f;
        if (tools[idx] == "grapple")
        {
            ty = DrawWrappedString(_font, "Grapple Gun", tx, ty, maxW, Color.White * 0.7f, 16);
            ty += 4;
            ty = DrawWrappedString(_font, "Fires a magnetic tether that pulls you to anchor points. Drains battery.", tx, ty, maxW, detailColor, 14);
            ty += 4;
            ty = DrawWrappedString(_font, "Cost: 0.5 battery/use", tx, ty, maxW, detailColor, 14);
        }
    }

    private void DrawLogCenter(int cx, int cy, int cw, int ch)
    {
        var cyan = new Color(80, 220, 200);
        var dimCyan = new Color(40, 120, 100);
        int tx = cx + 10;
        int ty = cy + 8;
        DrawOutlinedString(_font, "BESTIARY", new Vector2(tx, ty), cyan);
        ty += 22;
        _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, cw - 20, 1), dimCyan * 0.4f);
        ty += 6;

        var entries = new List<BestiaryEntry>(_bestiary.Entries.Values);
        if (entries.Count == 0)
        {
            _spriteBatch.DrawString(_font, SafeText("No species documented yet."), new Vector2(tx, ty), Color.Gray * 0.5f);
            return;
        }

        if (_bestiarySelectedIndex >= entries.Count) _bestiarySelectedIndex = entries.Count - 1;
        int lineH = 20;
        int maxVisible = (ch - 40) / lineH;
        int scrollStart = Math.Max(0, _bestiarySelectedIndex - maxVisible + 1);

        for (int i = scrollStart; i < entries.Count && i < scrollStart + maxVisible; i++)
        {
            var e = entries[i];
            bool sel = i == _bestiarySelectedIndex;
            Color c = sel ? Color.White : Color.Gray * 0.6f;

            // Scan-level color border: blue L1, green L2, crimson-purple L3
            Color borderColor = e.L3Scanned ? new Color(160, 40, 180) :
                                e.L2Scanned ? new Color(60, 200, 80) :
                                new Color(60, 120, 200);
            _spriteBatch.Draw(_pixel, new Rectangle(tx - 4, ty, 2, lineH), borderColor * (sel ? 0.9f : 0.4f));

            if (sel) _spriteBatch.Draw(_pixel, new Rectangle(tx - 2, ty, cw - 16, lineH), new Color(30, 60, 50) * 0.5f);

            string listName = e.SpeciesName;
            if (e.SightCount >= 5 && CreatureDatabase.Profiles.TryGetValue(e.SpeciesName, out var lp))
                listName = lp.DisplayName;

            // Truncate name to fit before progress bar
            int maxNameW = cw - 100;
            string displayName = listName;
            while (_font.MeasureString(displayName).X > maxNameW && displayName.Length > 3)
                displayName = displayName[..^1];
            if (displayName.Length < listName.Length) displayName += "..";

            _spriteBatch.DrawString(_font, SafeText(displayName), new Vector2(tx + 2, ty + 2), c);

            // Progress bar (right-aligned)
            int barW = 40;
            int barX = tx + cw - barW - 18;
            int barH = 6;
            int barY2 = ty + 7;
            _spriteBatch.Draw(_pixel, new Rectangle(barX, barY2, barW, barH), new Color(20, 40, 35));
            int fill = (int)(barW * e.L1Progress);
            if (fill > 0) _spriteBatch.Draw(_pixel, new Rectangle(barX, barY2, fill, barH), cyan * 0.7f);

            ty += lineH;
        }

        // Hint
        ty = cy + ch - 18;
        _spriteBatch.DrawString(_font, SafeText("[W/S] Navigate  [D] Details"), new Vector2(tx, ty), Color.Gray * 0.4f);
    }

    private List<string> WrapText(SpriteFontBase font, string text, float maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;
        var words = text.Split(' ');
        string current = "";
        foreach (var word in words)
        {
            string test = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureString(test).X > maxWidth)
            {
                if (current.Length > 0) lines.Add(current);
                current = word;
            }
            else current = test;
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private int DrawWrappedString(SpriteFontBase font, string text, int x, int y, float maxWidth, Color color, int lineHeight = 16)
    {
        var lines = WrapText(font, text, maxWidth);
        foreach (var line in lines)
        {
            _spriteBatch.DrawString(font, SafeText(line), new Vector2(x, y), color);
            y += lineHeight;
        }
        return y;
    }

    private void DrawLogRight(int rx, int ry, int rw, int rh)
    {
        var cyan = new Color(80, 220, 200);
        int tx = rx + 10;
        int ty = ry + 10;

        var entries = new List<BestiaryEntry>(_bestiary.Entries.Values);
        if (entries.Count == 0 || _bestiarySelectedIndex >= entries.Count)
        {
            DrawOutlinedString(_font, "LOG ENTRY", new Vector2(tx, ty), cyan);
            ty += 22;
            _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 60) * 0.4f);
            ty += 8;
            _spriteBatch.DrawString(_font, "Select an entry to view.", new Vector2(tx, ty), Color.Gray * 0.4f);
            return;
        }

        var entry = entries[_bestiarySelectedIndex];
        
        // Use CreatureDatabase for display name if available
        string displayName = entry.SpeciesName;
        CreatureProfile profile = null;
        if (CreatureDatabase.Profiles.TryGetValue(entry.SpeciesName, out var p))
        {
            profile = p;
            if (entry.SightCount >= 5) displayName = p.DisplayName;
        }

        // Title (always visible, not scrolled)
        string title = displayName.ToUpper();
        while (_font.MeasureString(title).X > rw - 24 && title.Length > 3)
            title = title[..^1];
        DrawOutlinedString(_font, title, new Vector2(tx, ty), cyan);
        ty += 22;
        _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 60) * 0.4f);
        ty += 8;

        // --- Scrollable content area ---
        int contentTop = ty;
        int contentBottom = ry + rh - 18; // leave room for scroll hint

        // End current batch, set scissor, restart
        _spriteBatch.End();
        var prevScissor = _spriteBatch.GraphicsDevice.ScissorRectangle;
        var prevRasterizer = _spriteBatch.GraphicsDevice.RasterizerState;
        var scissorRaster = new RasterizerState { ScissorTestEnable = true };
        _spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(rx, contentTop, rw, contentBottom - contentTop);
        _spriteBatch.Begin(rasterizerState: scissorRaster);

        // Apply scroll offset
        ty -= _bestiaryRightScroll;
        float maxW = rw - 20;

        // Classification — evolves with sightings
        ty = DrawWrappedString(_font, $"Class: {entry.DisplayClassification}", tx, ty, maxW, Color.White * 0.8f, 18);
        ty = DrawWrappedString(_font, $"Sightings: {entry.SightCount}", tx, ty, maxW, Color.Gray * 0.7f, 18);

        // Status line
        string status = entry.L1Progress >= 1f ? "Well-documented" :
                         entry.SightCount <= 1 ? "Barely known" :
                         entry.SightCount <= 3 ? "Tentative data" : "Building profile";
        ty = DrawWrappedString(_font, $"Status: {status}", tx, ty, maxW, cyan * 0.7f, 18);
        ty += 8;

        // EVE's current assessment — progressive reveal text
        string eveText = CreatureDatabase.GetCurrentL1Summary(entry.SpeciesName, entry.SightCount);
        if (eveText != "No data.")
        {
            _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 60) * 0.3f);
            ty += 6;
            ty = DrawWrappedString(_font, "EVE:", tx, ty, maxW, cyan * 0.8f, 16);
            ty = DrawWrappedString(_font, $"\"{eveText}\"", tx, ty, maxW, new Color(180, 220, 200) * 0.8f, 16);
            ty += 6;
        }

        // Observation summary (condensed environmental data)
        if (entry.Observations.Count > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 60) * 0.3f);
            ty += 6;
            ty = DrawWrappedString(_font, "OBSERVATIONS", tx, ty, maxW, Color.White * 0.6f, 16);
            ty += 2;

            var biomes = new HashSet<string>();
            var weathers = new HashSet<string>();
            var behaviors = new HashSet<string>();
            var times = new HashSet<string>();
            foreach (var obs in entry.Observations)
            {
                if (obs.Biome != null) biomes.Add(obs.Biome);
                if (obs.Weather != null) weathers.Add(obs.Weather);
                if (obs.Behavior != null) behaviors.Add(obs.Behavior);
                if (obs.TimeOfDay != null) times.Add(obs.TimeOfDay);
            }
            
            if (biomes.Count > 0)
                ty = DrawWrappedString(_font, $"  Habitat: {string.Join(", ", biomes)}", tx, ty, maxW, Color.Gray * 0.6f, 14);
            if (times.Count > 0)
                ty = DrawWrappedString(_font, $"  Active: {string.Join(", ", times)}", tx, ty, maxW, Color.Gray * 0.6f, 14);
            if (entry.SightCount >= 3 && behaviors.Count > 0)
                ty = DrawWrappedString(_font, $"  Behavior: {string.Join(", ", behaviors)}", tx, ty, maxW, Color.Gray * 0.6f, 14);
            if (entry.SightCount >= 4 && weathers.Count > 0)
                ty = DrawWrappedString(_font, $"  Conditions: {string.Join(", ", weathers)}", tx, ty, maxW, Color.Gray * 0.6f, 14);
        }

        // L2 data (if active-scanned)
        if (entry.L2Scanned && profile != null)
        {
            ty += 6;
            _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(40, 80, 60) * 0.3f);
            ty += 6;
            ty = DrawWrappedString(_font, "SCAN DATA", tx, ty, maxW, new Color(255, 200, 80) * 0.9f, 16);
            ty += 2;
            ty = DrawWrappedString(_font, $"  Temperament: {profile.Temperament}", tx, ty, maxW, Color.White * 0.7f, 14);
            ty = DrawWrappedString(_font, $"  Weaknesses: {profile.Weaknesses}", tx, ty, maxW, new Color(255, 120, 120) * 0.7f, 14);
            ty = DrawWrappedString(_font, $"  Resistances: {profile.Resistances}", tx, ty, maxW, new Color(120, 180, 255) * 0.7f, 14);
            ty += 4;
            ty = DrawWrappedString(_font, $"\"{profile.L2ScanText}\"", tx, ty, maxW, new Color(255, 220, 150) * 0.7f, 16);
        }

        // L3 data (if deep-scanned) — cipher empathy
        if (entry.L3Scanned && profile != null)
        {
            ty += 6;
            _spriteBatch.Draw(_pixel, new Rectangle(tx, ty, rw - 20, 1), new Color(80, 40, 80) * 0.3f);
            ty += 6;
            ty = DrawWrappedString(_font, "CIPHER SCAN", tx, ty, maxW, new Color(200, 120, 255) * 0.9f, 16);
            ty += 2;
            ty = DrawWrappedString(_font, $"\"{profile.L3ScanText}\"", tx, ty, maxW, new Color(200, 160, 255) * 0.7f, 16);
            if (profile.CreatureFeeling != null)
            {
                ty += 4;
                ty = DrawWrappedString(_font, $"  Feels: {profile.CreatureFeeling}", tx, ty, maxW, new Color(220, 180, 255) * 0.6f, 14);
            }
            if (profile.EvolutionaryHistory != null)
            {
                ty += 4;
                ty = DrawWrappedString(_font, $"  Lineage: {profile.EvolutionaryHistory}", tx, ty, maxW, new Color(180, 140, 220) * 0.5f, 14);
            }
        }

        if (entry.L1Progress >= 1f)
        {
            ty += 8;
            _spriteBatch.DrawString(_font, SafeText(">> L1 PROFILE COMPLETE <<"), new Vector2(tx, ty), cyan);
        }

        // Track total content height for scroll clamping
        int totalContentH = ty + _bestiaryRightScroll - contentTop;
        int maxScroll = Math.Max(0, totalContentH - (contentBottom - contentTop));
        _bestiaryRightScroll = Math.Clamp(_bestiaryRightScroll, 0, maxScroll);

        // End scissor batch, restore
        _spriteBatch.End();
        _spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
        _spriteBatch.GraphicsDevice.RasterizerState = prevRasterizer;
        _spriteBatch.Begin();

        // Scroll indicators
        if (_bestiaryRightScroll > 0)
            _spriteBatch.DrawString(_font, "▲", new Vector2(rx + rw - 16, contentTop - 2), cyan * 0.5f);
        if (_bestiaryRightScroll < maxScroll && maxScroll > 0)
            _spriteBatch.DrawString(_font, "▼", new Vector2(rx + rw - 16, contentBottom - 12), cyan * 0.5f);

        // Focus hint
        if (_bestiaryFocusRight)
            _spriteBatch.DrawString(_font, SafeText("[W/S] Scroll  [A] Back"), new Vector2(rx + 10, ry + rh - 16), Color.Gray * 0.4f);
    }

    private void DrawEquipSlot(int x, int y, int w, int h, string label, WeaponType weapon, bool selected, bool isActive)
    {
        var bgColor = isActive ? new Color(20, 40, 60) : new Color(12, 20, 30);
        var borderColor = selected ? new Color(120, 220, 255) : (isActive ? new Color(60, 120, 180) : new Color(30, 50, 70));
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), bgColor * 0.8f);
        DrawHollowRect(x, y, w, h, borderColor);
        if (isActive)
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 3, h), new Color(80, 200, 255)); // active indicator bar

        // Label
        _spriteBatch.DrawString(_font, label, new Vector2(x + 4, y + 2), isActive ? new Color(80, 200, 255) * 0.7f : Color.Gray * 0.4f);

        // Weapon name
        string wName = weapon != WeaponType.None ? weapon.ToString() : "---";
        Color nameColor = weapon != WeaponType.None ? (isActive ? Color.White : Color.Gray * 0.7f) : Color.Gray * 0.4f;
        _spriteBatch.DrawString(_font, SafeText(wName), new Vector2(x + 28, y + 7), nameColor);
    }

    private void DrawLockedSlot(int x, int y, int w, int h, string label, bool selected)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), new Color(15, 15, 20) * 0.6f);
        var borderColor = selected ? new Color(80, 80, 100) : new Color(30, 30, 40);
        DrawHollowRect(x, y, w, h, borderColor);
        _spriteBatch.DrawString(_font, SafeText(label), new Vector2(x + 4, y + 2), Color.Gray * 0.3f);
        _spriteBatch.DrawString(_font, "LOCKED", new Vector2(x + 28, y + 7), Color.Gray * 0.35f);
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
                int totalItems = 1 + _graphicsSettings.Length;
                if (up) _settingsItemCursor = (_settingsItemCursor - 1 + totalItems) % totalItems;
                if (down) _settingsItemCursor = (_settingsItemCursor + 1) % totalItems;
                if (confirm)
                {
                    if (_settingsItemCursor == 0)
                        ApplyWindowSize(_windowSizeIndex + 1);
                    else
                        _graphicsSettings[_settingsItemCursor - 1].Toggle();
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

    // === PROLOGUE ===
    private void StartPrologue()
    {
        _gameState = GameState.Prologue;
        _prologuePhase = 0;
        _prologueTimer = 0f;
        _prologueFadeAlpha = 1f; // start from black
        _prologueSkipTimer = 0f;
    }

    private void UpdatePrologue(KeyboardState kb, float dt)
    {
        // Hold ESC to skip (1 second hold)
        if (kb.IsKeyDown(Keys.Escape))
        {
            _prologueSkipTimer += dt;
            if (_prologueSkipTimer >= 1.0f)
            {
                _prologueSkipped = true;
                StartFadeTo(GameState.TitleCard, 2f);
                return;
            }
        }
        else
        {
            _prologueSkipTimer = Math.Max(0f, _prologueSkipTimer - dt * 2f);
        }

        // Already transitioning out — just wait for fade
        if (_prologuePhase >= ProloguePhaseDurations.Length)
            return;

        // Fade in at phase start
        if (_prologueFadeAlpha > 0f)
            _prologueFadeAlpha = Math.Max(0f, _prologueFadeAlpha - dt * 2f);

        _prologueTimer += dt;

        float phaseDuration = ProloguePhaseDurations[_prologuePhase];
        if (_prologueTimer >= phaseDuration)
        {
            _prologuePhase++;
            _prologueTimer = 0f;
            _prologueFadeAlpha = 1f; // black between phases

            if (_prologuePhase >= ProloguePhaseDurations.Length)
            {
                // Prologue done → title card
                StartFadeTo(GameState.TitleCard, 2f);
                return;
            }
        }
    }

    private void DrawPrologue()
    {
        _spriteBatch.Begin();
        float cx = ViewW / 2f;
        float cy = ViewH / 2f;

        // Phase-specific content
        switch (_prologuePhase)
        {
            case 0: // The Ship
                _spriteBatch.DrawString(_fontSmall, "Ship alarms echo through the corridor.", new Vector2(cx - 180, cy - 20), Color.Gray * (1f - _prologueFadeAlpha));
                if (_prologueTimer > 2f)
                    _spriteBatch.DrawString(_fontSmall, "EVE: \"Proximity alert. Unknown energy signature.\"", new Vector2(cx - 210, cy + 20), new Color(100, 180, 255) * Math.Min(1f, _prologueTimer - 2f));
                if (_prologueTimer > 4f)
                    _spriteBatch.DrawString(_fontSmall, "\"Recommend course correction.\"", new Vector2(cx - 140, cy + 50), new Color(100, 180, 255) * Math.Min(1f, _prologueTimer - 4f));
                break;

            case 1: // The Override
                if (_prologueTimer > 0.5f)
                    _spriteBatch.DrawString(_fontSmall, "\"There's a signal underneath it. Someone's alive down there.\"", new Vector2(cx - 270, cy - 20), new Color(200, 200, 180) * Math.Min(1f, _prologueTimer - 0.5f));
                if (_prologueTimer > 2.5f)
                    _spriteBatch.DrawString(_fontSmall, "EVE: \"The energy field is incompatible with our systems. I strongly advise—\"", new Vector2(cx - 330, cy + 20), new Color(100, 180, 255) * Math.Min(1f, _prologueTimer - 2.5f));
                if (_prologueTimer > 4f)
                    _spriteBatch.DrawString(_fontSmall, "\"Override navigation lock. Take us in.\"", new Vector2(cx - 180, cy + 60), new Color(200, 200, 180) * Math.Min(1f, _prologueTimer - 4f));
                break;

            case 2: // The Descent
                _spriteBatch.DrawString(_fontSmall, "Systems fail one by one.", new Vector2(cx - 100, cy - 40), Color.Gray * (1f - _prologueFadeAlpha));
                if (_prologueTimer > 1.5f)
                    _spriteBatch.DrawString(_fontSmall, "EVE: \"—Loss of navigation. Loss of comms. Loss of—\"", new Vector2(cx - 230, cy), new Color(100, 180, 255) * Math.Min(1f, (_prologueTimer - 1.5f) * 0.8f));
                if (_prologueTimer > 3f)
                    _spriteBatch.DrawString(_fontSmall, "[IMPACT]", new Vector2(cx - 35, cy + 40), Color.White * Math.Min(1f, _prologueTimer - 3f));
                break;

            case 3: // The Eye
                // White flash at start
                float flashAlpha = Math.Max(0f, 1f - _prologueTimer * 3f);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.White * flashAlpha);

                // Red eye — simple but iconic
                if (_prologueTimer > 0.5f)
                {
                    float eyeAlpha = Math.Min(1f, (_prologueTimer - 0.5f) * 1.5f);
                    int eyeR = 24;
                    var eyeColor = new Color(200, 30, 20) * eyeAlpha;

                    // Outer glow
                    for (int r = eyeR + 20; r > eyeR; r -= 2)
                    {
                        float glowA = eyeAlpha * 0.15f * (1f - (r - eyeR) / 20f);
                        _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - r), (int)(cy - r / 3), r * 2, r * 2 / 3), new Color(200, 30, 20) * glowA);
                    }

                    // Core eye (ellipse via stacked rectangles)
                    for (int y = -eyeR / 3; y <= eyeR / 3; y++)
                    {
                        float t = 1f - (y * y) / (float)(eyeR * eyeR / 9);
                        int hw = (int)(eyeR * Math.Sqrt(t));
                        _spriteBatch.Draw(_pixel, new Rectangle((int)(cx - hw), (int)(cy + y), hw * 2, 1), eyeColor);
                    }

                    // Pupil (bright center slit)
                    int slitH = eyeR / 2;
                    _spriteBatch.Draw(_pixel, new Rectangle((int)cx - 1, (int)cy - slitH / 2, 3, slitH), Color.White * eyeAlpha * 0.9f);
                }
                break;
        }

        // Phase fade overlay
        if (_prologueFadeAlpha > 0f)
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * _prologueFadeAlpha);

        // Skip indicator
        if (_prologueSkipTimer > 0f)
        {
            float skipPct = _prologueSkipTimer / 1.0f;
            string skipText = "Hold [ESC] to skip";
            _spriteBatch.DrawString(_fontSmall, skipText, new Vector2(ViewW - 200, ViewH - 40), Color.Gray * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(ViewW - 200, ViewH - 22, (int)(160 * skipPct), 3), Color.Gray * 0.5f);
        }

        _spriteBatch.End();
    }

    // === TITLE SCREEN ANIMATION: "The Descent" ===
    // Camera descends through: stars → atmosphere → clouds → canopy → ground
    // Total descent height: 3000 virtual pixels, auto-scrolls at ~40px/s (~75 seconds full loop)
    private const float TitleDescentHeight = 3000f;
    private const float TitleScrollSpeed = 40f;
    // Layer boundaries (Y positions in descent space)
    private const float TitleStarsEnd = 600f;      // 0-600: deep space
    private const float TitleAtmoStart = 400f;      // atmosphere entry begins
    private const float TitleAtmoEnd = 900f;        // atmosphere fully entered
    private const float TitleCloudsStart = 700f;
    private const float TitleCloudsEnd = 1400f;
    private const float TitleCanopyStart = 1200f;
    private const float TitleCanopyEnd = 2200f;
    private const float TitleGroundStart = 2000f;
    private const float TitleGroundLevel = 2600f;   // ground + crash site smoke

    private void GenerateTitleScene()
    {
        _titleRng = new Random(42);
        // Stars
        _titleStars = new TitleStar[200];
        for (int i = 0; i < _titleStars.Length; i++)
            _titleStars[i] = new TitleStar
            {
                X = _titleRng.NextSingle() * 800,
                Y = _titleRng.NextSingle() * TitleStarsEnd,
                Brightness = 0.3f + _titleRng.NextSingle() * 0.7f,
                Speed = 0.2f + _titleRng.NextSingle() * 0.8f // parallax depth
            };
        // Clouds
        _titleClouds = new TitleCloud[30];
        for (int i = 0; i < _titleClouds.Length; i++)
            _titleClouds[i] = new TitleCloud
            {
                X = _titleRng.NextSingle() * 900 - 50,
                Y = TitleCloudsStart + _titleRng.NextSingle() * (TitleCloudsEnd - TitleCloudsStart),
                W = 80 + _titleRng.NextSingle() * 200,
                H = 20 + _titleRng.NextSingle() * 40,
                Alpha = 0.15f + _titleRng.NextSingle() * 0.35f
            };
        // Trees
        _titleTrees = new TitleTree[60];
        for (int i = 0; i < _titleTrees.Length; i++)
        {
            float h = 30 + _titleRng.NextSingle() * 80;
            _titleTrees[i] = new TitleTree
            {
                X = _titleRng.NextSingle() * 800,
                BaseY = TitleGroundLevel + _titleRng.NextSingle() * 10 - 5,
                Height = h,
                Width = h * (0.3f + _titleRng.NextSingle() * 0.3f),
                Trunk = new Color(40 + _titleRng.Next(20), 25 + _titleRng.Next(15), 15),
                Canopy = new Color(20 + _titleRng.Next(30), 60 + _titleRng.Next(60), 15 + _titleRng.Next(20))
            };
        }
    }

    private void UpdateTitleAnimation(float dt)
    {
        _titleTime += dt;
        _titleScrollY += TitleScrollSpeed * dt;
        if (_titleScrollY > TitleDescentHeight)
            _titleScrollY -= TitleDescentHeight; // loop
    }

    private void DrawTitleBackground()
    {
        float camY = _titleScrollY;
        int vw = ViewW, vh = ViewH;

        // Sky color gradient based on depth
        Color skyTop, skyBot;
        if (camY < TitleAtmoStart)
        {
            // Deep space — black
            skyTop = new Color(2, 2, 8);
            skyBot = new Color(2, 2, 8);
        }
        else if (camY < TitleAtmoEnd)
        {
            float t = (camY - TitleAtmoStart) / (TitleAtmoEnd - TitleAtmoStart);
            skyTop = Color.Lerp(new Color(2, 2, 8), new Color(10, 15, 40), t);
            skyBot = Color.Lerp(new Color(2, 2, 8), new Color(25, 35, 80), t);
        }
        else if (camY < TitleCloudsEnd)
        {
            float t = (camY - TitleAtmoEnd) / (TitleCloudsEnd - TitleAtmoEnd);
            skyTop = Color.Lerp(new Color(10, 15, 40), new Color(30, 50, 90), t);
            skyBot = Color.Lerp(new Color(25, 35, 80), new Color(60, 80, 120), t);
        }
        else if (camY < TitleCanopyEnd)
        {
            float t = (camY - TitleCloudsEnd) / (TitleCanopyEnd - TitleCloudsEnd);
            skyTop = Color.Lerp(new Color(30, 50, 90), new Color(20, 40, 30), t);
            skyBot = Color.Lerp(new Color(60, 80, 120), new Color(15, 30, 20), t);
        }
        else
        {
            skyTop = new Color(20, 40, 30);
            skyBot = new Color(15, 30, 20);
        }

        // Draw gradient background (4 horizontal bands)
        for (int row = 0; row < 4; row++)
        {
            float t = row / 3f;
            var c = Color.Lerp(skyTop, skyBot, t);
            _spriteBatch.Draw(_pixel, new Rectangle(0, row * vh / 4, vw, vh / 4 + 1), c);
        }

        // Stars (visible when camY < TitleStarsEnd + vh)
        if (camY < TitleStarsEnd + vh)
        {
            float starFade = camY > TitleAtmoStart ? 1f - (camY - TitleAtmoStart) / (TitleAtmoEnd - TitleAtmoStart) : 1f;
            starFade = Math.Clamp(starFade, 0f, 1f);
            for (int i = 0; i < _titleStars.Length; i++)
            {
                ref var s = ref _titleStars[i];
                float sy = s.Y - camY * s.Speed;
                // Wrap
                while (sy < -10) sy += TitleStarsEnd + vh;
                while (sy > vh + 10) sy -= TitleStarsEnd + vh;
                if (sy < -5 || sy > vh + 5) continue;
                float twinkle = 0.7f + 0.3f * MathF.Sin(_titleTime * (1.5f + s.Speed * 2f) + s.X);
                float alpha = s.Brightness * twinkle * starFade;
                int size = s.Brightness > 0.8f ? 2 : 1;
                _spriteBatch.Draw(_pixel, new Rectangle((int)s.X, (int)sy, size, size), Color.White * alpha);
            }
        }

        // Atmosphere entry streaks (reentry particles)
        if (camY > TitleAtmoStart - 100 && camY < TitleAtmoEnd + 200)
        {
            float intensity = 1f - MathF.Abs(camY - (TitleAtmoStart + TitleAtmoEnd) / 2f) / ((TitleAtmoEnd - TitleAtmoStart) / 2f + 200);
            intensity = Math.Clamp(intensity, 0f, 1f);
            var streakRng = new Random(7);
            int count = (int)(20 * intensity);
            for (int i = 0; i < count; i++)
            {
                float sx = streakRng.NextSingle() * vw;
                float sy = streakRng.NextSingle() * vh;
                float len = 8 + streakRng.NextSingle() * 25;
                float drift = MathF.Sin(_titleTime * 0.5f + i) * 15;
                var streakColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 100, 50), streakRng.NextSingle());
                _spriteBatch.Draw(_pixel, new Rectangle((int)(sx + drift), (int)sy, 1, (int)len), streakColor * (intensity * 0.6f));
            }
        }

        // Clouds
        if (camY > TitleCloudsStart - vh && camY < TitleCloudsEnd + vh)
        {
            for (int i = 0; i < _titleClouds.Length; i++)
            {
                ref var c = ref _titleClouds[i];
                float cy2 = c.Y - camY * 0.7f;
                float drift = MathF.Sin(_titleTime * 0.3f + c.X * 0.01f) * 20;
                if (cy2 < -c.H - 50 || cy2 > vh + 50) continue;
                var cloudColor = new Color(180, 190, 210) * c.Alpha;
                // Soft cloud: 3 overlapping ovals
                _spriteBatch.Draw(_pixel, new Rectangle((int)(c.X + drift), (int)cy2, (int)c.W, (int)c.H), cloudColor);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(c.X + drift + c.W * 0.2f), (int)(cy2 - c.H * 0.3f), (int)(c.W * 0.6f), (int)(c.H * 0.8f)), cloudColor * 0.7f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(c.X + drift - c.W * 0.1f), (int)(cy2 + c.H * 0.1f), (int)(c.W * 0.5f), (int)(c.H * 0.6f)), cloudColor * 0.5f);
            }
        }

        // Canopy (vertical lines for tree trunks + leafy blobs)
        if (camY > TitleCanopyStart - vh && camY < TitleCanopyEnd + vh)
        {
            float canopyAlpha = Math.Clamp((camY - TitleCanopyStart + vh * 0.3f) / (float)vh, 0f, 1f);
            var leafRng = new Random(99);
            for (int i = 0; i < 40; i++)
            {
                float lx = leafRng.NextSingle() * vw;
                float ly = TitleCanopyStart + leafRng.NextSingle() * (TitleCanopyEnd - TitleCanopyStart);
                float screenY = ly - camY * 0.6f;
                if (screenY < -60 || screenY > vh + 60) continue;
                int lw = 30 + leafRng.Next(50);
                int lh = 20 + leafRng.Next(35);
                float sway = MathF.Sin(_titleTime * 0.5f + lx * 0.02f) * 8;
                var leafColor = new Color(15 + leafRng.Next(25), 50 + leafRng.Next(50), 10 + leafRng.Next(20));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(lx + sway), (int)screenY, lw, lh), leafColor * (canopyAlpha * 0.7f));
            }
            // Light shafts through canopy
            float shaftAlpha = Math.Clamp((camY - TitleCanopyStart) / 400f, 0f, 0.15f);
            for (int i = 0; i < 5; i++)
            {
                float sx = 100 + i * 160 + MathF.Sin(_titleTime * 0.2f + i) * 30;
                _spriteBatch.Draw(_pixel, new Rectangle((int)sx, 0, 3, vh), new Color(255, 240, 200) * shaftAlpha);
            }
        }

        // Ground + trees
        if (camY > TitleGroundStart - vh)
        {
            float groundScreenY = TitleGroundLevel - camY * 0.85f;
            // Ground
            if (groundScreenY < vh)
            {
                int gy = Math.Max(0, (int)groundScreenY);
                _spriteBatch.Draw(_pixel, new Rectangle(0, gy, vw, vh - gy), new Color(20, 35, 18));
                // Grass texture lines
                var grassRng = new Random(33);
                for (int i = 0; i < 40; i++)
                {
                    int gx = grassRng.Next(vw);
                    int gh = 3 + grassRng.Next(6);
                    _spriteBatch.Draw(_pixel, new Rectangle(gx, gy - gh, 1, gh), new Color(30, 55, 25) * 0.6f);
                }
            }

            // Trees
            for (int i = 0; i < _titleTrees.Length; i++)
            {
                ref var tr = ref _titleTrees[i];
                float treeBaseY = tr.BaseY - camY * 0.85f;
                if (treeBaseY < -tr.Height - 20 || treeBaseY > vh + 20) continue;
                // Trunk
                int tw = Math.Max(2, (int)(tr.Width * 0.15f));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(tr.X - tw / 2), (int)(treeBaseY - tr.Height * 0.6f), tw, (int)(tr.Height * 0.6f)), tr.Trunk);
                // Canopy blob
                int cw = (int)tr.Width;
                int ch = (int)(tr.Height * 0.5f);
                float sway = MathF.Sin(_titleTime * 0.4f + tr.X * 0.01f) * 3;
                _spriteBatch.Draw(_pixel, new Rectangle((int)(tr.X - cw / 2 + sway), (int)(treeBaseY - tr.Height), cw, ch), tr.Canopy);
            }

            // Crash site smoke (rising wisps near center)
            if (groundScreenY < vh + 50)
            {
                for (int i = 0; i < 8; i++)
                {
                    float smokeAge = (_titleTime * 0.5f + i * 1.3f) % 4f;
                    float sx = vw / 2f + MathF.Sin(smokeAge * 2f + i) * 15;
                    float sy = groundScreenY - smokeAge * 30;
                    float sa = (1f - smokeAge / 4f) * 0.25f;
                    int ss = (int)(3 + smokeAge * 4);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)sx, (int)sy, ss, ss), Color.Gray * sa);
                }
            }
        }
    }

    // === TITLE CARD ===
    private void UpdateTitleCard(float dt)
    {
        _titleCardTimer += dt;

        if (_titleCardTimer < 1f)
            _titleCardFade = Math.Min(1f, _titleCardTimer * 2f); // fade in over 0.5s
        else if (_titleCardTimer > 2.5f)
            _titleCardFade = Math.Max(0f, 1f - (_titleCardTimer - 2.5f) * 2f); // fade out over 0.5s

        if (_titleCardTimer >= 3.5f)
        {
            // After title card, load level and start playing
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
            SyncInventoryToSave();
            _saveData.Save();
            // Reset overworld
            if (System.IO.File.Exists(OverworldPath))
                _overworld = OverworldData.Load(OverworldPath);
            else
                _overworld = new OverworldData();
            foreach (var n in _overworld.Nodes)
            {
                n.Discovered = n.Id == _overworld.StartNode;
                n.Cleared = false;
            }
            _overworld.Save(OverworldPath);
            _currentNodeId = _overworld.StartNode;
            if (System.IO.File.Exists("Content/worldmap.json"))
                System.IO.File.Delete("Content/worldmap.json");
            _worldMap = null;
            if (System.IO.Directory.Exists("Content/sim"))
                foreach (var f in System.IO.Directory.GetFiles("Content/sim", "*.json"))
                    System.IO.File.Delete(f);
            _titleCardTimer = 0f;
            _fadeAlpha = 1f; // start gameplay from black, fade in
            
            if (_prologueSkipped)
            {
                // Skipped prologue — no wake-up, no EVE. Player must find her.
                _wakeUpComplete = true;
                _player.IsLyingDown = false;
                _camera.Zoom = 1f;
                _camera.TargetZoom = 1f;
                _eveOrbActive = false;
            }
            else
            {
                // Full cinematic wake-up sequence
                _camera.Zoom = 2.5f;
                _camera.TargetZoom = 2.5f;
                _camera.ZoomLerpSpeed = 0.5f;
                _camera.SnapTo(_player.Position, Player.Width, Player.Height);
                _wakeUpTimer = 0f;
                _wakeUpPhase = 0;
                _player.IsLyingDown = true;
                _player.StandUpProgress = 0f;
                _player.IsInjured = true;
                _player.Hp = 2; // barely survived the crash
                _player.SuitIntegrity = 31f;
                _player.Battery = 80f;
                _player.ApplyTierConstants(); // apply injured debuff
            }
        }
    }

    private void DrawTitleCard()
    {
        _spriteBatch.Begin();
        string title = "GENESYS";
        var titleSize = _fontLarge.MeasureString(title);
        float cx = ViewW / 2f;
        float cy = ViewH / 2f;
        _spriteBatch.DrawString(_fontLarge, title, new Vector2(cx - titleSize.X / 2, cy - titleSize.Y / 2), Color.White * _titleCardFade);
        _spriteBatch.End();
    }

    // === FADE TRANSITION ===
    private void StartFadeTo(GameState target, float speed = 1.5f, Action callback = null)
    {
        _fadingOut = true;
        _fadeSpeed = speed;
        _fadeTargetState = target;
        _fadeCallback = callback;
    }

    private void DrawFadeOverlay()
    {
        if (_fadeAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * _fadeAlpha);
            _spriteBatch.End();
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
            DrawOutlinedString(_fontSmall, point.Label, new Vector2(px + ts / 2f - labelSize.X / 2, py - 14), Color.White * 0.8f);
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
    // ===================== WEATHER SYSTEM =====================
    private void UpdateWeather(float dt)
    {
        // Clouds
        if (_weatherRain || _weatherStorm)
        {
            // Spawn clouds if needed
            while (_clouds.Count < 8)
            {
                float cx = _rng.Next((int)_level.Bounds.Left - 200, (int)_level.Bounds.Right + 200);
                float cy = _level.Bounds.Top + _rng.Next(20, 80);
                _clouds.Add(new Cloud
                {
                    X = cx, Y = cy,
                    W = 80 + _rng.Next(120),
                    H = 20 + _rng.Next(15),
                    Speed = 10 + (float)_rng.NextDouble() * 20f,
                    Opacity = 0.3f + (float)_rng.NextDouble() * 0.4f
                });
            }
            // Move clouds
            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                float windPush = _weatherWind ? _windDir * _windStrength * 0.5f : 0;
                c.X += (c.Speed + windPush) * dt;
                if (c.X > _level.Bounds.Right + 300) c.X = _level.Bounds.Left - 300;
                if (c.X < _level.Bounds.Left - 300) c.X = _level.Bounds.Right + 300;
                _clouds[i] = c;
            }
        }
        else
        {
            _clouds.Clear();
        }

        // Rain drops
        if (_weatherRain)
        {
            // Spawn rain
            int spawnCount = _weatherStorm ? 12 : 5;
            float camLeft = _player.Position.X - ViewW / 2f - 50;
            float camRight = _player.Position.X + ViewW / 2f + 50;
            float camTop = _player.Position.Y - ViewH / 2f - 20;
            for (int i = 0; i < spawnCount; i++)
            {
                _rainDrops.Add(new RainDrop
                {
                    X = camLeft + (float)_rng.NextDouble() * (camRight - camLeft),
                    Y = camTop,
                    Speed = 300 + (float)_rng.NextDouble() * 200f,
                    Length = 4 + (float)_rng.NextDouble() * 8f
                });
            }

            // Update drops
            float windPush = _weatherWind ? _windDir * _windStrength : 0;
            for (int i = _rainDrops.Count - 1; i >= 0; i--)
            {
                var d = _rainDrops[i];
                d.Y += d.Speed * dt;
                d.X += windPush * dt;
                _rainDrops[i] = d;

                // Remove if below floor or too many
                if (d.Y > _level.Floor.Y + 20)
                {
                    // Splash particle on impact
                    if (_dustParticlesEnabled && _rng.NextDouble() < 0.3)
                    {
                        _particles.Add(new Particle
                        {
                            Position = new Vector2(d.X, _level.Floor.Y),
                            Velocity = new Vector2((float)(_rng.NextDouble() * 30 - 15), -(float)(_rng.NextDouble() * 40)),
                            Life = 0.2f,
                            Color = new Color(100, 140, 180) * 0.6f
                        });
                    }
                    _rainDrops.RemoveAt(i);
                }
            }
            // Cap
            if (_rainDrops.Count > 600) _rainDrops.RemoveRange(0, _rainDrops.Count - 600);
        }
        else
        {
            _rainDrops.Clear();
        }

        // Lightning
        if (_weatherStorm)
        {
            _lightningTimer -= dt;
            if (_lightningTimer <= 0)
            {
                _lightningTimer = 2f + (float)_rng.NextDouble() * 5f;
                _lightningFlash = 0.3f;
                if (_screenShakeEnabled) { _shakeTimer = 0.2f; _shakeIntensity = 4f; }
            }
            if (_lightningFlash > 0) _lightningFlash -= dt * 2f;
        }
        else
        {
            _lightningFlash = 0;
        }
    }

    private void DrawWeather()
    {
        // Wind visual — horizontal streaks/debris particles
        if (_weatherWind)
        {
            float windX = _windDir * _windStrength;
            for (int i = 0; i < 12; i++)
            {
                // Deterministic but scrolling streaks
                float seed = i * 7331f;
                float baseX = _player.Position.X + MathF.Sin(seed) * ViewW * 0.8f;
                float baseY = _player.Position.Y - ViewH * 0.4f + (seed % ViewH);
                // Scroll with wind
                float scroll = (_totalTime * windX * 2f + seed * 13f) % (ViewW * 2f) - ViewW;
                float sx = baseX + scroll;
                float sy = baseY + MathF.Sin(_totalTime * 1.5f + seed * 0.1f) * 15f;
                int streakLen = 6 + (int)(seed % 10);
                var streakColor = new Color(180, 200, 180) * (0.15f + (seed % 5) * 0.03f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)sx, (int)sy, streakLen, 1), streakColor);
            }
        }

        // Clouds (behind everything, drawn in world space)
        foreach (var c in _clouds)
        {
            float alpha = c.Opacity * (_weatherStorm ? 0.7f : 0.4f);
            var cloudColor = _weatherStorm ? new Color(40, 40, 50) : new Color(160, 170, 180);
            // Cloud body (layered rects for puffy shape)
            _spriteBatch.Draw(_pixel, new Rectangle((int)c.X, (int)c.Y, (int)c.W, (int)c.H), cloudColor * alpha);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(c.X + c.W * 0.1f), (int)(c.Y - c.H * 0.3f), (int)(c.W * 0.8f), (int)(c.H * 0.5f)), cloudColor * (alpha * 0.8f));
            _spriteBatch.Draw(_pixel, new Rectangle((int)(c.X + c.W * 0.25f), (int)(c.Y - c.H * 0.5f), (int)(c.W * 0.5f), (int)(c.H * 0.4f)), cloudColor * (alpha * 0.6f));
        }

        // Rain drops
        float windAngle = _weatherWind ? _windDir * 0.15f : 0;
        var rainColor = _weatherStorm ? new Color(150, 170, 200) * 0.7f : new Color(120, 150, 200) * 0.5f;
        foreach (var d in _rainDrops)
        {
            float endX = d.X + windAngle * d.Length;
            // Draw as a thin line (1px wide rect angled via two small rects)
            _spriteBatch.Draw(_pixel, new Rectangle((int)d.X, (int)d.Y, 1, (int)d.Length), rainColor);
        }
    }

    private void DrawWeatherOverlay()
    {
        // Lightning flash (screen-space overlay)
        if (_lightningFlash > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.White * (_lightningFlash * 0.6f));
        }

        // Ambient darkening during storms
        if (_weatherStorm)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.15f);
        }
        else if (_weatherRain)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * 0.05f);
        }
    }

    /// <summary>Good hash for Voronoi — produces well-distributed 0..1 values from integer coords.</summary>
    private static float Hash2D(int x, int y, int seed)
    {
        uint h = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
        h ^= h >> 16; h *= 0x45d9f3b; h ^= h >> 16; h *= 0x45d9f3b; h ^= h >> 16;
        return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
    }

    /// <summary>Compute Voronoi caustic value at a world position. Returns 0..1 brightness.</summary>
    private float VoronoiCaustic(float worldX, float worldY, float cellSize, float time, float timeScale, float power)
    {
        float invCell = 1f / cellSize;
        float wx = worldX * invCell;
        float wy = worldY * invCell;
        int cellX = (int)MathF.Floor(wx);
        int cellY = (int)MathF.Floor(wy);

        float d1 = 99f, d2 = 99f;
        for (int cy = -1; cy <= 1; cy++)
        {
            for (int cx = -1; cx <= 1; cx++)
            {
                int cxi = cellX + cx;
                int cyi = cellY + cy;
                float hx = Hash2D(cxi, cyi, 0);
                float hy = Hash2D(cxi, cyi, 1);
                // Seed point: offset within cell + animated orbit
                float seedX = cxi + 0.1f + hx * 0.8f + MathF.Sin(time * timeScale + hx * 6.28f) * 0.3f;
                float seedY = cyi + 0.1f + hy * 0.8f + MathF.Cos(time * timeScale * 0.85f + hy * 6.28f) * 0.3f;
                float dx = wx - seedX;
                float dy = wy - seedY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < d1) { d2 = d1; d1 = dist; }
                else if (dist < d2) { d2 = dist; }
            }
        }

        // The classic caustic formula: the EDGE value (d2 - d1) inverted and raised to high power
        // Small d2-d1 = near a ridge = bright. Large = inside a cell = dark.
        float edge = d2 - d1;
        // Normalize roughly (max edge in Voronoi ~0.8) and invert
        float v = MathHelper.Clamp(1f - edge * 1.5f, 0f, 1f);
        // Power function crushes everything except the brightest ridge peaks
        return MathF.Pow(v, power);
    }

    /// <summary>Draw an animated liquid tile (water, lava, acid). Uses Voronoi cellular noise for caustic light patterns.</summary>
    // === NOISE-BASED WATER SHADER SYSTEM ===

    private Texture2D GenerateNoiseTexture(int w, int h, int seed, float scale)
    {
        var rng = new Random(seed);
        // Generate a grid of random values for value noise
        int gridW = (int)(w / scale) + 2;
        int gridH = (int)(h / scale) + 2;
        float[,] grid = new float[gridW + 1, gridH + 1];
        for (int gy = 0; gy <= gridH; gy++)
            for (int gx = 0; gx <= gridW; gx++)
                grid[gx, gy] = (float)rng.NextDouble();

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x / scale;
                float fy = y / scale;
                int ix = (int)fx;
                int iy = (int)fy;
                float tx = fx - ix;
                float ty = fy - iy;
                // Smoothstep
                tx = tx * tx * (3f - 2f * tx);
                ty = ty * ty * (3f - 2f * ty);
                // Bilinear interpolation
                float v00 = grid[ix, iy];
                float v10 = grid[ix + 1, iy];
                float v01 = grid[ix, iy + 1];
                float v11 = grid[ix + 1, iy + 1];
                float v0 = v00 + (v10 - v00) * tx;
                float v1 = v01 + (v11 - v01) * tx;
                float v = v0 + (v1 - v0) * ty;

                // Add a second octave for detail
                float fx2 = x / (scale * 0.5f);
                float fy2 = y / (scale * 0.5f);
                int ix2 = (int)fx2 % gridW;
                int iy2 = (int)fy2 % gridH;
                float tx2 = fx2 - (int)fx2;
                float ty2 = fy2 - (int)fy2;
                tx2 = tx2 * tx2 * (3f - 2f * tx2);
                ty2 = ty2 * ty2 * (3f - 2f * ty2);
                float w00 = grid[ix2, iy2];
                float w10 = grid[Math.Min(ix2 + 1, gridW), iy2];
                float w01 = grid[ix2, Math.Min(iy2 + 1, gridH)];
                float w11 = grid[Math.Min(ix2 + 1, gridW), Math.Min(iy2 + 1, gridH)];
                float w0 = w00 + (w10 - w00) * tx2;
                float w1 = w01 + (w11 - w01) * tx2;
                float v2 = w0 + (w1 - w0) * ty2;

                float combined = v * 0.7f + v2 * 0.3f;
                byte b = (byte)(combined * 255);
                pixels[y * w + x] = new Color(b, b, b, (byte)255);
            }
        }

        var tex = new Texture2D(GraphicsDevice, w, h);
        tex.SetData(pixels);
        return tex;
    }

    /// <summary>
    /// Draws noise-based water for all visible water tiles in one pass.
    /// Uses two scrolling noise textures overlaid with water tinting + transparency variation.
    /// Also applies sinusoidal refraction distortion to the scene beneath water.
    /// </summary>
    private void DrawWaterShaderPass(TileGrid tg, Matrix cameraTransform)
    {
        // Collect visible water tiles
        var camInv = Matrix.Invert(cameraTransform);
        var tl = Vector2.Transform(Vector2.Zero, camInv);
        var br = Vector2.Transform(new Vector2(ViewW, ViewH), camInv);
        int stx = Math.Max(0, ((int)tl.X - tg.OriginX) / tg.TileSize - 1);
        int sty = Math.Max(0, ((int)tl.Y - tg.OriginY) / tg.TileSize - 1);
        int etx = Math.Min(tg.Width, ((int)br.X - tg.OriginX) / tg.TileSize + 2);
        int ety = Math.Min(tg.Height, ((int)br.Y - tg.OriginY) / tg.TileSize + 2);

        var waterTiles = new List<(int wx, int wy, int tx, int ty)>();
        for (int ty = sty; ty < ety; ty++)
            for (int tx = stx; tx < etx; tx++)
                if (tg.GetTileAt(tx, ty) == TileType.Water)
                    waterTiles.Add((tg.OriginX + tx * tg.TileSize, tg.OriginY + ty * tg.TileSize, tx, ty));

        if (waterTiles.Count == 0) return;

        float t = _totalTime;
        int ts = tg.TileSize; // 32

        // End current sprite batch to do our water pass
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, transformMatrix: cameraTransform);

        // Noise scrolling parameters — two layers moving in different directions
        float scrollAx = t * 12f;
        float scrollAy = t * 4f;
        float scrollBx = -t * 7f;
        float scrollBy = t * 9f;

        // Noise UV scale — controls how zoomed-in the noise appears
        // Lower = larger blobs (more visible noise pattern)
        float uvScaleA = 0.4f;  // large blobs
        float uvScaleB = 1.3f;  // finer detail

        foreach (var (wx, wy, tx, ty) in waterTiles)
        {
            bool aboveLiquid = ty > 0 && TileProperties.IsLiquid(tg.GetTileAt(tx, ty - 1));
            bool isSurface = !aboveLiquid;

            // Calculate depth (how many water tiles above)
            int depth = 0;
            for (int cy = ty - 1; cy >= 0; cy--)
            {
                if (TileProperties.IsLiquid(tg.GetTileAt(tx, cy))) depth++;
                else break;
            }

            // Depth factor for darkening (deeper = darker, more opaque)
            float depthFactor = MathHelper.Clamp(depth * 0.12f, 0f, 0.6f);

            int surfaceWaveTop = isSurface ? 6 : 0; // leave room for wavy surface edge

            // --- Surface wavy edge ---
            if (isSurface)
            {
                for (int px = 0; px < ts; px++)
                {
                    float wX = wx + px;
                    // Organic wave from combined sin + noise
                    float w1 = MathF.Sin(wX * 0.05f + t * 0.5f) * 3.0f;
                    float w2 = MathF.Sin(wX * 0.11f + t * 0.3f + 2f) * 1.8f;
                    float w3_raw = MathF.Sin(wX * 0.03f + t * 0.2f) * 1.5f;
                    int waveY = 5 + (int)(w1 + w2 + w3_raw);
                    waveY = Math.Clamp(waveY, 2, ts - 2);

                    // Bright surface highlight line (2px)
                    float glint = 0.5f + 0.5f * MathF.Sin(wX * 0.3f + t * 2.2f);
                    var surfColor = Color.Lerp(new Color(80, 170, 235), new Color(200, 240, 255), glint * glint);
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + waveY, 1, 2), surfColor * (0.7f + glint * 0.3f));

                    // Fill from waveY+2 to bottom of tile with noise water (1px per pixel)
                    for (int py = waveY + 2; py < ts; py++)
                    {
                        float wY = wy + py;
                        float nA = SampleNoise(_noiseDataA, (wX + scrollAx) * uvScaleA, (wY + scrollAy) * uvScaleA);
                        float nB = SampleNoise(_noiseDataB, (wX + scrollBx) * uvScaleB, (wY + scrollBy) * uvScaleB);
                        float combined = (nA + nB) * 0.5f;

                        float r = MathHelper.Lerp(120f, 10f, combined) / 255f;
                        float g = MathHelper.Lerp(210f, 30f, combined) / 255f;
                        float b2 = MathHelper.Lerp(245f, 130f, combined) / 255f;
                        var waterColor = new Color(r, g, b2);

                        float alpha = MathHelper.Lerp(0.15f, 0.85f, combined);
                        alpha = MathHelper.Clamp(alpha + depthFactor, 0f, 0.92f);

                        _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + py, 1, 1), waterColor * alpha);
                    }
                }
            }
            else
            {
                // --- Fully submerged tile: noise-driven water (1px per pixel) ---
                for (int px = 0; px < ts; px++)
                {
                    for (int py = 0; py < ts; py++)
                    {
                        float wX = wx + px;
                        float wY = wy + py;

                        float nA = SampleNoise(_noiseDataA, (wX + scrollAx) * uvScaleA, (wY + scrollAy) * uvScaleA);
                        float nB = SampleNoise(_noiseDataB, (wX + scrollBx) * uvScaleB, (wY + scrollBy) * uvScaleB);
                        float combined = (nA + nB) * 0.5f;

                        float dd = MathHelper.Clamp(1f - depth * 0.06f, 0.35f, 1f);

                        float r = MathHelper.Lerp(100f, 8f, combined) * dd / 255f;
                        float g = MathHelper.Lerp(190f, 25f, combined) * dd / 255f;
                        float b2 = MathHelper.Lerp(240f, 120f, combined) * dd / 255f;
                        var waterColor = new Color(r, g, b2);

                        float alpha = MathHelper.Lerp(0.1f, 0.9f, combined);
                        alpha = MathHelper.Clamp(alpha + depthFactor, 0f, 0.95f);

                        _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + py, 1, 1), waterColor * alpha);
                    }
                }

                // --- Refraction: sinusoidal distortion light/dark bands ---
                for (int py = 0; py < ts; py += 2)
                {
                    float wY = wy + py;
                    float sinOff = MathF.Sin(wY * 0.12f + t * 0.9f + wx * 0.01f);
                    float nR = SampleNoise(_noiseDataB, (wx + t * 3f) * 0.5f, (wY + t * 2f) * 0.5f);
                    float band = sinOff * nR;
                    if (band > 0.25f)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + py, ts, 2), Color.White * ((band - 0.25f) * 0.2f));
                    else if (band < -0.25f)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + py, ts, 2), Color.Black * ((-band - 0.25f) * 0.15f));
                }
            }

            // --- Caustic sparkles (sparse, bright) ---
            if (depth >= 0)
            {
                float sparkleScale = 0.6f;
                int cStep = 4;
                for (int py = (isSurface ? 10 : 0); py < ts; py += cStep)
                {
                    for (int px = 0; px < ts; px += cStep)
                    {
                        float wX = wx + px;
                        float wY = wy + py;
                        float nA = SampleNoise(_noiseDataA, (wX + t * 6f) * sparkleScale, (wY + t * 3f) * sparkleScale);
                        float nB = SampleNoise(_noiseDataB, (wX - t * 4f) * sparkleScale, (wY + t * 7f) * sparkleScale);
                        float sparkle = nA * nB;
                        if (sparkle > 0.4f)
                        {
                            float intensity = (sparkle - 0.4f) / 0.6f;
                            float depthFade = MathHelper.Clamp(1f - depth * 0.12f, 0.1f, 0.7f);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + py, cStep, cStep),
                                new Color(180, 230, 255) * (intensity * depthFade * 0.5f));
                        }
                    }
                }
            }

            // --- Bubbles ---
            {
                int bseed = tx * 7919 + ty * 104729;
                for (int b = 0; b < 3; b++)
                {
                    int bs = bseed + b * 31337;
                    float bx = bs % ts;
                    float byOffset = ((t * 0.6f * 12f + bs * 0.01f) % (ts + 8)) - 4;
                    float by = wy + ts - byOffset;
                    int topY = wy + (isSurface ? 8 : 0);
                    if (by >= topY && by < wy + ts - 2)
                    {
                        int bsize = 1 + (bs % 2);
                        float wobble = MathF.Sin(t * 2.5f + bs) * 1.5f;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + (int)(bx + wobble), (int)by, bsize, bsize),
                            new Color(180, 220, 255) * 0.35f);
                    }
                }
            }

            // --- Edge darkening ---
            bool leftLiquid = tx > 0 && TileProperties.IsLiquid(tg.GetTileAt(tx - 1, ty));
            bool rightLiquid = tx < tg.Width - 1 && TileProperties.IsLiquid(tg.GetTileAt(tx + 1, ty));
            bool belowLiquid = ty < tg.Height - 1 && TileProperties.IsLiquid(tg.GetTileAt(tx, ty + 1));
            if (!leftLiquid)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 2, ts), Color.Black * 0.15f);
            if (!rightLiquid)
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 2, wy, 2, ts), Color.Black * 0.15f);
            if (!belowLiquid)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 2, ts, 2), Color.Black * 0.2f);
        }
    }

    /// <summary>Sample cached noise texture data with bilinear interpolation. Returns 0-1.</summary>
    private float SampleNoise(byte[] data, float u, float v)
    {
        // Wrap to 0-255
        u = ((u % 256f) + 256f) % 256f;
        v = ((v % 256f) + 256f) % 256f;
        int x0 = (int)u;
        int y0 = (int)v;
        int x1 = (x0 + 1) & 255;
        int y1 = (y0 + 1) & 255;
        float fx = u - x0;
        float fy = v - y0;
        x0 &= 255;
        y0 &= 255;
        float v00 = data[y0 * 256 + x0];
        float v10 = data[y0 * 256 + x1];
        float v01 = data[y1 * 256 + x0];
        float v11 = data[y1 * 256 + x1];
        float top = v00 + (v10 - v00) * fx;
        float bot = v01 + (v11 - v01) * fx;
        return (top + (bot - top) * fy) / 255f;
    }

    /// <summary>Fast noise texture sampling — delegates to cached texture data.</summary>
    private float SampleNoiseTexFast(float worldX, float worldY, float noiseScale)
    {
        float u = worldX * noiseScale * 256f / 32f; // scale relative to tile size
        float v = worldY * noiseScale * 256f / 32f;
        return SampleNoise(_noiseDataA, u, v);
    }

    private void DrawLiquidTile(int wx, int wy, int ts, TileType tile, TileGrid tg, int tx, int ty)
    {
        float t = _totalTime;
        
        // --- Determine liquid colors ---
        Color tintColor, surfaceColor, highlightColor, foamColor;
        float speed;
        
        switch (tile)
        {
            case TileType.Lava:
                tintColor = new Color(180, 40, 10) * 0.55f;
                surfaceColor = new Color(255, 120, 20);
                highlightColor = new Color(255, 200, 80);
                foamColor = new Color(255, 160, 40);
                speed = 1.4f;
                break;
            case TileType.Acid:
                tintColor = new Color(30, 140, 20) * 0.5f;
                surfaceColor = new Color(80, 220, 50);
                highlightColor = new Color(160, 255, 120);
                foamColor = new Color(100, 240, 70);
                speed = 0.8f;
                break;
            default: // Water
                tintColor = new Color(30, 70, 130) * 0.45f;
                surfaceColor = new Color(60, 140, 200);
                highlightColor = new Color(160, 210, 255);
                foamColor = new Color(180, 220, 255);
                speed = 0.6f;
                break;
        }
        
        // --- Neighbor checks ---
        bool aboveLiquid = ty > 0 && TileProperties.IsLiquid(tg.GetTileAt(tx, ty - 1));
        bool belowLiquid = ty < tg.Height - 1 && TileProperties.IsLiquid(tg.GetTileAt(tx, ty + 1));
        bool leftLiquid = tx > 0 && TileProperties.IsLiquid(tg.GetTileAt(tx - 1, ty));
        bool rightLiquid = tx < tg.Width - 1 && TileProperties.IsLiquid(tg.GetTileAt(tx + 1, ty));
        bool isSurface = !aboveLiquid;
        
        // Depth (how many liquid tiles above this one)
        int depth = 0;
        for (int checkY = ty - 1; checkY >= 0; checkY--)
        {
            if (TileProperties.IsLiquid(tg.GetTileAt(tx, checkY))) depth++;
            else break;
        }
        
        // --- 1) Semi-transparent color overlay (ground shows through) ---
        float baseAlpha = 0.35f + depth * 0.08f;
        if (baseAlpha > 0.7f) baseAlpha = 0.7f;
        
        int bodyTop = 0;
        
        if (isSurface)
        {
            // Surface tile: draw wavy top edge, body below
            for (int px = 0; px < ts; px++)
            {
                float wX = wx + px;
                float w1 = MathF.Sin(wX * 0.08f + t * speed) * 3f;
                float w2 = MathF.Sin(wX * 0.15f + t * speed * 0.6f + 1.5f) * 1.5f;
                int waveY = 4 + (int)(w1 + w2);
                if (waveY < 1) waveY = 1;
                if (waveY > ts - 2) waveY = ts - 2;
                
                int colH = ts - waveY;
                if (colH > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + waveY, 1, colH), tintColor * baseAlpha);
            }
            bodyTop = 4;
        }
        else
        {
            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, ts), tintColor * baseAlpha);
        }
        
        // --- 2) Animated ripple lines ---
        {
            float rippleSpeed = speed * 0.4f;
            float spacing = 8f;
            int startPy = isSurface ? bodyTop + 2 : 0;
            
            for (int py = startPy; py < ts; py++)
            {
                float worldY = wy + py;
                float r1 = MathF.Sin(worldY * (MathF.PI * 2f / spacing) + t * rippleSpeed);
                float r2 = MathF.Sin(worldY * (MathF.PI * 2f / (spacing * 1.3f)) + t * rippleSpeed * 0.7f + 2f);
                float combined = (r1 + r2) * 0.5f;
                
                if (combined > 0.6f)
                {
                    float xOff = MathF.Sin(worldY * 0.1f + t * speed * 0.3f) * 4f;
                    int lineX = (int)xOff;
                    float intensity = (combined - 0.6f) * 2.5f;
                    int lineW = (int)(ts * 0.4f + ts * 0.5f * intensity);
                    int lx = wx + (ts - lineW) / 2 + lineX;
                    
                    float depthFade = 1f - depth * 0.12f;
                    if (depthFade < 0.2f) depthFade = 0.2f;
                    
                    var rippleColor = Color.Lerp(surfaceColor, highlightColor, intensity * 0.5f);
                    _spriteBatch.Draw(_pixel, new Rectangle(lx, wy + py, lineW, 1),
                        rippleColor * (0.3f * intensity * depthFade));
                }
            }
        }
        
        // --- 3) Surface detail: foam line + zigzag ripples ---
        if (isSurface)
        {
            for (int px = 0; px < ts; px++)
            {
                float wX = wx + px;
                float w1 = MathF.Sin(wX * 0.08f + t * speed) * 3f;
                float w2 = MathF.Sin(wX * 0.15f + t * speed * 0.6f + 1.5f) * 1.5f;
                int waveY = 4 + (int)(w1 + w2);
                if (waveY < 1) waveY = 1;
                if (waveY > ts - 2) waveY = ts - 2;
                
                // Bright surface line
                _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + waveY, 1, 2), surfaceColor * 0.8f);
                
                // Specular sparkle
                float glint = MathF.Sin(wX * 0.25f + t * speed * 3f);
                if (glint > 0.75f)
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + waveY, 1, 1),
                        Color.White * ((glint - 0.75f) * 3f));
                
                // Foam dots below surface
                if (px % 4 == ((int)(t * 8) % 4))
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + waveY + 3, 2, 1), foamColor * 0.35f);
            }
            
            // Animated zigzag ripple lines below surface
            for (int line = 0; line < 2; line++)
            {
                int lineBaseY = wy + 10 + line * 8;
                if (lineBaseY >= wy + ts) break;
                float linePhase = t * speed * 1.5f + line * 1.8f;
                
                for (int px = 0; px < ts; px += 2)
                {
                    float wX = wx + px;
                    float zigzag = MathF.Sin(wX * 0.2f + linePhase) * 2f;
                    int ly = lineBaseY + (int)zigzag;
                    if (ly >= wy && ly < wy + ts)
                    {
                        float fade = 0.4f - line * 0.12f;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + px, ly, 2, 1), highlightColor * fade);
                    }
                }
            }
        }
        
        // --- 4) Subtle caustics (deep tiles only) ---
        if (depth > 0 || !isSurface)
        {
            int step = 4;
            float s = speed * 0.4f;
            float depthFade = MathHelper.Clamp(1f - depth * 0.15f, 0.15f, 0.8f);
            int startY = isSurface ? bodyTop : 0;
            
            for (int py = startY; py < ts; py += step)
            {
                for (int px = 0; px < ts; px += step)
                {
                    float wX = wx + px;
                    float wY = wy + py;
                    float v1 = MathF.Sin(wX * 0.07f + wY * 0.04f + t * s);
                    float v2 = MathF.Sin(wX * -0.05f + wY * 0.08f + t * s * 0.7f + 1.3f);
                    float sum = (v1 + v2 + 2f) * 0.25f;
                    float caustic = sum * sum * sum * depthFade;
                    
                    if (caustic > 0.08f)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + px, wy + py, step, step),
                            highlightColor * (caustic * 0.25f));
                }
            }
        }
        
        // --- 5) Bubbles ---
        {
            int seed = tx * 7919 + ty * 104729;
            for (int b = 0; b < 2; b++)
            {
                int bseed = seed + b * 31337;
                float bx = bseed % ts;
                float byOffset = ((t * speed * 12f + bseed * 0.01f) % (ts + 8)) - 4;
                float by = wy + ts - byOffset;
                if (by >= wy + (isSurface ? bodyTop : 0) && by < wy + ts - 2)
                {
                    int bsize = 1 + (bseed % 2);
                    float wobble = MathF.Sin(t * 2.5f + bseed) * 1.5f;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + (int)(bx + wobble), (int)by, bsize, bsize),
                        highlightColor * 0.25f);
                }
            }
        }
        
        // --- 6) Edge darkening ---
        if (!leftLiquid)
            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), Color.Black * 0.2f);
        if (!rightLiquid)
            _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), Color.Black * 0.2f);
        if (!belowLiquid)
            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), Color.Black * 0.25f);
    }


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

    /// <summary>Draw 3D-bevel outline tracing only the filled area of the slope.</summary>
    private void DrawSlopeOutline(int wx, int wy, int ts, TileType tile, Color lightColor, Color darkColor)
    {
        // Diagonal edge matching DrawSlopeTile fill geometry exactly
        for (int row = 0; row < ts; row++)
        {
            int edgeX = -1;
            Color edgeColor = lightColor;
            switch (tile)
            {
                // 45° floor slopes: diagonal is the surface (top-facing = light)
                case TileType.SlopeUpRight: // fill left edge = wx + (ts-1-row)
                    edgeX = wx + (ts - 1 - row); edgeColor = lightColor; break;
                case TileType.SlopeUpLeft: // fill right edge = wx + row
                    edgeX = wx + row; edgeColor = lightColor; break;
                // 45° ceiling slopes: diagonal is bottom-facing = dark
                case TileType.SlopeCeilRight: // fill left edge = wx + row
                    edgeX = wx + row; edgeColor = darkColor; break;
                case TileType.SlopeCeilLeft: // fill right edge = wx + (ts-1-row)
                    edgeX = wx + (ts - 1 - row); edgeColor = darkColor; break;
                // Gentle floor: surface from halfway down
                case TileType.GentleUpRight:
                    if (row >= ts / 2) { int surfX = (int)((ts - row) * 2f); if (surfX < ts) edgeX = wx + surfX; }
                    edgeColor = lightColor; break;
                case TileType.GentleUpLeft:
                    if (row >= ts / 2) { int fillW = (int)((row - ts / 2) * 2f); edgeX = wx + fillW; }
                    edgeColor = lightColor; break;
                // Gentle ceiling: surface from top to halfway
                case TileType.GentleCeilRight:
                    if (row * 2 < ts) { edgeX = wx + row * 2; }
                    edgeColor = darkColor; break;
                case TileType.GentleCeilLeft:
                    if (row * 2 < ts) { edgeX = wx + ts - 1 - row * 2; }
                    edgeColor = darkColor; break;
                // Shaved floor: top corner cut off, mostly full block
                case TileType.ShavedRight: // top-right shaved: diagonal at right edge of fill for row < ts/2
                    if (row < ts / 2) { edgeX = wx + (int)(row * 2f); }
                    edgeColor = lightColor; break;
                case TileType.ShavedLeft: // top-left shaved: diagonal at left edge of fill for row < ts/2
                    if (row < ts / 2) { int cut = ts - (int)(row * 2f) - 1; if (cut >= 0) edgeX = wx + cut; }
                    edgeColor = lightColor; break;
                // Shaved ceiling: bottom corner cut off
                case TileType.ShavedCeilRight: // bottom-right shaved
                    if (row > ts / 2) { int sr = row - ts / 2; int w2 = ts - sr * 2; if (w2 > 0) edgeX = wx + w2 - 1; }
                    edgeColor = darkColor; break;
                case TileType.ShavedCeilLeft: // bottom-left shaved
                    if (row > ts / 2) { int sr = row - ts / 2; edgeX = wx + sr * 2; }
                    edgeColor = darkColor; break;

                // Gentle4 floor slopes (4-tile, quarter-height rise per tile)
                case TileType.Gentle4UpRightA:
                case TileType.Gentle4UpRightB:
                case TileType.Gentle4UpRightC:
                case TileType.Gentle4UpRightD:
                    {
                        int qi = tile - TileType.Gentle4UpRightA;
                        int qBase = ts - qi * (ts / 4);
                        int qTop = qBase - ts / 4;
                        if (row >= qTop && row < qBase)
                        {
                            int surfX = (qBase - row) * 4;
                            if (surfX >= 0 && surfX < ts) edgeX = wx + surfX;
                        }
                        edgeColor = lightColor;
                    }
                    break;
                case TileType.Gentle4UpLeftA:
                case TileType.Gentle4UpLeftB:
                case TileType.Gentle4UpLeftC:
                case TileType.Gentle4UpLeftD:
                    {
                        int qi = tile - TileType.Gentle4UpLeftA;
                        int qBase = ts - qi * (ts / 4);
                        int qTop = qBase - ts / 4;
                        if (row >= qTop && row < qBase)
                        {
                            int fillW = (row - qTop) * 4;
                            if (fillW >= 0 && fillW < ts) edgeX = wx + fillW;
                        }
                        edgeColor = lightColor;
                    }
                    break;
                case TileType.Gentle4CeilRightA:
                case TileType.Gentle4CeilRightB:
                case TileType.Gentle4CeilRightC:
                case TileType.Gentle4CeilRightD:
                    {
                        int qi = tile - TileType.Gentle4CeilRightA;
                        int surfLeft = qi * (ts / 4);
                        int surfRight = surfLeft + ts / 4;
                        if (row > surfLeft && row <= surfRight)
                        {
                            int sx = (row - surfLeft) * 4;
                            if (sx >= 0 && sx < ts) edgeX = wx + sx;
                        }
                        edgeColor = darkColor;
                    }
                    break;
                case TileType.Gentle4CeilLeftA:
                case TileType.Gentle4CeilLeftB:
                case TileType.Gentle4CeilLeftC:
                case TileType.Gentle4CeilLeftD:
                    {
                        int qi = tile - TileType.Gentle4CeilLeftA;
                        int surfRight = qi * (ts / 4);
                        int surfLeft = surfRight + ts / 4;
                        if (row > surfRight && row <= surfLeft)
                        {
                            int sx = (row - surfRight) * 4;
                            int ex = ts - sx;
                            if (ex >= 0 && ex < ts) edgeX = wx + ex;
                        }
                        edgeColor = darkColor;
                    }
                    break;
            }
            if (edgeX >= wx && edgeX < wx + ts)
                _spriteBatch.Draw(_pixel, new Rectangle(edgeX, wy + row, 1, 1), edgeColor);
        }

        // Straight edges only where fill is solid along the full edge
        switch (tile)
        {
            case TileType.SlopeUpRight: // bottom full, right edge full (tall side)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), darkColor);
                break;
            case TileType.SlopeUpLeft: // bottom full, left edge full (tall side)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), lightColor);
                break;
            case TileType.SlopeCeilRight: // top full, right edge full (tall side)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), darkColor);
                break;
            case TileType.SlopeCeilLeft: // top full, left edge full (tall side)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), lightColor);
                break;
            case TileType.GentleUpRight: // bottom full, right edge from ts/2 to bottom
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy + ts / 2, 1, ts / 2), darkColor);
                break;
            case TileType.GentleUpLeft: // bottom full, left edge from ts/2 to bottom
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts / 2, 1, ts / 2), lightColor);
                break;
            case TileType.GentleCeilRight: // top full, right edge from top to ts/2
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts / 2), darkColor);
                break;
            case TileType.GentleCeilLeft: // top full, left edge from top to ts/2
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor);
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts / 2), lightColor);
                break;
            case TileType.ShavedRight: // top, left, bottom full (top-right corner shaved)
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), lightColor); // left
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor); // bottom
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy + ts / 2, 1, ts / 2), darkColor); // right below shave
                break;
            case TileType.ShavedLeft: // top, right, bottom full (top-left corner shaved)
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), darkColor); // right
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor); // bottom
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts / 2, 1, ts / 2), lightColor); // left below shave
                break;
            case TileType.ShavedCeilRight: // top, left full, bottom-right shaved
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor); // top
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts), lightColor); // left
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts / 2), darkColor); // right above shave
                break;
            case TileType.ShavedCeilLeft: // top, right full, bottom-left shaved
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor); // top
                _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, ts), darkColor); // right
                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts / 2), lightColor); // left above shave
                break;

            // Gentle4 floor slopes — straight edges
            case TileType.Gentle4UpRightA:
            case TileType.Gentle4UpRightB:
            case TileType.Gentle4UpRightC:
            case TileType.Gentle4UpRightD:
                {
                    int qi = tile - TileType.Gentle4UpRightA;
                    int qBase = ts - qi * (ts / 4);
                    int qTop = qBase - ts / 4;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor); // bottom
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy + qTop, 1, ts - qTop), darkColor); // right from surface top down
                    if (qBase < ts) // left edge below surface (where fill is full-width)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + qBase, 1, ts - qBase), lightColor);
                }
                break;
            case TileType.Gentle4UpLeftA:
            case TileType.Gentle4UpLeftB:
            case TileType.Gentle4UpLeftC:
            case TileType.Gentle4UpLeftD:
                {
                    int qi = tile - TileType.Gentle4UpLeftA;
                    int qBase = ts - qi * (ts / 4);
                    int qTop = qBase - ts / 4;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts - 1, ts, 1), darkColor); // bottom
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + qTop, 1, ts - qTop), lightColor); // left from surface top down
                    if (qBase < ts) // right edge below surface
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy + qBase, 1, ts - qBase), darkColor);
                }
                break;
            case TileType.Gentle4CeilRightA:
            case TileType.Gentle4CeilRightB:
            case TileType.Gentle4CeilRightC:
            case TileType.Gentle4CeilRightD:
                {
                    int qi = tile - TileType.Gentle4CeilRightA;
                    int surfLeft = qi * (ts / 4);
                    int surfRight = surfLeft + ts / 4;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor); // top
                    _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, surfRight), darkColor); // right from top to surface bottom
                    if (surfLeft > 0) // left edge above surface (where fill is full-width)
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, surfLeft), lightColor);
                }
                break;
            case TileType.Gentle4CeilLeftA:
            case TileType.Gentle4CeilLeftB:
            case TileType.Gentle4CeilLeftC:
            case TileType.Gentle4CeilLeftD:
                {
                    int qi = tile - TileType.Gentle4CeilLeftA;
                    int surfRight = qi * (ts / 4);
                    int surfLeft = surfRight + ts / 4;
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts, 1), lightColor); // top
                    _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, surfLeft), lightColor); // left from top to surface bottom
                    if (surfRight > 0) // right edge above surface
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + ts - 1, wy, 1, surfRight), darkColor);
                }
                break;
        }
    }

    /// <summary>Draw a spike tile (triangular spikes pointing in a direction).</summary>
    private void DrawSpikeTile(int wx, int wy, int ts, TileType tile, Color color)
    {
        bool isRetract = TileProperties.IsRetractable(tile);
        float ext = 1f;
        if (isRetract && _level.TileGridInstance != null)
        {
            ext = _level.TileGridInstance.RetractExtension;
            if (ext <= 0.01f) return; // fully retracted, don't draw
            // Map retract types to their base spike direction
            if (tile == TileType.RetractSpikesUp) tile = TileType.Spikes;
            else if (tile == TileType.RetractSpikesDown) tile = TileType.SpikesDown;
            else if (tile == TileType.RetractSpikesLeft) tile = TileType.SpikesLeft;
            else if (tile == TileType.RetractSpikesRight) tile = TileType.SpikesRight;
            else if (tile == TileType.RetractHalfSpikesUp) tile = TileType.HalfSpikesUp;
            else if (tile == TileType.RetractHalfSpikesDown) tile = TileType.HalfSpikesDown;
            else if (tile == TileType.RetractHalfSpikesLeft) tile = TileType.HalfSpikesLeft;
            else if (tile == TileType.RetractHalfSpikesRight) tile = TileType.HalfSpikesRight;
            color = Color.Lerp(color * 0.3f, color, ext); // fade when retracting
        }
        
        bool isHalf = tile >= TileType.HalfSpikesUp && tile <= TileType.HalfSpikesRight;
        bool up = tile == TileType.Spikes || tile == TileType.HalfSpikesUp;
        bool down = tile == TileType.SpikesDown || tile == TileType.HalfSpikesDown;
        bool left = tile == TileType.SpikesLeft || tile == TileType.HalfSpikesLeft;
        bool right = tile == TileType.SpikesRight || tile == TileType.HalfSpikesRight;
        
        int n = 4; // number of spikes
        
        if (up || down)
        {
            int h = isHalf ? ts / 2 : ts;
            if (isRetract) h = (int)(h * ext); // shrink height by extension
            if (h < 1) return;
            int sw = ts / n;
            int oy = 0;
            if (up) oy = ts - h; // base at bottom, tips retract downward
            // DOWN: oy=0, tips retract upward
            
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
            if (isRetract) w = (int)(w * ext); // shrink width by extension
            if (w < 1) return;
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
        // CRT: redirect all drawing to render target
        if (_crtEnabled && _crtTarget != null)
            GraphicsDevice.SetRenderTarget(_crtTarget);

        bool isDebugLevel = _editorSaveFile.Contains("debug", StringComparison.OrdinalIgnoreCase);
        GraphicsDevice.Clear(isDebugLevel ? new Color(25, 10, 35) : new Color(20, 20, 20));

        // Editor draw
        if (_gameState == GameState.Editing)
        {
            DrawEditor();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        // Prologue draw
        if (_gameState == GameState.Prologue)
        {
            GraphicsDevice.Clear(Color.Black);
            DrawPrologue();
            DrawFadeOverlay();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        // Title card draw
        if (_gameState == GameState.TitleCard)
        {
            GraphicsDevice.Clear(Color.Black);
            DrawTitleCard();
            DrawFadeOverlay();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        if (_gameState == GameState.Overworld)
        {
            DrawOverworld();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        if (_gameState == GameState.SimMode)
        {
            DrawSimMode();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        if (_gameState == GameState.Title)
        {
            _spriteBatch.Begin();
            DrawTitleBackground();

            // Title text with subtle glow
            string title = "GENESYS";
            var titleSize = _fontLarge.MeasureString(title);
            float cx = ViewW / 2f;
            float cy = ViewH / 2f;
            // Shadow/glow
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0)
                        _spriteBatch.DrawString(_fontLarge, title, new Vector2(cx - titleSize.X / 2 + ox, cy - 130 + oy), new Color(80, 140, 200) * 0.3f);
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
            DrawFadeOverlay();
            DrawCRT();
            base.Draw(gameTime);
            return;
        }

        if (_isDead) GraphicsDevice.Clear(Color.DarkRed);

        // --- World rendering (camera transform + screen shake) ---
        var shakeOff = Matrix.Identity;
        if (_shakeTimer > 0)
        {
            float sx = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
            float sy = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
            shakeOff = Matrix.CreateTranslation(sx, sy, 0);
        }

        // --- Parallax background layers ---
        DrawParallaxBackground(shakeOff);

        _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix * shakeOff);

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

        if (floorH > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, floorH), isDebugLevel ? new Color(140, 80, 160) : new Color(40, 40, 40));
            _spriteBatch.Draw(_pixel, new Rectangle(bL, floorY, bR - bL, 1), isDebugLevel ? new Color(190, 160, 100) : new Color(80, 80, 80));
        }

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

        // Draw weather (clouds + rain in world space, behind tiles)
        DrawWeather();

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
                        DrawSlopeTile(wx, wy, tg.TileSize, tile, isDebugLevel ? new Color(140, 80, 160) : color);
                        if (isDebugLevel)
                        {
                            DrawSlopeOutline(wx, wy, tg.TileSize, tile, new Color(190, 160, 100), new Color(80, 60, 30));
                        }
                    }
                    else if (TileProperties.IsLiquid(tile))
                    {
                        if (!(_useShaderWater && tile == TileType.Water))
                            DrawLiquidTile(wx, wy, tg.TileSize, tile, tg, tx, ty);
                    }
                    else if (TileProperties.IsEffectTile(tile) || tile == TileType.Breakable)
                    {
                        // Effect tiles keep their distinct colored appearance in all levels
                        int tsE = tg.TileSize;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tsE, tsE), color * 0.4f);
                        var bright = new Color(
                            Math.Min(255, color.R + 80), Math.Min(255, color.G + 80), Math.Min(255, color.B + 80));
                        var border = bright * 0.7f;
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tsE, 1), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + tsE - 1, tsE, 1), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, tsE), border);
                        _spriteBatch.Draw(_pixel, new Rectangle(wx + tsE - 1, wy, 1, tsE), border);
                    }
                    else
                    {
                        if (isDebugLevel)
                        {
                            // SotN-style debug tiles: vibrant purple fill, gold 3D-bevel grid
                            int ts3 = tg.TileSize;
                            // Subtle shade variation per tile
                            int shade = ((tx * 7 + ty * 13) % 5) - 2; // -2 to +2
                            var tileColor = new Color(
                                Math.Clamp(140 + shade * 5, 125, 155),
                                Math.Clamp(80 + shade * 3, 68, 92),
                                Math.Clamp(160 + shade * 6, 145, 175));
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts3, ts3), tileColor);
                            // 3D bevel: light gold top-left, dark bottom-right
                            var lightGold = new Color(190, 160, 100);
                            var darkGold = new Color(80, 60, 30);
                            // Top edge (light)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts3, 1), lightGold);
                            // Left edge (light)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts3), lightGold);
                            // Bottom edge (dark)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts3 - 1, ts3, 1), darkGold);
                            // Right edge (dark)
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + ts3 - 1, wy, 1, ts3), darkGold);
                        }
                        else
                        {
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, tg.TileSize), color);
                            if (tile == TileType.Grass)
                                _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 4), TileProperties.GetAccentColor(tile));
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, tg.TileSize, 1), Color.White * 0.1f);
                            var dark = new Color(
                                (int)(color.R * 0.5f), (int)(color.G * 0.5f), (int)(color.B * 0.5f));
                            int ts3 = tg.TileSize;
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, ts3, 1), dark);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy + ts3 - 1, ts3, 1), dark);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx, wy, 1, ts3), dark);
                            _spriteBatch.Draw(_pixel, new Rectangle(wx + ts3 - 1, wy, 1, ts3), dark);
                        }
                    }
                }
            }
        }

        // Noise-based water shader pass (draws all Water tiles with noise overlay)
        if (_useShaderWater && _level.TileGridInstance != null)
        {
            var shakeOff2 = Matrix.Identity;
            if (_shakeTimer > 0)
            {
                float sx2 = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
                float sy2 = (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeIntensity;
                shakeOff2 = Matrix.CreateTranslation(sx2, sy2, 0);
            }
            DrawWaterShaderPass(_level.TileGridInstance, _camera.TransformMatrix * shakeOff2);
        }

        // Draw ropes (always visible — disabling rope climb only prevents grabbing)
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

        // Draw spatial labels (gameplay)
        foreach (var lbl in _level.Labels)
        {
            var lblFont = lbl.Size switch { "large" => _fontLarge, "normal" => _font, _ => _fontSmall };
            var lblColor = lbl.Color switch
            {
                "Green" => Color.LimeGreen,
                "Yellow" => Color.Yellow,
                "Red" => Color.OrangeRed,
                "Cyan" => Color.Cyan,
                "Orange" => Color.Orange,
                "Gray" => Color.Gray,
                "Purple" => Color.MediumPurple,
                _ => Color.White
            };
            DrawOutlinedString(lblFont, lbl.Text, new Vector2(lbl.X, lbl.Y), lblColor * 0.9f);
        }

        // Draw switches
        foreach (var sw in _level.Switches)
        {
            bool isToggle = sw.Action.StartsWith("toggle-") || sw.Action.StartsWith("grant-");
            bool activated = _activatedSwitches.Contains(sw.Id);
            // For toggle/grant switches, check current state
            bool isOn = isToggle ? (sw.Action switch {
                "toggle-rain" => _weatherRain,
                "toggle-storm" => _weatherStorm,
                "toggle-wind" => _weatherWind,
                "toggle-all-weather" => _weatherRain && _weatherStorm && _weatherWind,
                "grant-slide" => _enableSlide,
                "grant-dash" => _enableDash,
                "grant-double-jump" => _enableDoubleJump,
                "grant-wall-climb" => _enableWallClimb,
                "grant-drop-through" => _enableDropThrough,
                "grant-vault-kick" => _enableVaultKick,
                "grant-uppercut" => _enableUppercut,
                "grant-cartwheel" => _enableCartwheel,
                "grant-flip" => _enableFlip,
                "grant-blade-dash" => _enableBladeDash,
                "grant-spin-melee" => _enableSpinMelee,
                "grant-rope-climb" => _enableRopeClimb,
                _ => activated
            }) : activated;
            var swColor = isOn ? new Color(50, 180, 50) : new Color(200, 180, 50);
            if (!isToggle && activated) swColor = new Color(60, 60, 60);
            // Draw switch body
            _spriteBatch.Draw(_pixel, new Rectangle((int)sw.X, (int)sw.Y, sw.W, sw.H), swColor);
            // Draw lever/handle
            int handleY = isOn ? (int)sw.Y + sw.H - 6 : (int)sw.Y + 2;
            _spriteBatch.Draw(_pixel, new Rectangle((int)sw.X + 2, handleY, sw.W - 4, 4), isOn ? Color.Gray : Color.White);
            // Draw label above
            if (!string.IsNullOrEmpty(sw.Label))
                DrawOutlinedString(_fontSmall, sw.Label, new Vector2(sw.X - 20, sw.Y - 16), isOn ? new Color(100, 200, 100) : Color.Yellow);
            // Draw "W" prompt when player is near
            {
                float dist = Math.Abs(_player.Position.X + Player.Width / 2f - (sw.X + sw.W / 2f));
                if (dist < 60)
                    DrawOutlinedString(_fontSmall, "[W]", new Vector2(sw.X - 2, sw.Y - 28), Color.White * 0.8f);
            }
        }

        // Draw shelters
        if (_level.Shelters != null)
        {
            foreach (var sh in _level.Shelters)
            {
                int sx = (int)sh.X - 16, sy = (int)sh.Y - 30;
                // Leaf shelter: triangular roof + trunk
                var leafGreen = new Color(50, 90, 35);
                var leafDark = new Color(30, 60, 20);
                var trunk = new Color(90, 60, 30);
                // Trunk
                _spriteBatch.Draw(_pixel, new Rectangle(sx + 14, sy + 12, 4, 16), trunk);
                // Leaf canopy (layered triangles)
                for (int row = 0; row < 12; row++)
                {
                    int w = 32 - row * 2;
                    int ox = row;
                    var c = (row % 3 == 0) ? leafDark : leafGreen;
                    _spriteBatch.Draw(_pixel, new Rectangle(sx + ox, sy + row, w, 1), c);
                }
                // Ground leaves
                _spriteBatch.Draw(_pixel, new Rectangle(sx + 2, sy + 28, 28, 2), leafDark * 0.5f);

                // Prompt
                if (_nearShelter && _currentShelter == sh)
                {
                    float holdPct = MathHelper.Clamp(_shelterRestTimer / 0.8f, 0, 1);
                    string prompt = _isResting ? "Resting..." : _shelterPromptText ?? "[W] Rest";
                    var promptPos = new Vector2(sx - 8, sy - 16);
                    DrawOutlinedString(_fontSmall, SafeText(prompt), promptPos, Color.White * 0.9f);
                    // Hold progress bar
                    if (holdPct > 0 && !_isResting)
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(sx, sy - 4, 32, 3), Color.Black * 0.5f);
                        _spriteBatch.Draw(_pixel, new Rectangle(sx, sy - 4, (int)(32 * holdPct), 3), Color.Green * 0.8f);
                    }
                }
            }
        }

        // Draw item pickups (gameplay)
        for (int i = 0; i < _itemPickups.Count; i++)
        {
            var item = _itemPickups[i];
            if (!item.Collected)
            {
                var itemColor = item.ItemType switch
                {
                    "knife" => new Color(200, 200, 210),
                    "grapple" => new Color(70, 90, 110),
                    "stick" => new Color(139, 90, 43),
                    "whip" => new Color(100, 60, 30),
                    "dagger" => new Color(180, 180, 200),
                    "sling" => Color.DarkKhaki,
                    "sword" => Color.Silver,
                    "greatsword" => new Color(200, 200, 220),
                    "axe" => Color.DarkGray,
                    "club" => new Color(110, 70, 35),
                    "greatclub" => new Color(90, 55, 25),
                    "hammer" => new Color(160, 160, 170),
                    "bow" => new Color(160, 120, 60),
                    "gun" => Color.SlateGray,
                    "heart" => Color.Red,
                    "battery" => Color.Yellow,
                    "map-module" => Color.Cyan,
                    _ => Color.White
                };
                // Draw with glow so it's visible
                _spriteBatch.Draw(_pixel, new Rectangle(item.Rect.X - 2, item.Rect.Y - 2, item.Rect.Width + 4, item.Rect.Height + 4), Color.White * 0.25f);
                _spriteBatch.Draw(_pixel, item.Rect, itemColor);
                // Heart items: no pickup prompt, auto-collect on touch
                if (item.ItemType == "heart" || item.ItemType == "battery") continue;
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
            foreach (var creature in _creatures) { if (!_enemySquashEnabled) creature.VisualScale = Vector2.One; creature.Draw(_spriteBatch, _pixel); }
        }

        // --- EVE Scan overlays (world-space) ---
        // Reusable triangulation scan effect
        void DrawTriangulationScan(Vector2 anchor, Vector2 targetCenter, float time, float alpha)
        {
            // Two vertices orbit independently around the target
            float t1 = time * 1.3f;
            float t2 = time * 0.9f + 2.1f;
            Vector2 pA = new(
                targetCenter.X + MathF.Cos(t1) * 10f,
                targetCenter.Y + MathF.Sin(t1 * 1.7f) * 22f);
            Vector2 pB = new(
                targetCenter.X + MathF.Cos(t2 * 1.4f) * 8f,
                targetCenter.Y + MathF.Sin(t2) * 20f);
            
            // Fill triangle (semi-transparent)
            // Scanline fill: for each row between min/max Y, draw horizontal span
            int minY = (int)Math.Min(anchor.Y, Math.Min(pA.Y, pB.Y));
            int maxY = (int)Math.Max(anchor.Y, Math.Max(pA.Y, pB.Y));
            float fillAlpha = alpha * 0.25f;
            for (int row = minY; row <= maxY; row += 2) // every other row for scanline look
            {
                // Find x-intercepts of triangle edges at this row
                float[] xs = new float[6];
                int xCount = 0;
                void EdgeIntersect(Vector2 e1, Vector2 e2)
                {
                    if ((e1.Y <= row && e2.Y >= row) || (e2.Y <= row && e1.Y >= row))
                    {
                        float ey = e2.Y - e1.Y;
                        if (MathF.Abs(ey) < 0.01f) return;
                        float t = (row - e1.Y) / ey;
                        if (xCount < 6) xs[xCount++] = e1.X + t * (e2.X - e1.X);
                    }
                }
                EdgeIntersect(anchor, pA);
                EdgeIntersect(pA, pB);
                EdgeIntersect(pB, anchor);
                if (xCount >= 2)
                {
                    float xMin = Math.Min(xs[0], xs[1]);
                    float xMax = Math.Max(xs[0], xs[1]);
                    int w = Math.Max(1, (int)(xMax - xMin));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)xMin, row, w, 1), Color.Cyan * fillAlpha);
                }
            }
            
            // Triangle edges (dashed)
            void DrawScanEdge(Vector2 a, Vector2 b, float edgeAlpha)
            {
                int steps = (int)Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
                if (steps < 1) return;
                for (int i = 0; i <= steps; i += 2)
                {
                    float lt = i / (float)steps;
                    int px = (int)MathHelper.Lerp(a.X, b.X, lt);
                    int py = (int)MathHelper.Lerp(a.Y, b.Y, lt);
                    _spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, 1), Color.Cyan * edgeAlpha);
                }
            }
            DrawScanEdge(anchor, pA, alpha);
            DrawScanEdge(anchor, pB, alpha);
            DrawScanEdge(pA, pB, alpha * 1.2f);
            
            // Pulsing dots at vertices
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 5f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)pA.X - 1, (int)pA.Y - 1, 3, 3), Color.Cyan * pulse);
            _spriteBatch.Draw(_pixel, new Rectangle((int)pB.X - 1, (int)pB.Y - 1, 3, 3), Color.Cyan * (1f - pulse));
        }
        
        // Scan beam during scanning (enemy scan)
        if (_isScanning)
        {
            float beamAlpha = 0.4f + 0.3f * MathF.Sin(_scanPulseTimer * 8f);
            DrawTriangulationScan(_eveOrbActive ? _evePos : PlayerCenter, _scanTargetPos, _totalTime, beamAlpha);
            // Scan progress bar above target
            float progress = 1f - _scanTimer / L2ScanDuration;
            int barW = 24, barH = 3;
            int barX = (int)(_scanTargetPos.X - barW / 2f);
            int barY = (int)(_scanTargetPos.Y - 16);
            // Bar color: green safe, yellow if enemies near EVE, red if very close
            float nearestThreat = float.MaxValue;
            foreach (var cr in _creatures)
            {
                if (!cr.Alive || cr == _scanTargetCreature) continue;
                float td = Vector2.Distance(_evePos, cr.Position + new Vector2(cr.Rect.Width / 2f, cr.Rect.Height / 2f));
                if (td < nearestThreat) nearestThreat = td;
            }
            Color barColor = nearestThreat < 30f ? Color.Red : nearestThreat < 60f ? Color.Yellow : Color.LimeGreen;
            _spriteBatch.Draw(_pixel, new Rectangle(barX, barY, barW, barH), Color.Black * 0.5f);
            _spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barW * progress), barH), barColor);
        }
        // EVE wake-up scan (uses same triangulation)
        if (_eveOrbActive && _eveMode == EveMovementMode.Scan)
        {
            float scanAlpha = 0.15f + 0.1f * MathF.Sin(_totalTime * 2.5f);
            var pc2 = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            DrawTriangulationScan(_evePos, pc2, _totalTime, scanAlpha);
        }
        // Enemy labels and HP bars based on scan level
        void DrawScanOverlay(string type, Vector2 pos, int w, int h, int hp, int maxHp)
        {
            if (!_scanProgress.TryGetValue(type, out int lvl) || lvl < 1) return;
            // Level 1+: name label
            string name = char.ToUpper(type[0]) + type[1..];
            var nameSize = _fontSmall.MeasureString(SafeText(name));
            DrawOutlinedString(_fontSmall, name, new Vector2(pos.X + w / 2f - nameSize.X / 2, pos.Y - 14), Color.White * 0.8f);
            if (lvl >= 2 && maxHp > 0)
            {
                // Level 2+: HP bar
                int hbW = w + 4, hbH = 2;
                int hbX = (int)(pos.X + w / 2f - hbW / 2f);
                int hbY = (int)(pos.Y - 6);
                _spriteBatch.Draw(_pixel, new Rectangle(hbX, hbY, hbW, hbH), Color.DarkRed * 0.7f);
                float ratio = MathHelper.Clamp((float)hp / maxHp, 0f, 1f);
                _spriteBatch.Draw(_pixel, new Rectangle(hbX, hbY, (int)(hbW * ratio), hbH), Color.Red);
            }
            if (lvl >= 3)
            {
                // Level 3: yellow pulsing outline
                float pulse = 0.4f + 0.3f * MathF.Sin((float)_totalTime * 6f);
                var c = Color.Yellow * pulse;
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - 1, w + 2, 1), c);
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 1, (int)pos.Y + h, w + 2, 1), c);
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 1, (int)pos.Y, 1, h), c);
                _spriteBatch.Draw(_pixel, new Rectangle((int)pos.X + w, (int)pos.Y, 1, h), c);
            }
        }
        foreach (var cr in _creatures)
        {
            if (!cr.Alive) continue;
            if (cr is Crawler crl && crl.IsDummy) continue;
            string typeName = GetCreatureTypeName(cr);
            DrawScanOverlay(typeName, cr.Position, cr.Rect.Width, cr.Rect.Height, cr.Hp, cr.MaxHp);
        }

        // Draw splatters
        foreach (var s in _splatters)
        {
            float alpha = MathHelper.Clamp(s.Life / 1.5f, 0, 1);
            _spriteBatch.Draw(_pixel, new Rectangle((int)s.Position.X, (int)s.Position.Y, 2, 1), s.Color * alpha);
        }

        // Draw particles
        foreach (var p in _particles)
        {
            float alpha = p.Life / p.MaxLife;
            _spriteBatch.Draw(_pixel, new Rectangle((int)p.Position.X, (int)p.Position.Y, p.Size, p.Size), p.Color * alpha);
        }

        // Draw arc strikes (ionized zones)
        foreach (var arc in _arcStrikes)
        {
            float alpha = arc.Timer / arc.MaxTimer;
            int segs = arc.Offsets.Length;
            Vector2 dir = arc.End - arc.Start;
            Vector2 perp = Vector2.Normalize(new Vector2(-dir.Y, dir.X));
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs;
                float t1 = (i + 1) / (float)segs;
                float off0 = i == 0 ? 0 : arc.Offsets[i];
                float off1 = i == segs - 1 ? 0 : arc.Offsets[Math.Min(i + 1, segs - 1)];
                Vector2 p0 = Vector2.Lerp(arc.Start, arc.End, t0) + perp * off0;
                Vector2 p1 = Vector2.Lerp(arc.Start, arc.End, t1) + perp * off1;
                // Draw line as series of pixels
                int steps = Math.Max(1, (int)Vector2.Distance(p0, p1) / 2);
                for (int s = 0; s <= steps; s++)
                {
                    float lt = s / (float)steps;
                    int px = (int)MathHelper.Lerp(p0.X, p1.X, lt);
                    int py = (int)MathHelper.Lerp(p0.Y, p1.Y, lt);
                    _spriteBatch.Draw(_pixel, new Rectangle(px, py, 2, 2), Color.White * alpha);
                    _spriteBatch.Draw(_pixel, new Rectangle(px - 1, py - 1, 4, 4), Color.Cyan * (alpha * 0.3f));
                }
            }
        }

        // Draw player
        if (!_isDead)
        {
            bool visible = _spawnInvincibility <= 0f || MathF.Sin(_spawnInvincibility * 20f) > 0;
            if (visible)
                _player.Draw(_spriteBatch, _pixel, _adamSheet);
                // Effect overlays
                if (_player.SpeedBoostTimer > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height), Color.Lime * 0.2f);
                if (_player.FloatTimer > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height), Color.MediumPurple * 0.25f);

                // Draw melee swing arm
                if (_player.MeleeTimer > 0 && _player.CurrentWeapon != WeaponType.None)
                {
                    var pCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
                    float swAngle = _player.MeleeSwingAngle;
                    float range = _player.MeleeRangeOverride;
                    // Draw arm line (3 segments for thickness)
                    for (int seg = 0; seg < (int)(range * 0.8f); seg += 2)
                    {
                        float sx = pCenter.X + MathF.Cos(swAngle) * seg;
                        float sy = pCenter.Y + MathF.Sin(swAngle) * seg;
                        _spriteBatch.Draw(_pixel, new Rectangle((int)sx, (int)sy, 2, 2), Color.White * 0.8f);
                    }
                    // Draw tip flash
                    float tipX = pCenter.X + MathF.Cos(swAngle) * range * 0.7f;
                    float tipY = pCenter.Y + MathF.Sin(swAngle) * range * 0.7f;
                    _spriteBatch.Draw(_pixel, new Rectangle((int)tipX - 2, (int)tipY - 2, 4, 4), Color.Yellow * 0.9f);
                }

                // Draw charge jump indicator
                if (_player.IsChargingJump)
                {
                    float charge = _player.ChargeJumpProgress;
                    int barW = (int)(Player.Width * charge);
                    int barX = (int)_player.Position.X + (Player.Width - barW) / 2;
                    int barY = (int)_player.Position.Y + Player.Height + 3;
                    _spriteBatch.Draw(_pixel, new Rectangle(barX, barY, barW, 2), Color.Lerp(Color.Gray, Color.Cyan, charge));
                }

                // Shield visual when blocking
                if (_player.IsBlocking)
                {
                    int shieldW = 6, shieldH = 20;
                    bool facingRight = _player.FacingDir == 1;
                    int shieldX = facingRight
                        ? (int)_player.Position.X + Player.Width + 1
                        : (int)_player.Position.X - shieldW - 1;
                    int shieldY = (int)_player.Position.Y + (_player.CurrentHeight - shieldH) / 2;
                    _spriteBatch.Draw(_pixel, new Rectangle(shieldX, shieldY, shieldW, shieldH), new Color(139, 90, 43));
                }
        }

        // Draw EVE orbiting companion
        if (_eveOrbActive && !_isDead)
        {
            float orbX = _evePos.X, orbY = _evePos.Y;
            var playerCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            
            // Triangulation scan — reuses shared DrawTriangulationScan (drawn in scan overlay section)
            // The scan overlay section handles this when _eveMode == Scan
            
            // Sparks during wake-up (EVE is damaged/rebooting)
            if (!_wakeUpComplete)
            {
                foreach (var sp in _eveSparkParticles)
                {
                    if (sp.Life <= 0) continue;
                    float a = sp.Life / sp.MaxLife;
                    var sparkColor = sp.IsBlue ? Color.Cyan * (a * 0.9f) : Color.Yellow * (a * 0.8f);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)sp.X, (int)sp.Y, sp.IsBlue ? 2 : 1, sp.IsBlue ? 2 : 1), sparkColor);
                }
            }
            
            // Outer glow (flickers during boot, changes color by state)
            float glowAlpha = 0.4f;
            Color glowColor = Color.CornflowerBlue;
            Color coreColor = Color.Cyan;
            if (!_wakeUpComplete)
            {
                glowAlpha *= 0.3f + 0.7f * (0.5f + 0.5f * MathF.Sin(_totalTime * 2.5f));
            }
            else
            {
                switch (_eveVisualState)
                {
                    case EveVisualState.Scared:
                        // Rapid flicker, warm tint
                        glowColor = new Color(100, 180, 255);
                        coreColor = new Color(150, 220, 255);
                        glowAlpha = 0.3f + 0.3f * MathF.Abs(MathF.Sin(_totalTime * 12f));
                        break;
                    case EveVisualState.Damaged:
                        // Spark flicker, orange tinge
                        glowColor = new Color(200, 160, 100);
                        coreColor = (_totalTime * 8f % 1f < 0.1f) ? Color.Orange : Color.Cyan;
                        glowAlpha = 0.2f + 0.4f * (float)_rng.NextDouble();
                        break;
                    case EveVisualState.LowBattery:
                        // Dim, slow pulse
                        glowColor = new Color(60, 100, 140);
                        coreColor = new Color(80, 160, 200);
                        glowAlpha = 0.15f + 0.15f * MathF.Sin(_totalTime * 1.5f);
                        break;
                    case EveVisualState.Scanning:
                        // Bright, focused
                        glowColor = Color.Cyan;
                        coreColor = Color.White;
                        glowAlpha = 0.5f + 0.2f * MathF.Sin(_totalTime * 4f);
                        break;
                    default:
                        break;
                }
            }
            _spriteBatch.Draw(_pixel, new Rectangle((int)(orbX - 6), (int)(orbY - 6), 12, 12), glowColor * glowAlpha);
            // Core (flickers during boot)
            float coreAlpha = 1f;
            if (!_wakeUpComplete)
                coreAlpha = 0.4f + 0.6f * (0.5f + 0.5f * MathF.Sin(_totalTime * 3.5f));
            else if (_eveVisualState == EveVisualState.LowBattery)
                coreAlpha = 0.5f + 0.3f * MathF.Sin(_totalTime * 1.5f);
            _spriteBatch.Draw(_pixel, new Rectangle((int)(orbX - 4), (int)(orbY - 4), 8, 8), coreColor * coreAlpha);

            // Water droplets on EVE
            foreach (var drop in _eveDroplets)
            {
                float da = MathHelper.Clamp(drop.Life, 0, 1);
                Vector2 dropPos = drop.Dripping
                    ? _evePos + drop.Offset  // world-relative offset for falling drops
                    : new Vector2(orbX + drop.Offset.X, orbY + drop.Offset.Y); // follows EVE
                int ds = Math.Max(1, (int)drop.Size);
                Color dropColor = new Color(100, 160, 220) * (da * 0.8f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)dropPos.X, (int)dropPos.Y, ds, ds), dropColor);
                // Elongate dripping drops
                if (drop.Dripping && drop.Velocity.Y > 30f)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)dropPos.X, (int)dropPos.Y - 1, 1, ds + 1), dropColor * 0.5f);
            }
            // EVE scan pulse rings
            if (_eveScanPulseTimer > 0)
            {
                float t = 1f - _eveScanPulseTimer / 1.5f;
                float radius1 = 8f + t * 40f;
                float alpha = (1f - t) * 0.5f;
                int segments = 24;
                for (int i = 0; i < segments; i++)
                {
                    float a1 = MathF.PI * 2f * i / segments;
                    float a2 = MathF.PI * 2f * (i + 1) / segments;
                    int px1 = (int)(orbX + MathF.Cos(a1) * radius1);
                    int py1 = (int)(orbY + MathF.Sin(a1) * radius1);
                    int px2 = (int)(orbX + MathF.Cos(a2) * radius1);
                    int py2 = (int)(orbY + MathF.Sin(a2) * radius1);
                    DrawLine(px1, py1, px2, py2, Color.Cyan * alpha);
                }
                float radius2 = 4f + t * 25f;
                float alpha2 = (1f - t) * 0.3f;
                for (int i = 0; i < segments; i++)
                {
                    float a1 = MathF.PI * 2f * i / segments;
                    float a2 = MathF.PI * 2f * (i + 1) / segments;
                    int px1 = (int)(orbX + MathF.Cos(a1) * radius2);
                    int py1 = (int)(orbY + MathF.Sin(a1) * radius2);
                    int px2 = (int)(orbX + MathF.Cos(a2) * radius2);
                    int py2 = (int)(orbY + MathF.Sin(a2) * radius2);
                    DrawLine(px1, py1, px2, py2, Color.Cyan * alpha2);
                }
            }
            // EVE speech bubble (word-wrapped)
            if (_eveMessageTimer > 0 && !string.IsNullOrEmpty(_eveMessage))
            {
                float alpha = MathHelper.Clamp(_eveMessageTimer, 0, 1);
                float maxBubbleW = 180f;
                var lines = WrapText(_fontSmall, _eveMessage, maxBubbleW);
                float lineH = _fontSmall.MeasureString("A").Y;
                float totalH = lines.Count * lineH;
                float widest = 0f;
                foreach (var ln in lines)
                {
                    float lw = _fontSmall.MeasureString(ln).X;
                    if (lw > widest) widest = lw;
                }
                float bubbleX = orbX - widest / 2f;
                float bubbleY = orbY - 24 - totalH;
                _spriteBatch.Draw(_pixel, new Rectangle((int)bubbleX - 4, (int)bubbleY - 2, (int)widest + 8, (int)totalH + 4), Color.Black * (0.7f * alpha));
                for (int li = 0; li < lines.Count; li++)
                    DrawOutlinedString(_fontSmall, lines[li], new Vector2(bubbleX, bubbleY + li * lineH), Color.Cyan * alpha);
            }

            // --- EVE Mini-Map Projection (7 area hexagons) ---
            if (_eveProjectingMap && _eveMode == EveMovementMode.MapProject && _eveMapTimer > 0.3f)
            {
                float projAlpha = MathHelper.Clamp((_eveMapTimer - 0.3f) / 0.5f, 0f, 1f);
                float projX = _eveMapGroundPos.X;
                float projBaseY = _eveMapGroundPos.Y - 12f;
                
                // Holographic beam (wider, more visible)
                for (int beam = -1; beam <= 1; beam++)
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(projX + beam), (int)(projBaseY - 80), 1, 68), Color.Cyan * (0.2f * projAlpha));
                
                // 7 area hexagons in honeycomb pattern
                float hexR = 12f;
                float hexSpacing = hexR * 2.2f;
                float mapCX = projX;
                float mapCY = projBaseY - 110f;
                
                var areaHexes = new (string id, float hx, float hy)[]
                {
                    ("wreckage",          0,     0),
                    ("forest",           -1f,    0),
                    ("bone-reef",         1f,    0),
                    ("native-ruins",     -0.5f, -0.866f),
                    ("deep-ruins",        0.5f,  0.866f),
                    ("transformed-lands", -0.5f,  0.866f),
                    ("dragons-sanctum",   0,     1.732f),
                };

                Color MiniAreaCol(string a) => a switch
                {
                    "wreckage" => new Color(100, 150, 180),
                    "forest" => new Color(60, 160, 60),
                    "native-ruins" => new Color(180, 150, 80),
                    "bone-reef" => new Color(150, 80, 150),
                    "deep-ruins" => new Color(60, 100, 160),
                    "transformed-lands" => new Color(120, 60, 160),
                    "dragons-sanctum" => new Color(180, 40, 40),
                    _ => Color.Cyan
                };
                
                // Connections between adjacent hexes
                for (int i = 0; i < areaHexes.Length; i++)
                for (int j = i + 1; j < areaHexes.Length; j++)
                {
                    float ddx = areaHexes[j].hx - areaHexes[i].hx;
                    float ddy = areaHexes[j].hy - areaHexes[i].hy;
                    if (ddx * ddx + ddy * ddy < 1.3f)
                    {
                        float lx1 = mapCX + areaHexes[i].hx * hexSpacing;
                        float ly1 = mapCY + areaHexes[i].hy * hexSpacing;
                        float lx2 = mapCX + areaHexes[j].hx * hexSpacing;
                        float ly2 = mapCY + areaHexes[j].hy * hexSpacing;
                        DrawLine((int)lx1, (int)ly1, (int)lx2, (int)ly2, Color.Cyan * (0.15f * projAlpha));
                    }
                }
                
                // Draw each area hex
                var curRoom = _worldGraph?.GetRoom(_currentRoomId);
                foreach (var (areaId, hx, hy) in areaHexes)
                {
                    float ax = mapCX + hx * hexSpacing;
                    float ay = mapCY + hy * hexSpacing;
                    Color col = MiniAreaCol(areaId);
                    bool isCurrent = curRoom != null && curRoom.AreaId == areaId;
                    bool anyVisited = _worldGraph?.GetAreaRooms(areaId).Any(r => r.Visited) == true;
                    float alpha = anyVisited ? 0.7f : 0.15f;
                    
                    // Hex fill (cross shape approximation)
                    int hr = (int)hexR;
                    int hr2 = (int)(hexR * 0.7f);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr2), (int)(ay - hr), hr2 * 2, hr * 2), col * (alpha * projAlpha * 0.5f));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr), (int)(ay - hr2), hr * 2, hr2 * 2), col * (alpha * projAlpha * 0.5f));
                    // Hex border edges
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr), (int)(ay - hr2), hr * 2, 1), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr), (int)(ay + hr2), hr * 2, 1), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr2), (int)(ay - hr), hr2 * 2, 1), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr2), (int)(ay + hr), hr2 * 2, 1), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr), (int)(ay - hr2), 1, hr2 * 2), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax + hr), (int)(ay - hr2), 1, hr2 * 2), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr2), (int)(ay - hr), 1, hr - hr2), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax + hr2), (int)(ay - hr), 1, hr - hr2), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - hr2), (int)(ay + hr2 + 1), 1, hr - hr2 - 1), col * (alpha * projAlpha));
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(ax + hr2), (int)(ay + hr2 + 1), 1, hr - hr2 - 1), col * (alpha * projAlpha));
                    
                    if (isCurrent)
                    {
                        float blink = 0.5f + 0.5f * MathF.Sin(_totalTime * 6f);
                        _spriteBatch.Draw(_pixel, new Rectangle((int)(ax - 2), (int)(ay - 2), 4, 4), Color.Red * (blink * projAlpha));
                    }
                }
                
                // "[W] Map" prompt if player is close
                float distToEve = Vector2.Distance(new Vector2(_player.Position.X + Player.Width / 2f, _player.Position.Y), _eveMapGroundPos);
                if (distToEve < 80f)
                {
                    string prompt = "[W] Map";
                    var pSize = _fontSmall.MeasureString(SafeText(prompt));
                    DrawOutlinedString(_fontSmall, SafeText(prompt), new Vector2(mapCX - pSize.X / 2f, mapCY + hexSpacing * 2f), Color.Cyan * (0.7f * projAlpha));
                }
                
                // Scanline
                float scanTotal = hexSpacing * 4f;
                float scanY = mapCY - scanTotal * 0.5f + ((_eveMapTimer * 15f) % scanTotal);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(mapCX - hexSpacing * 1.5f), (int)scanY, (int)(hexSpacing * 3f), 1), Color.Cyan * (0.1f * projAlpha));
            }
        }

        // Draw scannable object glows
        foreach (var sc in _scannables)
        {
            if (sc.GlowTimer > 0)
            {
                float glowAlpha = MathHelper.Clamp(sc.GlowTimer, 0, 1) * 0.4f;
                float pulse = 1f + 0.3f * MathF.Sin(sc.GlowTimer * 8f);
                int glowR = (int)(12f * pulse);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(sc.Position.X - glowR), (int)(sc.Position.Y - glowR), glowR * 2, glowR * 2), Color.CornflowerBlue * glowAlpha);
            }
        }

        // Draw bullets
        _bullets.ForEach(b => b.Draw(_spriteBatch, _pixel));

        // Muzzle flash
        if (_muzzleFlashTimer > 0)
        {
            float flashSize = 8f + _muzzleFlashTimer * 80f;
            var flashRect = new Rectangle(
                (int)(_muzzleFlashPos.X - flashSize / 2f),
                (int)(_muzzleFlashPos.Y - flashSize / 2f),
                (int)flashSize, (int)flashSize);
            _spriteBatch.Draw(_pixel, flashRect, Color.White * (_muzzleFlashTimer / 0.06f));
            // Smaller yellow core
            var coreRect = new Rectangle(flashRect.X + 2, flashRect.Y + 2, flashRect.Width - 4, flashRect.Height - 4);
            _spriteBatch.Draw(_pixel, coreRect, Color.Yellow * (_muzzleFlashTimer / 0.06f * 0.8f));
        }

        // === Cipher teleport visual — purple trail line ===
        if (_player.IsCipherTeleporting)
        {
            var tStart = _player.CipherTeleportStart;
            var tEnd = _player.CipherTeleportEnd;
            float tProgress = 1f; // trail visible whole duration
            // Draw a flickering purple line with varying thickness
            var diff = tEnd - tStart;
            float dist = diff.Length();
            if (dist > 1f)
            {
                var dir = diff / dist;
                var perp = new Vector2(-dir.Y, dir.X);
                int segments = Math.Max(4, (int)(dist / 12f));
                for (int i = 0; i < segments; i++)
                {
                    float t0 = (float)i / segments;
                    float t1 = (float)(i + 1) / segments;
                    var p0 = Vector2.Lerp(tStart, tEnd, t0);
                    var p1 = Vector2.Lerp(tStart, tEnd, t1);
                    // Wobble perpendicular for distortion effect
                    float wobble = (float)Math.Sin(t0 * 20f + _totalTime * 30f) * 3f;
                    p0 += perp * wobble;
                    p1 += perp * -wobble;
                    float alpha = 0.6f * (1f - t0 * 0.5f);
                    var col = new Color(180, 80, 200) * alpha;
                    DrawLine((int)p0.X, (int)p0.Y, (int)p1.X, (int)p1.Y, col);
                }
            }
        }
        
        // === Grapple rope + hook rendering ===
        if (_player.IsGrappling || _player.IsGrappleFiring || _player.IsGrappleRetracting || _player.IsGrapplePulling)
        {
            var playerCenter = _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);
            var hookEnd = _player.IsGrappling ? _player.GrappleAnchor
                        : _player.IsGrapplePulling ? _player.GrappleAnchor
                        : _player.GrappleHookPos;
            
            // Draw rope
            var ropeDiff = hookEnd - playerCenter;
            int steps = (int)MathF.Max(MathF.Abs(ropeDiff.X), MathF.Abs(ropeDiff.Y));
            if (steps > 0)
            {
                var ropeColor = _player.IsGrappling ? new Color(140, 140, 150)
                              : _player.IsGrapplePulling ? new Color(200, 120, 80) // orange tint when pulling enemy
                              : new Color(100, 100, 110);
                for (int i = 0; i <= steps; i += 2)
                {
                    float t = i / (float)steps;
                    int rx = (int)MathHelper.Lerp(playerCenter.X, hookEnd.X, t);
                    int ry = (int)MathHelper.Lerp(playerCenter.Y, hookEnd.Y, t);
                    if (!_player.IsGrappling)
                    {
                        float sag = MathF.Sin(t * MathF.PI) * MathF.Min(12f, steps * 0.08f);
                        ry += (int)sag;
                    }
                    _spriteBatch.Draw(_pixel, new Rectangle(rx, ry, 1, 1), ropeColor);
                }
            }
            
            // Hook crosshair
            int hx = (int)hookEnd.X, hy = (int)hookEnd.Y;
            _spriteBatch.Draw(_pixel, new Rectangle(hx - 2, hy, 5, 1), Color.White);
            _spriteBatch.Draw(_pixel, new Rectangle(hx, hy - 2, 1, 5), Color.White);
            
            // Hand indicator
            int handX = (int)playerCenter.X + _player.FacingDir * (Player.Width / 2);
            int handY = (int)playerCenter.Y;
            _spriteBatch.Draw(_pixel, new Rectangle(handX - 1, handY - 1, 3, 3), new Color(80, 80, 90));
        }
        
        // (Crosshair drawn after lighting overlay — see below)

        _spriteBatch.End();

        // --- UI rendering (no camera transform, screen-space) ---

        // Day-night: simple overlay (same as editor) + additive light punch-through
        var ambientColor = GetAmbientOverlay();
        if (ambientColor.A > 2)
        {
            // Dark overlay over entire scene
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), ambientColor);
            _spriteBatch.End();

            // Additive lights punch through the darkness
            _spriteBatch.Begin(blendState: BlendState.Additive);

            if (_lanternActive && _player.Battery > 0)
            {
                if (_currentEnvType == "ionized")
                    _lanternFlicker = 1f + (float)(_rng.NextDouble() * 0.25 - 0.12);
                else
                    _lanternFlicker = 1f + (float)(_rng.NextDouble() * 0.06 - 0.03);

                float radius = 200f * _lanternFlicker;
                var pc = _player.Position + new Vector2(Player.Width / 2f, _player.CurrentHeight / 2f);
                var sp = Vector2.Transform(pc, _camera.TransformMatrix);
                int ls = (int)(radius * 2);
                // Scale light intensity with darkness so it counters the overlay
                float nightStr = ambientColor.A / 255f;
                byte lc = (byte)(nightStr * 220);
                _spriteBatch.Draw(_lightGradient, new Rectangle((int)sp.X - ls / 2, (int)sp.Y - ls / 2, ls, ls), new Color(lc, (int)(lc * 0.9f), (int)(lc * 0.65f)));
            }

            // Tech dash rocket glow
            if (_player.IsDashing && _player.CurrentTier == Player.MoveTier.Tech)
            {
                var pc = _player.Position + new Vector2(Player.Width / 2f, _player.CurrentHeight / 2f);
                var sp = Vector2.Transform(pc, _camera.TransformMatrix);
                int ds = 300;
                float nightStr = ambientColor.A / 255f;
                byte rc = (byte)(nightStr * 255);
                _spriteBatch.Draw(_lightGradient, new Rectangle((int)sp.X - ds / 2, (int)sp.Y - ds / 2, ds, ds), new Color(rc, (int)(rc * 0.7f), (int)(rc * 0.3f)));
            }

            // Arc strike light
            if (_arcStrikes != null)
            {
                foreach (var arc in _arcStrikes)
                {
                    if (arc.Timer > 0)
                    {
                        float arcAlpha = arc.Timer / 0.15f;
                        var arcCenter = (arc.Start + arc.End) / 2f;
                        var sp = Vector2.Transform(arcCenter, _camera.TransformMatrix);
                        int arcSize = 140;
                        _spriteBatch.Draw(_lightGradient, new Rectangle((int)sp.X - arcSize / 2, (int)sp.Y - arcSize / 2, arcSize, arcSize), 
                            new Color((int)(180 * arcAlpha), (int)(180 * arcAlpha), (int)(255 * arcAlpha)));
                    }
                }
            }

            // Metal tile sparks in ionized zones
            if (_currentEnvType == "ionized" && _level?.TileGridInstance != null)
            {
                var tg = _level.TileGridInstance;
                float camL = _camera.Position.X;
                float camT = _camera.Position.Y;
                float camR = camL + _camera.EffectiveViewW;
                float camB = camT + _camera.EffectiveViewH;
                int startCol = Math.Max(0, (int)((camL - tg.OriginX) / tg.TileSize) - 1);
                int endCol = Math.Min(tg.Width - 1, (int)((camR - tg.OriginX) / tg.TileSize) + 1);
                int startRow = Math.Max(0, (int)((camT - tg.OriginY) / tg.TileSize) - 1);
                int endRow = Math.Min(tg.Height - 1, (int)((camB - tg.OriginY) / tg.TileSize) + 1);
                for (int row = startRow; row <= endRow; row++)
                    for (int col = startCol; col <= endCol; col++)
                    {
                        var tt = tg.Tiles[col, row];
                        if ((tt == TileType.MetalFloor || tt == TileType.MetalWall) && _rng.NextDouble() < 0.05)
                        {
                            float wx = tg.OriginX + col * tg.TileSize + tg.TileSize / 2f;
                            float wy = tg.OriginY + row * tg.TileSize + tg.TileSize / 2f;
                            var sp = Vector2.Transform(new Vector2(wx, wy), _camera.TransformMatrix);
                            _spriteBatch.Draw(_lightGradient, new Rectangle((int)sp.X - 30, (int)sp.Y - 30, 60, 60), new Color(140, 140, 255));
                        }
                    }
            }

            _spriteBatch.End();
        }
        // Small lantern glow (always, even during day — subtle warm spot)
        if (_lanternActive && _player.Battery > 0)
        {
            var pc = _player.Position + new Vector2(Player.Width / 2f, _player.CurrentHeight / 2f);
            var sp = Vector2.Transform(pc, _camera.TransformMatrix);
            _spriteBatch.Begin(blendState: BlendState.Additive);
            int gs = 80;
            _spriteBatch.Draw(_lightGradient, new Rectangle((int)sp.X - gs / 2, (int)sp.Y - gs / 2, gs, gs), new Color(60, 55, 20));
            _spriteBatch.End();
        }
        
        // Draw crosshair reticle AFTER lighting so it's always visible
        // Also redraw player on top of lighting so they're not covered by darkness
        if (_wakeUpComplete && _gameState == GameState.Playing)
        {
            _spriteBatch.Begin(transformMatrix: _camera.TransformMatrix);
            // Redraw player above lighting
            if (!_isDead)
            {
                bool visible = _spawnInvincibility <= 0f || MathF.Sin(_spawnInvincibility * 20f) > 0;
                if (visible)
                    _player.Draw(_spriteBatch, _pixel, _adamSheet);
            }
            // Crosshair
            var ms = Mouse.GetState();
            var mScreen = new Vector2(ms.X, ms.Y);
            var mWorld = Vector2.Transform(mScreen, Matrix.Invert(_camera.TransformMatrix));
            int mx = (int)mWorld.X, my = (int)mWorld.Y;
            var rc = Color.White;
            _spriteBatch.Draw(_pixel, new Rectangle(mx, my - 4, 1, 2), rc); // N
            _spriteBatch.Draw(_pixel, new Rectangle(mx, my + 3, 1, 2), rc); // S
            _spriteBatch.Draw(_pixel, new Rectangle(mx + 3, my, 2, 1), rc); // E
            _spriteBatch.Draw(_pixel, new Rectangle(mx - 4, my, 2, 1), rc); // W
            _spriteBatch.End();
        }

        // --- L3 Cipher Scan Glitch Overlay ---
        if (_l3GlitchIntensity > 0.01f)
        {
            _spriteBatch.Begin(blendState: BlendState.Additive);
            float gi = _l3GlitchIntensity;

            // Purple vignette
            for (int edge = 0; edge < 4; edge++)
            {
                Rectangle r = edge switch
                {
                    0 => new Rectangle(0, 0, ViewW, (int)(30 * gi)),                    // top
                    1 => new Rectangle(0, ViewH - (int)(30 * gi), ViewW, (int)(30 * gi)), // bottom
                    2 => new Rectangle(0, 0, (int)(20 * gi), ViewH),                    // left
                    _ => new Rectangle(ViewW - (int)(20 * gi), 0, (int)(20 * gi), ViewH) // right
                };
                _spriteBatch.Draw(_pixel, r, new Color(80, 20, 120) * (gi * 0.4f));
            }

            // Horizontal scan lines (static noise)
            int lineCount = (int)(8 * gi);
            for (int i = 0; i < lineCount; i++)
            {
                int ly = _rng.Next(0, ViewH);
                int lh = _rng.Next(1, 3);
                float la = (float)_rng.NextDouble() * gi * 0.3f;
                _spriteBatch.Draw(_pixel, new Rectangle(0, ly, ViewW, lh), new Color(160, 80, 255) * la);
            }

            // Chromatic offset — draw thin colored strips to simulate RGB shift
            if (gi > 0.3f)
            {
                int offsetX = (int)(3 * gi * (_rng.NextDouble() * 2 - 1));
                int offsetY = (int)(2 * gi * (_rng.NextDouble() * 2 - 1));
                // Red channel shift (thin overlay strip)
                _spriteBatch.Draw(_pixel, new Rectangle(offsetX, ViewH / 2 + offsetY - 1, ViewW, 2), new Color(255, 0, 0) * (gi * 0.15f));
                // Blue channel shift (opposite direction)
                _spriteBatch.Draw(_pixel, new Rectangle(-offsetX, ViewH / 2 - offsetY - 1, ViewW, 2), new Color(0, 0, 255) * (gi * 0.15f));
            }

            // Random glitch blocks at high intensity
            if (gi > 0.6f)
            {
                int blockCount = (int)(4 * (gi - 0.6f) / 0.4f);
                for (int i = 0; i < blockCount; i++)
                {
                    int bx = _rng.Next(0, ViewW);
                    int by = _rng.Next(0, ViewH);
                    int bw = _rng.Next(8, 40);
                    int bh = _rng.Next(2, 6);
                    _spriteBatch.Draw(_pixel, new Rectangle(bx, by, bw, bh), new Color(120, 40, 180) * (gi * 0.2f));
                }
            }

            _spriteBatch.End();

            // L3 scan progress bar (screen-space, center bottom)
            if (_isL3Scanning)
            {
                float progress = 1f - _l3ScanTimer / L3ScanDuration;
                int barW = 60, barH = 4;
                int barX = ViewW / 2 - barW / 2;
                int barY = ViewH / 2 + 40;
                _spriteBatch.Begin();
                _spriteBatch.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), Color.Black * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barW * progress), barH), new Color(180, 100, 255));
                var cipherLabel = "CIPHER LINK";
                var clSize = _fontSmall.MeasureString(cipherLabel);
                _spriteBatch.DrawString(_fontSmall, cipherLabel, new Vector2(ViewW / 2f - clSize.X / 2f, barY - clSize.Y - 2), new Color(200, 120, 255) * gi);
                _spriteBatch.End();
            }
        }

        _spriteBatch.Begin();

        // Weather overlay (lightning flash, ambient darkening)
        DrawWeatherOverlay();

        // Environmental region screen tint
        if (_currentEnvType != "neutral" && _gameState == GameState.Playing)
        {
            float pulse = 1f + 0.15f * MathF.Sin(_totalTime * 2.5f);
            float suitFactor = 1f + (1f - _player.SuitIntegrity / 100f) * 0.5f; // more intense at lower suit
            Color tintColor = _currentEnvType switch
            {
                "cold" => Color.CornflowerBlue * (0.08f * pulse * suitFactor),
                "hot" => Color.OrangeRed * (0.08f * pulse * suitFactor),
                "extreme_cold" => Color.Blue * (0.12f * pulse * suitFactor),
                "extreme_hot" => Color.Red * (0.12f * pulse * suitFactor),
                "ionized" => new Color(100, 60, 180) * (0.06f * (0.8f + 0.2f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f))),
                _ => Color.Transparent
            };
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), tintColor);
        }

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
            DrawOutlinedString(_font, $"HP {_player.Hp}/{_player.MaxHp}", new Vector2(hpBarX + hpBarW + 8, hpBarY - 2), Color.White * 0.7f);

            // Suit Integrity bar (below HP)
            int siBarY = hpBarY + hpBarH + 6;
            float siPct = _player.SuitIntegrity / 100f;
            var siColor = siPct > 0.6f ? new Color(100, 180, 255) : (siPct > 0.3f ? Color.Orange : Color.Red);
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, siBarY, hpBarW, hpBarH), new Color(20, 30, 50) * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, siBarY, (int)(hpBarW * siPct), hpBarH), siColor);
            DrawHollowRect(hpBarX, siBarY, hpBarW, hpBarH, Color.White * 0.3f);
            DrawOutlinedString(_font, $"SUIT {(int)_player.SuitIntegrity}%", new Vector2(hpBarX + hpBarW + 8, siBarY - 2), siColor * 0.9f);

            // Battery bar (below suit)
            int batBarY = siBarY + hpBarH + 4;
            float batPct = _player.Battery / 100f;
            var batColor = batPct > 0.5f ? new Color(220, 200, 60) : (batPct > 0.2f ? Color.Orange : Color.Red);
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, batBarY, hpBarW, hpBarH), new Color(40, 35, 10) * 0.6f);
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, batBarY, (int)(hpBarW * batPct), hpBarH), batColor);
            DrawHollowRect(hpBarX, batBarY, hpBarW, hpBarH, Color.White * 0.3f);
            DrawOutlinedString(_font, $"BAT {(int)_player.Battery}%", new Vector2(hpBarX + hpBarW + 8, batBarY - 2), batColor * 0.9f);
            if (_lanternActive)
                DrawOutlinedString(_fontSmall, "LANTERN", new Vector2(hpBarX + hpBarW + 8 + 80, batBarY), Color.Yellow * (0.8f + 0.2f * MathF.Sin(_totalTime * 4f)));

            // HUD glitch effect at low suit integrity
            if (_player.SuitIntegrity < 20f)
            {
                _hudGlitchTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_hudGlitchTimer <= 0)
                {
                    _hudGlitchTimer = 0.1f + (float)_hudGlitchRng.NextDouble() * 0.3f;
                    // Random pixel displacement on HUD bars
                    float glitchIntensity = 1f - _player.SuitIntegrity / 20f; // 0 at 20%, 1 at 0%
                    if (_hudGlitchRng.NextDouble() < glitchIntensity * 0.4f)
                    {
                        int gx = _hudGlitchRng.Next(-3, 4);
                        int gy = _hudGlitchRng.Next(-2, 3);
                        int gw = _hudGlitchRng.Next(20, 80);
                        int gh = _hudGlitchRng.Next(2, 6);
                        _spriteBatch.Draw(_pixel, new Rectangle(hpBarX + gx, hpBarY + gy, gw, gh),
                            new Color(_hudGlitchRng.Next(50, 255), _hudGlitchRng.Next(50, 255), _hudGlitchRng.Next(50, 255)) * 0.3f);
                    }
                }
            }

            // EVE Status indicator (below battery) — only when EVE is active
            int eveY = batBarY + hpBarH + 8;
            if (_eveOrbActive)
            {
            Color eveStatusColor;
            string eveStatusText;
            switch (_eveStatus)
            {
                case EveStatus.Scanning:
                    eveStatusColor = Color.Yellow;
                    eveStatusText = "SCANNING";
                    break;
                case EveStatus.Overheat:
                    eveStatusColor = Color.Red;
                    eveStatusText = "OVERHEAT";
                    break;
                case EveStatus.Offline:
                    eveStatusColor = Color.DarkGray;
                    eveStatusText = "OFFLINE";
                    break;
                default:
                    eveStatusColor = Color.LimeGreen;
                    eveStatusText = "OK";
                    break;
            }
            // EVE dot + label
            _spriteBatch.Draw(_pixel, new Rectangle(hpBarX, eveY + 2, 8, 8), eveStatusColor);
            DrawOutlinedString(_font, $"EVE: {eveStatusText}", new Vector2(hpBarX + 14, eveY), eveStatusColor * 0.9f);
            // Debug: world time
            DrawOutlinedString(_fontSmall, $"{(int)_worldTime:00}:{(int)((_worldTime % 1) * 60):00} {GetTimeOfDay()}", new Vector2(hpBarX, eveY + 18), Color.Gray * 0.5f);
            } // end _eveOrbActive

            // Weapon hand indicators (bottom-left, always visible)
            {
                string rName = ActiveRight != WeaponType.None ? WeaponStats.Get(ActiveRight).DisplayName : "---";
                string lName = ActiveLeft != WeaponType.None ? WeaponStats.Get(ActiveLeft).DisplayName : "Fists";
                var rColor = ActiveRight != WeaponType.None ? Color.White * 0.8f : Color.Gray * 0.5f;
                var lColor = ActiveLeft != WeaponType.None ? Color.White * 0.8f : Color.Gray * 0.5f;

                // Ammo display for gun in either hand
                string rAmmo = (ActiveRight == WeaponType.Gun) ? $"  [{_sidearmRounds}/{SidearmClipSize}]" : "";
                string lAmmo = (ActiveLeft == WeaponType.Gun) ? $"  [{_sidearmRounds}/{SidearmClipSize}]" : "";

                DrawOutlinedString(_font, $"R: {rName}{rAmmo}", new Vector2(10, ViewH - 36), rColor);
                DrawOutlinedString(_font, $"L: {lName}{lAmmo}", new Vector2(10, ViewH - 18), lColor);
            }

            // Sidearm detail HUD (bottom-right, only when gun equipped)
            {
                int hudRight = ViewW - 10;
                int hudBottom = ViewH - 10;

                if (CurrentRanged == WeaponType.Gun)
                {
                    // Sidearm outline + ammo display
                    int gunW = 48, gunH = 28;
                    int gunX = hudRight - gunW - 60;
                    int gunY = hudBottom - gunH - 4;

                    // Gun silhouette (simple L-shape)
                    var gunColor = _sidearmReloading ? Color.Gray * 0.5f : Color.White * 0.7f;
                    _spriteBatch.Draw(_pixel, new Rectangle(gunX, gunY + 4, 36, 8), gunColor); // barrel
                    _spriteBatch.Draw(_pixel, new Rectangle(gunX + 20, gunY + 4, 8, 20), gunColor); // grip
                    _spriteBatch.Draw(_pixel, new Rectangle(gunX + 4, gunY + 8, 4, 4), gunColor); // trigger guard
                    DrawHollowRect(gunX - 2, gunY, gunW, gunH, Color.White * 0.2f); // outline box

                    // Rounds in clip (individual bullet dots)
                    int dotX = gunX + gunW + 4;
                    int dotY = gunY;
                    for (int i = 0; i < SidearmClipSize; i++)
                    {
                        int row = i / 6;
                        int col = i % 6;
                        var dotColor = i < _sidearmRounds ? new Color(220, 200, 80) : Color.DarkGray * 0.4f;
                        _spriteBatch.Draw(_pixel, new Rectangle(dotX + col * 8, dotY + row * 10, 5, 7), dotColor);
                    }

                    // Reload indicator
                    if (_sidearmReloading)
                    {
                        float reloadPct = 1f - _sidearmReloadTimer / SidearmReloadTime;
                        DrawOutlinedString(_font, "RELOAD", new Vector2(gunX, gunY - 16), Color.Yellow * (0.5f + 0.5f * MathF.Sin(_totalTime * 10f)));
                        // Reload progress bar
                        _spriteBatch.Draw(_pixel, new Rectangle(gunX, gunY + gunH + 2, gunW + 60, 3), Color.DarkGray * 0.5f);
                        _spriteBatch.Draw(_pixel, new Rectangle(gunX, gunY + gunH + 2, (int)((gunW + 60) * reloadPct), 3), Color.Yellow);
                    }
                    else
                    {
                        DrawOutlinedString(_font, $"{_sidearmRounds}/{SidearmClipSize}", new Vector2(gunX + gunW + 4, gunY + 20), Color.White * 0.7f);
                    }
                }
            }

            // Bestiary update notification (bottom center, slides up and fades)
            if (_bestiaryNotifyTimer > 0 && _bestiaryNotifySpecies != null)
            {
                float alpha = MathHelper.Clamp(_bestiaryNotifyTimer, 0f, 1f);
                float slideUp = (1f - MathHelper.Clamp(_bestiaryNotifyTimer / 0.5f, 0f, 1f)) * 0f; // instant appear
                // Get display name if available
                string displayName = _bestiaryNotifySpecies;
                if (CreatureDatabase.Profiles.TryGetValue(_bestiaryNotifySpecies, out var np))
                {
                    if (_bestiary.Entries.TryGetValue(_bestiaryNotifySpecies, out var ne) && ne.SightCount >= 5)
                        displayName = np.DisplayName;
                }
                string notifyText = $"[ Bestiary Updated — {displayName} ]";
                var notifySize = _fontSmall.MeasureString(notifyText);
                float nx = ViewW / 2f - notifySize.X / 2f;
                float ny = ViewH - 28 - slideUp;
                _spriteBatch.Draw(_pixel, new Rectangle((int)nx - 6, (int)ny - 2, (int)notifySize.X + 12, (int)notifySize.Y + 4), Color.Black * (0.5f * alpha));
                DrawOutlinedString(_fontSmall, notifyText, new Vector2(nx, ny), new Color(100, 200, 255) * alpha);
            }

            // Death log (bottom-left, above weapon HUD)
            if (_deathLogEnabled)
            {
                for (int i = 0; i < _deathLog.Count; i++)
                {
                    float alpha = MathHelper.Clamp(_deathLogTimers[i] / 2f, 0f, 1f); // fade in last 2s
                    int y = ViewH - 50 - (_deathLog.Count - i) * 16;
                    DrawOutlinedString(_font, _deathLog[i], new Vector2(10, y), Color.White * 0.6f * alpha);
                }
            }
        }

        // --- EVE Dialogue Log (toggleable, right side) ---
        if (_eveLogVisible && _eveDialogueLog.Count > 0)
        {
            int logX = ViewW - 320;
            int logY = 30;
            int maxLines = 12;
            int lineH = 14;
            // Background
            int bgH = Math.Min(_eveDialogueLog.Count, maxLines) * lineH + 12;
            _spriteBatch.Draw(_pixel, new Rectangle(logX - 6, logY - 4, 316, bgH), Color.Black * 0.5f);
            DrawOutlinedString(_fontSmall, "EVE LOG [L]  W/S scroll", new Vector2(logX, logY - 2), Color.Cyan * 0.7f);
            logY += 14;
            int end = Math.Max(0, _eveDialogueLog.Count - _eveLogScroll);
            int start = Math.Max(0, end - maxLines);
            for (int i = start; i < end; i++)
            {
                var entry = _eveDialogueLog[i];
                float age = _totalTime - entry.Timestamp;
                float alpha = age < 5f ? 1f : MathHelper.Clamp(1f - (age - 5f) / 30f, 0.3f, 1f);
                string text = entry.Text.Length > 45 ? entry.Text.Substring(0, 42) + "..." : entry.Text;
                _spriteBatch.DrawString(_fontSmall, SafeText(text), new Vector2(logX, logY), Color.Cyan * (alpha * 0.8f));
                logY += lineH;
            }
        }

        // --- MOVEMENT TIER DEBUG SELECTOR (editor only) ---
        if (_gameState == GameState.Editing)
        {
            if (_tierSwitchFlash > 0) _tierSwitchFlash -= (float)gameTime.ElapsedGameTime.TotalSeconds * 2f;
            string[] tierNames = { "TECH", "BIO", "CIPHER" };
            string[] tierSymbols = { "[T]", "[B]", "[C]" };
            Color[] tierColors = { new Color(100, 180, 255), new Color(80, 200, 100), new Color(220, 120, 255) };
            int tierY = 80;
            DrawOutlinedString(_font, "MOVE TIER [Y]", new Vector2(10, tierY), Color.White * 0.8f);
            tierY += 22;
            for (int i = 0; i < 3; i++)
            {
                bool active = (int)_player.CurrentTier == i;
                string label = $"{tierSymbols[i]} {tierNames[i]}";
                float flash = active && _tierSwitchFlash > 0 ? _tierSwitchFlash : 0f;
                Color c = active ? Color.Lerp(tierColors[i], Color.White, flash) : Color.Gray * 0.5f;
                if (active) label = "> " + label;
                DrawOutlinedString(_font, label, new Vector2(10, tierY + i * 20), c);
            }
            // Show active constants
            tierY += 68;
            DrawOutlinedString(_fontSmall, $"Spd:{_player.GetTierSpeed():F0} Acc:{_player.GetTierAccel():F0}", new Vector2(10, tierY), Color.White * 0.5f);
            DrawOutlinedString(_fontSmall, $"Air:{_player.GetTierAirMult():F2} Jmp:{_player.GetTierJump():F0}", new Vector2(10, tierY + 16), Color.White * 0.5f);
        }

        // --- EVE Scan HUD ---
        // Scan reveal text (centered, fading)
        if (_scanRevealTimer > 0 && !string.IsNullOrEmpty(_scanRevealText))
        {
            float alpha = MathHelper.Clamp(_scanRevealTimer / 0.5f, 0f, 1f);
            var size = _font.MeasureString(SafeText(_scanRevealText));
            DrawOutlinedString(_font, _scanRevealText, new Vector2(ViewW / 2f - size.X / 2, ViewH / 3f), Color.LimeGreen * alpha);
        }

        // Scan progress counters (top-right)
        if (_scanProgress.Count > 0)
        {
            int sy = 10;
            DrawOutlinedString(_fontSmall, "EVE SCAN", new Vector2(ViewW - 130, sy), Color.LimeGreen * 0.8f);
            sy += 14;
            foreach (var kv in _scanProgress)
            {
                string name = char.ToUpper(kv.Key[0]) + kv.Key[1..];
                DrawOutlinedString(_fontSmall, $"{name} {kv.Value}/3", new Vector2(ViewW - 130, sy), kv.Value >= 3 ? Color.Yellow * 0.9f : Color.White * 0.7f);
                sy += 12;
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

        // Pause indicator
        if (_isPaused)
        {
            // Draw two vertical bars (pause symbol) bottom-right
            int bw = 6, bh = 20, gap = 5;
            int px = ViewW - 30;
            int py = ViewH - 30;
            _spriteBatch.Draw(_pixel, new Rectangle(px, py, bw, bh), Color.White * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(px + bw + gap, py, bw, bh), Color.White * 0.8f);
            _spriteBatch.DrawString(_fontSmall, "PAUSED", new Vector2(ViewW / 2f - 25, ViewH / 2f), Color.White * 0.5f);
        }

        _spriteBatch.End();
        if (_transitionActive && _transitionAlpha > 0f)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * _transitionAlpha);
            _spriteBatch.End();
        }

        // Death fade to black
        if (_isDead)
        {
            float fade = MathHelper.Clamp(_deathFadeTimer / 1.5f, 0, 1); // 1.5s fade
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), Color.Black * fade);
            _spriteBatch.End();
        }

        // Rest warm fade
        if (_restFadeAlpha > 0)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), new Color(40, 30, 15) * _restFadeAlpha);
            _spriteBatch.End();
        }

        // --- Fullscreen Hex Map ---
        if (_fullscreenMapOpen && _eveProjectingMap)
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Dark translucent background
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, ViewH), new Color(0, 10, 20) * 0.92f);

            // Title
            string mapTitle = "EVE CARTOGRAPHIC PROJECTION";
            var titleSize = _font.MeasureString(mapTitle);
            _spriteBatch.DrawString(_font, mapTitle, new Vector2(ViewW / 2f - titleSize.X / 2f, 20), Color.Cyan * 0.8f);

            // Hint text
            string hint = "[M] Close    [WASD] Scroll";
            var hintSize = _fontSmall.MeasureString(SafeText(hint));
            _spriteBatch.DrawString(_fontSmall, SafeText(hint), new Vector2(ViewW / 2f - hintSize.X / 2f, ViewH - 24), Color.Gray * 0.6f);

            float hexR = 20f; // hex radius in screen pixels
            float hexW = hexR * 1.5f;  // horizontal spacing
            float hexH = hexR * 1.3f;  // vertical spacing
            float cx = ViewW / 2f + _fullscreenMapScroll.X;
            float cy = ViewH / 2f + _fullscreenMapScroll.Y;

            // Draw connections (lines)
            foreach (var room in _worldGraph.Rooms)
            {
                if (!room.Visited) continue;
                float x1 = cx + room.MapX * hexW;
                float y1 = cy + room.MapY * hexH;
                foreach (var exit in room.Exits)
                {
                    var neighbor = _worldGraph.GetRoom(exit.TargetRoomId);
                    if (neighbor == null || !neighbor.Visited) continue;
                    float x2 = cx + neighbor.MapX * hexW;
                    float y2 = cy + neighbor.MapY * hexH;
                    DrawLine((int)x1, (int)y1, (int)x2, (int)y2, Color.Cyan * 0.25f);
                }
            }

            // Get area colors
            Color AreaColor(string areaId) => areaId switch
            {
                "wreckage" => new Color(100, 130, 160),
                "forest" => new Color(60, 140, 60),
                "native-ruins" => new Color(160, 130, 80),
                "bone-reef" => new Color(130, 80, 130),
                "deep-ruins" => new Color(60, 90, 140),
                "transformed-lands" => new Color(100, 60, 140),
                "dragons-sanctum" => new Color(160, 40, 40),
                _ => Color.Cyan
            };

            // Draw rooms
            foreach (var room in _worldGraph.Rooms)
            {
                if (!room.Visited && !room.Discovered) continue;
                float rx = cx + room.MapX * hexW;
                float ry = cy + room.MapY * hexH;

                Color col = AreaColor(room.AreaId);
                float alpha = room.Visited ? 0.8f : 0.25f;

                // Hex shape (approximated as filled octagon-ish)
                int hr = (int)(hexR * 0.6f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - hr), (int)(ry - hr), hr * 2, hr * 2), col * alpha);
                // Slightly larger outline
                int or2 = hr + 1;
                _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - or2), (int)(ry - or2), or2 * 2, 1), col * (alpha * 0.5f));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - or2), (int)(ry + or2 - 1), or2 * 2, 1), col * (alpha * 0.5f));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - or2), (int)(ry - or2), 1, or2 * 2), col * (alpha * 0.5f));
                _spriteBatch.Draw(_pixel, new Rectangle((int)(rx + or2 - 1), (int)(ry - or2), 1, or2 * 2), col * (alpha * 0.5f));

                // Current room: blinking red dot
                if (room.Id == _currentRoomId)
                {
                    float blink = 0.5f + 0.5f * MathF.Sin(_totalTime * 6f);
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - 3), (int)(ry - 3), 6, 6), Color.Red * blink);
                }

                // Ship icon on wreckage hub
                if (room.Id == "ship-interior")
                {
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(rx - 2), (int)(ry - hr - 6), 4, 4), Color.White * 0.6f);
                }

                // Room name label
                if (room.Visited)
                {
                    string rname = room.Name;
                    var nameSize = _fontSmall.MeasureString(SafeText(rname));
                    _spriteBatch.DrawString(_fontSmall, SafeText(rname),
                        new Vector2(rx - nameSize.X / 2f, ry + hr + 2), col * (alpha * 0.9f));
                }
            }

            // Area name labels (larger, positioned near area center)
            foreach (var area in _worldGraph.Areas)
            {
                var areaRooms = _worldGraph.GetAreaRooms(area.Id);
                if (areaRooms.Count == 0 || !areaRooms.Any(r => r.Visited)) continue;
                float avgX = areaRooms.Average(r => cx + r.MapX * hexW);
                float avgY = areaRooms.Min(r => cy + r.MapY * hexH) - hexR * 1.2f;
                var areaNameSize = _font.MeasureString(area.Name);
                _spriteBatch.DrawString(_font, area.Name,
                    new Vector2(avgX - areaNameSize.X / 2f, avgY), AreaColor(area.Id) * 0.6f);
            }

            // Holographic scanline effect
            float scanLineY = ((_totalTime * 40f) % ViewH);
            _spriteBatch.Draw(_pixel, new Rectangle(0, (int)scanLineY, ViewW, 1), Color.Cyan * 0.06f);
            _spriteBatch.Draw(_pixel, new Rectangle(0, (int)scanLineY + 2, ViewW, 1), Color.Cyan * 0.03f);

            _spriteBatch.End();
        }

        DrawCRT();
        base.Draw(gameTime);
    }

    private void DrawCRT()
    {
        if (!_crtEnabled || _crtTarget == null) return;

        // Switch back to screen
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        if (_crtShader != null)
        {
            // Full shader path
            _crtShader.Parameters["TextureSize"]?.SetValue(new Vector2(_crtTarget.Width, _crtTarget.Height));
            _crtShader.Parameters["OutputSize"]?.SetValue(new Vector2(ViewW, ViewH));
            _crtShader.Parameters["ScanlineWeight"]?.SetValue(0.25f);
            _crtShader.Parameters["Curvature"]?.SetValue(0.02f);
            _crtShader.Parameters["VignetteStrength"]?.SetValue(0.3f);
            _crtShader.Parameters["BleedAmount"]?.SetValue(0.001f);
            _crtShader.Parameters["Brightness"]?.SetValue(1.15f);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                SamplerState.PointClamp, null, null, _crtShader);
            _spriteBatch.Draw(_crtTarget, new Rectangle(0, 0, ViewW, ViewH), Color.White);
            _spriteBatch.End();
        }
        else
        {
            // Fallback: draw game frame + scanline overlay
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
                SamplerState.PointClamp);
            _spriteBatch.Draw(_crtTarget, new Rectangle(0, 0, ViewW, ViewH), Color.White);
            _spriteBatch.End();

            // Scanline overlay with alpha blending
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearWrap);
            // Tile the scanline texture across the screen
            _spriteBatch.Draw(_scanlineTex,
                new Rectangle(0, 0, ViewW, ViewH),
                new Rectangle(0, 0, 1, ViewH),
                Color.White);
            _spriteBatch.End();

            // Vignette overlay
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            // Dark edges using 4 gradient rects
            int vigSize = ViewW / 6;
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vigSize, ViewH), Color.Black * 0.3f);
            _spriteBatch.Draw(_pixel, new Rectangle(ViewW - vigSize, 0, vigSize, ViewH), Color.Black * 0.3f);
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewW, vigSize), Color.Black * 0.2f);
            _spriteBatch.Draw(_pixel, new Rectangle(0, ViewH - vigSize, ViewW, vigSize), Color.Black * 0.2f);
            _spriteBatch.End();
        }
    }

    private bool IsPlayerNearMetalTile()
    {
        if (_level?.TileGridInstance == null) return false;
        var tg = _level.TileGridInstance;
        int ts = tg.TileSize;
        int cx = (int)((_player.Position.X + Player.Width / 2f - tg.OriginX) / ts);
        int cy = (int)((_player.Position.Y + Player.Height / 2f - tg.OriginY) / ts);
        int radius = 2;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int tx = cx + dx, ty = cy + dy;
                if (tx >= 0 && tx < tg.Width && ty >= 0 && ty < tg.Height)
                {
                    var t = tg.Tiles[tx, ty];
                    if (t == TileType.MetalFloor || t == TileType.MetalWall)
                        return true;
                }
            }
        return false;
    }

    private Vector2? GetNearestMetalTilePos()
    {
        if (_level?.TileGridInstance == null) return null;
        var tg = _level.TileGridInstance;
        int ts = tg.TileSize;
        float px = _player.Position.X + Player.Width / 2f;
        float py = _player.Position.Y + Player.Height / 2f;
        int cx = (int)((px - tg.OriginX) / ts);
        int cy = (int)((py - tg.OriginY) / ts);
        float bestDist = float.MaxValue;
        Vector2? best = null;
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int tx = cx + dx, ty = cy + dy;
                if (tx >= 0 && tx < tg.Width && ty >= 0 && ty < tg.Height)
                {
                    var t = tg.Tiles[tx, ty];
                    if (t == TileType.MetalFloor || t == TileType.MetalWall)
                    {
                        var pos = new Vector2(tg.OriginX + tx * ts + ts / 2f, tg.OriginY + ty * ts + ts / 2f);
                        float d = Vector2.DistanceSquared(new Vector2(px, py), pos);
                        if (d < bestDist) { bestDist = d; best = pos; }
                    }
                }
            }
        return best;
    }
}
