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
    public float ZoomLerpSpeed { get; set; } = 0.5f; // kept for cinematic override
    private SecondOrderDynamics _zoomSpring;
    private bool _zoomInitialized;
    
    // Smoothing — SecondOrderDynamics replaces raw lerp
    // f=3 (responsive), z=0.7 (slight underdamping for organic feel), r=0 (no anticipation)
    private SecondOrderDynamics _smoothX;
    private SecondOrderDynamics _smoothY;
    private bool _springInitialized;
    
    // Dead zone: player can move this far from center before camera follows
    private const float DeadZoneX = 60f;
    private const float DeadZoneY = 30f;
    
    // Forward bias: offset camera ahead of facing direction
    private const float ForwardBias = 80f;
    private SecondOrderDynamics _biasSpring;
    private bool _biasInitialized;
    
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
        // Zoom — second-order dynamics for smooth zoom transitions
        if (!_zoomInitialized) { _zoomSpring = new SecondOrderDynamics(2f, 0.9f, 0f, Zoom); _zoomInitialized = true; }
        Zoom = _zoomSpring.Update(dt, TargetZoom);
        Zoom = MathHelper.Clamp(Zoom, 0.5f, 3f);
        
        float evw = EffectiveViewW;
        float evh = EffectiveViewH;
        
        float playerCenterX = playerPos.X + playerWidth / 2f;
        float playerCenterY = playerPos.Y + playerHeight / 2f;
        
        // --- Forward bias (spring-driven for organic turn feel) ---
        float targetBias = facingDir * ForwardBias;
        if (!_biasInitialized) { _biasSpring = new SecondOrderDynamics(1.5f, 0.6f, -0.5f, targetBias); _biasInitialized = true; }
        float currentBias = _biasSpring.Update(dt, targetBias);
        
        // Camera center = where the camera is currently looking
        float camCenterX = Position.X + evw / 2f;
        float camCenterY = Position.Y + evh / 2f;
        
        // --- Horizontal: dead zone + forward bias ---
        float idealX = playerCenterX + currentBias;
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
        
        // Second-order dynamics toward target (replaces raw lerp)
        if (!_springInitialized)
        {
            _smoothX = new SecondOrderDynamics(3f, 0.7f, 0f, targetX);
            _smoothY = new SecondOrderDynamics(3.5f, 0.8f, 0f, targetY);
            _springInitialized = true;
        }
        float newX = _smoothX.Update(dt, targetX);
        float newY = _smoothY.Update(dt, targetY);
        
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
        if (_springInitialized) { _smoothX.Reset(cx); _smoothY.Reset(cy); }
        if (_zoomInitialized) { _zoomSpring.Reset(Zoom); }
        if (_biasInitialized) { _biasSpring.Reset(0f); }
    }
    
    public Matrix TransformMatrix =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
        Matrix.CreateScale(Zoom, Zoom, 1f);
}
