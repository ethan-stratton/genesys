using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ArenaShooter;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private Player _player;

    private List<Bullet> _bullets;
    private List<Enemy> _enemies;

    private float _spawnTimer;
    private const float InitialSpawnInterval = 1.5f;
    private float _spawnInterval;
    private Random _rng;

    private bool _isDead;
    private KeyboardState _prevKb;

    private float _fireCooldown;
    private const float FireRate = 0.25f; // seconds between shots

    private const int FloorY = 550; // ground level
    private const int FloorHeight = 50;

    private static readonly Rectangle[] Platforms = new[]
    {
        new Rectangle(150, 430, 160, 12),
        new Rectangle(480, 430, 160, 12),
        new Rectangle(300, 320, 200, 12),
    };

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

        Restart();
        base.Initialize();
    }

    private void Restart()
    {
        _player = new Player(new Vector2(400 - Player.Width / 2, FloorY - Player.Height));
        _bullets = new List<Bullet>();
        _enemies = new List<Enemy>();
        _spawnTimer = 0f;
        _spawnInterval = InitialSpawnInterval;
        _rng = new Random();

        _isDead = false;
        _fireCooldown = 0f;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private Vector2 PlayerCenter =>
        _player.Position + new Vector2(Player.Width / 2f, Player.Height / 2f);

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape)) Exit();

        if (_isDead)
        {
            if (kb.IsKeyDown(Keys.R) && _prevKb.IsKeyUp(Keys.R))
                Restart();
            _prevKb = kb;
            return;
        }

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _player.Update(dt, kb, FloorY, Platforms);

        // Shoot bullets
        _fireCooldown -= dt;
        var mouse = Mouse.GetState();
        if (mouse.LeftButton == ButtonState.Pressed && _fireCooldown <= 0f)
        {
            _fireCooldown = FireRate;
            var dir = new Vector2(mouse.X, mouse.Y) - PlayerCenter;
            if (dir != Vector2.Zero) dir.Normalize();

            _bullets.Add(new Bullet(PlayerCenter, dir));
        }

        // Update bullets
        _bullets.ForEach(b => b.Update(dt));
        _bullets.RemoveAll(b => b.IsDead);

        // Spawn enemies from left and right edges on the ground
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0;
            _spawnInterval = MathF.Max(0.3f, _spawnInterval - 0.03f);

            var fromLeft = _rng.Next(2) == 0;
            var pos = new Vector2(
                fromLeft ? -Enemy.Size : 800,
                FloorY - Enemy.Size
            );
            _enemies.Add(new Enemy(pos));
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
            // Check player collision
            var pRect = new Rectangle((int)_player.Position.X, (int)_player.Position.Y, Player.Width, Player.Height);
            if (eRect.Intersects(pRect))
            {
                _isDead = true;
                break;
            }
        }

        // Clean up dead bullets and enemies
        _bullets.RemoveAll(b => b.IsDead);
        _enemies.RemoveAll(e => e.IsDead);

        _prevKb = kb;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_isDead ? Color.DarkRed : new Color(20, 20, 20));

        _spriteBatch.Begin();

        // Draw floor
        _spriteBatch.Draw(_pixel, new Rectangle(0, FloorY, 800, FloorHeight), new Color(40, 40, 40));
        _spriteBatch.Draw(_pixel, new Rectangle(0, FloorY, 800, 2), new Color(80, 80, 80));

        // Draw platforms
        foreach (var plat in Platforms)
        {
            _spriteBatch.Draw(_pixel, plat, new Color(50, 50, 50));
            _spriteBatch.Draw(_pixel, new Rectangle(plat.X, plat.Y, plat.Width, 2), new Color(90, 90, 90));
        }

        // Draw player
        if (!_isDead) _player.Draw(_spriteBatch, _pixel);

        // Draw bullets
        _bullets.ForEach(b => b.Draw(_spriteBatch, _pixel));

        // Draw enemies
        _enemies.ForEach(e => e.Draw(_spriteBatch, _pixel));

        // Death overlay
        if (_isDead)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 800, 600), Color.Black * 0.6f);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
