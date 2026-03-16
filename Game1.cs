using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace ArenaShooter;

public enum GameState { Title, Playing }

public class Game1 : Game
{
    private GameState _gameState = GameState.Title;
    private int _titleCursor;
    private bool _settingsFromTitle;
    private readonly string[] _titleOptions = { "Play", "Settings", "Quit" };

    private GraphicsDeviceManager _graphics;
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

    private const int FloorY = 550; // ground level
    private const int FloorHeight = 50;

    // World bounds (wider than viewport)
    private const int WorldLeft = -600;
    private const int WorldRight = 1400;
    private const int WorldTop = -200;
    private const int WorldBottom = 600;

    private static readonly Rectangle[] Platforms = new[]
    {
        new Rectangle(150, 430, 160, 12),
        new Rectangle(480, 430, 160, 12),
        new Rectangle(300, 320, 200, 12),
        // Extended area platforms
        new Rectangle(-400, 400, 180, 12),
        new Rectangle(-200, 300, 140, 12),
        new Rectangle(800, 430, 160, 12),
        new Rectangle(1000, 350, 180, 12),
        new Rectangle(1200, 430, 140, 12),
    };

    // Spikes: rectangles on the ground that kill the player
    private static readonly Rectangle[] Spikes = new[]
    {
        new Rectangle(550, FloorY - 12, 80, 12),
    };

    // Ropes: X position, top, bottom
    private static readonly float[] RopeXPositions = new float[] { 100f, 350f, 650f, -300f, 900f };
    private static readonly float[] RopeTops = new float[] { -100f, -100f, -100f, -100f, -100f };
    private static readonly float[] RopeBottoms = new float[] { 390f, 320f, 390f, 360f, 390f };

    // Walls
    private static readonly Rectangle[] Walls = new[]
    {
        new Rectangle(-600, 100, 40, 370),
        new Rectangle(1360, 100, 40, 370),
    };
    private static readonly int[] WallClimbSides = new[] { 1, -1 };
    private static readonly Rectangle[] WallLedges = new[]
    {
        new Rectangle(-600, 100, 40, 12),
        new Rectangle(1360, 100, 40, 12),
    };

    private static readonly Rectangle[] AllPlatforms;

    static Game1()
    {
        AllPlatforms = new Rectangle[Platforms.Length + WallLedges.Length];
        Platforms.CopyTo(AllPlatforms, 0);
        WallLedges.CopyTo(AllPlatforms, Platforms.Length);
    }

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

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Player.WorldLeft = WorldLeft;
        Player.WorldRight = WorldRight;
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

    private void Restart()
    {
        _player = new Player(new Vector2(400 - Player.Width / 2, FloorY - Player.Height));
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
        _camera = new Camera(800, 600, WorldLeft, WorldRight, WorldTop, WorldBottom);
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
                    case "Play":
                        _gameState = GameState.Playing;
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

        var wallsToPass = _enableWallClimb ? Walls : null;
        var wallSidesToPass = _enableWallClimb ? WallClimbSides : null;
        var ropesToPass = _enableRopeClimb ? RopeXPositions : null;
        var ropeTopsToPass = _enableRopeClimb ? RopeTops : null;
        var ropeBottomsToPass = _enableRopeClimb ? RopeBottoms : null;

        _player.Update(dt, kb, FloorY, AllPlatforms, ropesToPass, ropeTopsToPass, ropeBottomsToPass, wallsToPass, wallSidesToPass, Walls);

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
                    fromLeft ? WorldLeft - Enemy.Size : WorldRight,
                    FloorY - Enemy.Size
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
            foreach (var spike in Spikes)
            {
                if (pRect.Intersects(spike))
                {
                    _isDead = true;
                    break;
                }
            }
        }

        _prevKb = kb;
        base.Update(gameTime);
    }

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
        _spriteBatch.Draw(_pixel, new Rectangle(WorldLeft, FloorY, WorldRight - WorldLeft, FloorHeight), new Color(40, 40, 40));
        _spriteBatch.Draw(_pixel, new Rectangle(WorldLeft, FloorY, WorldRight - WorldLeft, 2), new Color(80, 80, 80));

        // Draw platforms
        foreach (var plat in Platforms)
        {
            _spriteBatch.Draw(_pixel, plat, new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(plat.X, plat.Y, plat.Width, 2), new Color(90, 90, 90));
        }

        // Draw spikes
        foreach (var spike in Spikes)
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

        // Draw walls
        foreach (var wall in Walls)
        {
            _spriteBatch.Draw(_pixel, wall, _enableWallClimb ? new Color(60, 60, 60) : new Color(45, 45, 45));
        }
        foreach (var ledge in WallLedges)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(ledge.X, ledge.Y, ledge.Width, 2), new Color(100, 100, 100));
        }

        // Draw ropes
        if (_enableRopeClimb)
        {
            for (int i = 0; i < RopeXPositions.Length; i++)
            {
                int rx = (int)RopeXPositions[i];
                int rt = (int)RopeTops[i];
                int rb = (int)RopeBottoms[i];
                _spriteBatch.Draw(_pixel, new Rectangle(rx - 1, rt, 3, rb - rt), new Color(120, 80, 40));
            }
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
