using Microsoft.Xna.Framework;

namespace ArenaShooter;

public class Camera
{
    public Vector2 Position { get; private set; } // top-left of viewport in world coords
    
    private readonly int _viewWidth;
    private readonly int _viewHeight;
    
    // Smoothing
    private const float LerpSpeedX = 4f;
    private const float LerpSpeedY = 6f;
    
    // Forward bias: offset camera ahead of facing direction
    private const float ForwardBias = 80f;
    private const float BiasLerpSpeed = 3f;
    private float _currentBias;
    
    // Vertical: snap to ground, only follow sustained air
    private const float AirFollowDelay = 0.25f; // seconds before camera follows upward
    private float _airTimer;
    private float _lastGroundY; // last Y when player was grounded
    
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
    
    public void Update(float dt, Vector2 playerPos, int playerWidth, int playerHeight, int facingDir, bool isGrounded, float velocityY)
    {
        float playerCenterX = playerPos.X + playerWidth / 2f;
        float playerCenterY = playerPos.Y + playerHeight / 2f;
        
        // --- Horizontal: lerp with forward bias ---
        float targetBias = facingDir * ForwardBias;
        _currentBias += (targetBias - _currentBias) * BiasLerpSpeed * dt;
        float targetX = playerCenterX + _currentBias - _viewWidth / 2f;
        
        // --- Vertical: ground-snapping with delayed air follow ---
        float targetY;
        if (isGrounded)
        {
            _lastGroundY = playerCenterY;
            _airTimer = 0;
            targetY = _lastGroundY - _viewHeight / 2f;
        }
        else
        {
            _airTimer += dt;
            if (_airTimer > AirFollowDelay || playerCenterY > _lastGroundY)
            {
                // Follow player if in air long enough or falling below ground level
                targetY = playerCenterY - _viewHeight / 2f;
            }
            else
            {
                // Stay at ground level during short hops
                targetY = _lastGroundY - _viewHeight / 2f;
            }
        }
        
        // Lerp toward target
        var current = Position;
        float newX = current.X + (targetX - current.X) * LerpSpeedX * dt;
        float newY = current.Y + (targetY - current.Y) * LerpSpeedY * dt;
        
        // Clamp to world bounds
        newX = MathHelper.Clamp(newX, _worldLeft, _worldRight - _viewWidth);
        newY = MathHelper.Clamp(newY, _worldTop, _worldBottom - _viewHeight);
        
        Position = new Vector2(newX, newY);
    }
    
    public void SnapTo(Vector2 playerPos, int playerWidth, int playerHeight)
    {
        float cx = playerPos.X + playerWidth / 2f - _viewWidth / 2f;
        float cy = playerPos.Y + playerHeight / 2f - _viewHeight / 2f;
        cx = MathHelper.Clamp(cx, _worldLeft, _worldRight - _viewWidth);
        cy = MathHelper.Clamp(cy, _worldTop, _worldBottom - _viewHeight);
        Position = new Vector2(cx, cy);
        _lastGroundY = playerPos.Y + playerHeight / 2f;
    }
    
    public Matrix TransformMatrix => Matrix.CreateTranslation(-Position.X, -Position.Y, 0);
}
