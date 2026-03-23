using System;
using Microsoft.Xna.Framework;

namespace Genesis;

public class Camera
{
    public Vector2 Position { get; private set; } // top-left of viewport in world coords
    
    private readonly int _viewWidth;
    private readonly int _viewHeight;
    
    // Zoom
    public float Zoom { get; set; } = 1f;
    public float TargetZoom { get; set; } = 1f;
    public float ZoomLerpSpeed { get; set; } = 0.5f; // slow cinematic default
    
    // Smoothing
    private const float LerpSpeedX = 4f;
    private const float LerpSpeedY = 6f;
    
    // Dead zone: player can move this far from center before camera follows
    private const float DeadZoneX = 60f;
    private const float DeadZoneY = 30f;
    
    // Forward bias: offset camera ahead of facing direction
    private const float ForwardBias = 80f;
    private const float BiasLerpSpeed = 3f;
    private float _currentBias;
    
    // Vertical: snap to ground, only follow sustained air
    private const float AirFollowDelay = 0.25f;
    private float _airTimer;
    private float _lastGroundY;
    
    // World bounds
    private readonly float _worldLeft;
    private readonly float _worldRight;
    private readonly float _worldTop;
    private readonly float _worldBottom;
    
    public Camera(int viewWidth, int viewHeight, float worldLeft, float worldRight, float worldTop, float worldBottom)
    {
        _viewWidth = viewWidth;
        _viewHeight = viewHeight;
        _worldLeft = worldLeft;
        _worldRight = worldRight;
        _worldTop = worldTop;
        _worldBottom = worldBottom;
    }
    
    /// <summary>Effective view dimensions accounting for zoom.</summary>
    public float EffectiveViewW => _viewWidth / Zoom;
    public float EffectiveViewH => _viewHeight / Zoom;
    
    public void Update(float dt, Vector2 playerPos, int playerWidth, int playerHeight, int facingDir, bool isGrounded, float velocityY)
    {
        // Lerp zoom toward target
        if (MathF.Abs(Zoom - TargetZoom) > 0.001f)
            Zoom += (TargetZoom - Zoom) * ZoomLerpSpeed * dt;
        else
            Zoom = TargetZoom;
        Zoom = MathHelper.Clamp(Zoom, 0.5f, 3f);
        
        float evw = EffectiveViewW;
        float evh = EffectiveViewH;
        
        float playerCenterX = playerPos.X + playerWidth / 2f;
        float playerCenterY = playerPos.Y + playerHeight / 2f;
        
        // --- Forward bias (smooth transition when turning) ---
        float targetBias = facingDir * ForwardBias;
        _currentBias += (targetBias - _currentBias) * BiasLerpSpeed * dt;
        
        // Camera center = where the camera is currently looking
        float camCenterX = Position.X + evw / 2f;
        float camCenterY = Position.Y + evh / 2f;
        
        // --- Horizontal: dead zone + forward bias ---
        float idealX = playerCenterX + _currentBias;
        float diffX = idealX - camCenterX;
        float targetX;
        if (MathF.Abs(diffX) > DeadZoneX)
        {
            float sign = MathF.Sign(diffX);
            targetX = idealX - sign * DeadZoneX - evw / 2f;
        }
        else
        {
            targetX = Position.X;
        }
        
        // --- Vertical: dead zone + ground snap ---
        float targetY;
        if (isGrounded)
        {
            _lastGroundY = playerCenterY;
            _airTimer = 0;
        }
        else
        {
            _airTimer += dt;
        }
        
        float verticalRef = (isGrounded || _airTimer <= AirFollowDelay) && playerCenterY <= _lastGroundY
            ? _lastGroundY : playerCenterY;
        
        float diffY = verticalRef - camCenterY;
        if (MathF.Abs(diffY) > DeadZoneY)
        {
            float sign = MathF.Sign(diffY);
            targetY = verticalRef - sign * DeadZoneY - evh / 2f;
        }
        else
        {
            targetY = Position.Y;
        }
        
        // Lerp toward target
        float newX = Position.X + (targetX - Position.X) * LerpSpeedX * dt;
        float newY = Position.Y + (targetY - Position.Y) * LerpSpeedY * dt;
        
        // Clamp to world bounds (accounting for zoom)
        newX = MathHelper.Clamp(newX, _worldLeft, MathF.Max(_worldLeft, _worldRight - evw));
        newY = MathHelper.Clamp(newY, _worldTop, MathF.Max(_worldTop, _worldBottom - evh));
        
        Position = new Vector2(newX, newY);
    }
    
    public void SnapTo(Vector2 playerPos, int playerWidth, int playerHeight, bool unclamped = false)
    {
        float evw = EffectiveViewW;
        float evh = EffectiveViewH;
        float cx = playerPos.X + playerWidth / 2f - evw / 2f;
        float cy = playerPos.Y + playerHeight / 2f - evh / 2f;
        if (!unclamped)
        {
            cx = MathHelper.Clamp(cx, _worldLeft, MathF.Max(_worldLeft, _worldRight - evw));
            cy = MathHelper.Clamp(cy, _worldTop, MathF.Max(_worldTop, _worldBottom - evh));
        }
        Position = new Vector2(cx, cy);
        _lastGroundY = playerPos.Y + playerHeight / 2f;
    }
    
    public Matrix TransformMatrix =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
        Matrix.CreateScale(Zoom, Zoom, 1f);
}
