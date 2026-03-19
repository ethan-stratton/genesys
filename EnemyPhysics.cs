using System;
using Microsoft.Xna.Framework;

namespace ArenaShooter;

/// <summary>
/// Shared physics helper for all enemies. Handles gravity, tile collision,
/// platform collision, and surface edge detection.
/// Call ApplyGravityAndCollision() each frame to move an entity with proper physics.
/// </summary>
public static class EnemyPhysics
{
    /// <summary>
    /// Apply gravity and resolve collisions against the tile grid, platforms, solid floors, and main floor.
    /// Modifies position and velocity in-place. Returns true if the entity is on the ground.
    /// </summary>
    public static bool ApplyGravityAndCollision(
        ref Vector2 position, ref Vector2 velocity,
        int entityW, int entityH,
        float gravity, float dt,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float mainFloorY)
    {
        // Apply gravity
        velocity.Y += gravity * dt;

        // Move X
        position.X += velocity.X * dt;
        ResolveHorizontalTileCollision(ref position, ref velocity, entityW, entityH, tileGrid, tileSize);

        // Move Y
        position.Y += velocity.Y * dt;
        bool onGround = ResolveVerticalCollision(ref position, ref velocity, entityW, entityH,
            tileGrid, tileSize, platforms, solidFloors, mainFloorY);

        return onGround;
    }

    /// <summary>
    /// Resolve horizontal collisions against solid tiles. Pushes entity out of walls.
    /// </summary>
    private static void ResolveHorizontalTileCollision(
        ref Vector2 position, ref Vector2 velocity,
        int entityW, int entityH, TileGrid tileGrid, int tileSize)
    {
        if (tileGrid == null) return;

        int left = (int)position.X;
        int right = (int)position.X + entityW - 1;
        int top = (int)position.Y + 2; // slight inset to avoid catching on floor edges
        int bottom = (int)position.Y + entityH - 2;

        // Check tiles at leading edge
        if (velocity.X > 0)
        {
            // Moving right — check right edge
            int tileCol = right / tileSize;
            for (int py = top; py <= bottom; py += tileSize / 2)
            {
                int tileRow = py / tileSize;
                if (tileCol >= 0 && tileCol < tileGrid.Width && tileRow >= 0 && tileRow < tileGrid.Height)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tileCol, tileRow)))
                    {
                        position.X = tileCol * tileSize - entityW;
                        velocity.X = 0;
                        return;
                    }
                }
            }
        }
        else if (velocity.X < 0)
        {
            // Moving left — check left edge
            int tileCol = left / tileSize;
            for (int py = top; py <= bottom; py += tileSize / 2)
            {
                int tileRow = py / tileSize;
                if (tileCol >= 0 && tileCol < tileGrid.Width && tileRow >= 0 && tileRow < tileGrid.Height)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tileCol, tileRow)))
                    {
                        position.X = (tileCol + 1) * tileSize;
                        velocity.X = 0;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resolve vertical collisions (floor landing and ceiling bonk) against tiles, platforms, solid floors, and main floor.
    /// Returns true if entity landed on ground.
    /// </summary>
    private static bool ResolveVerticalCollision(
        ref Vector2 position, ref Vector2 velocity,
        int entityW, int entityH,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float mainFloorY)
    {
        bool onGround = false;

        if (velocity.Y >= 0) // Falling or on ground
        {
            float footY = position.Y + entityH;

            // Check tile grid floors
            if (tileGrid != null)
            {
                int leftCol = (int)position.X / tileSize;
                int rightCol = ((int)position.X + entityW - 1) / tileSize;
                int footRow = (int)footY / tileSize;

                for (int tx = leftCol; tx <= rightCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || footRow < 0 || footRow >= tileGrid.Height) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                    {
                        float surfaceY = footRow * tileSize;
                        if (footY >= surfaceY && position.Y + entityH - velocity.Y * 0.017f <= surfaceY + 8)
                        {
                            position.Y = surfaceY - entityH;
                            velocity.Y = 0;
                            onGround = true;
                        }
                    }
                }
            }

            // Check platforms (land on top, one-way)
            if (!onGround && platforms != null)
            {
                foreach (var p in platforms)
                {
                    if (position.X + entityW > p.X && position.X < p.Right &&
                        footY >= p.Y && footY <= p.Y + velocity.Y * 0.017f + 10)
                    {
                        position.Y = p.Y - entityH;
                        velocity.Y = 0;
                        onGround = true;
                        break;
                    }
                }
            }

            // Check solid floors
            if (!onGround && solidFloors != null)
            {
                foreach (var sf in solidFloors)
                {
                    if (position.X + entityW > sf.X && position.X < sf.Right &&
                        footY >= sf.Y && footY <= sf.Y + velocity.Y * 0.017f + 10)
                    {
                        position.Y = sf.Y - entityH;
                        velocity.Y = 0;
                        onGround = true;
                        break;
                    }
                }
            }

            // Check main floor
            if (!onGround && footY >= mainFloorY)
            {
                position.Y = mainFloorY - entityH;
                velocity.Y = 0;
                onGround = true;
            }
        }
        else // Moving up — check ceilings
        {
            if (tileGrid != null)
            {
                int leftCol = (int)position.X / tileSize;
                int rightCol = ((int)position.X + entityW - 1) / tileSize;
                int headRow = (int)position.Y / tileSize;

                for (int tx = leftCol; tx <= rightCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || headRow < 0 || headRow >= tileGrid.Height) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, headRow)))
                    {
                        position.Y = (headRow + 1) * tileSize;
                        velocity.Y = 0;
                    }
                }
            }
        }

        return onGround;
    }

    /// <summary>
    /// Find the left and right edges of walkable surface at the given foot position.
    /// Checks tile grid, platforms, and solid floors.
    /// </summary>
    public static (float Left, float Right) FindSurfaceEdges(
        float x, float footY, int entityW,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        float boundsLeft, float boundsRight)
    {
        // Check tile grid — scan left and right from current position for continuous solid tiles
        if (tileGrid != null)
        {
            int footRow = (int)footY / tileSize;
            int startCol = (int)x / tileSize;

            // Verify we're actually on a tile surface
            if (footRow >= 0 && footRow < tileGrid.Height && startCol >= 0 && startCol < tileGrid.Width
                && TileProperties.IsSolid(tileGrid.GetTileAt(startCol, footRow)))
            {
                // Scan left
                float left = startCol * tileSize;
                for (int tx = startCol - 1; tx >= 0; tx--)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        left = tx * tileSize;
                    else
                        break;
                }
                // Scan right
                float right = (startCol + 1) * tileSize;
                for (int tx = startCol + 1; tx < tileGrid.Width; tx++)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        right = (tx + 1) * tileSize;
                    else
                        break;
                }
                return (left, right);
            }
        }

        // Check platforms
        if (platforms != null)
        {
            foreach (var p in platforms)
            {
                if (MathF.Abs(footY - p.Y) < 4 && x + entityW > p.X && x < p.Right)
                    return (p.X, p.Right);
            }
        }

        // Check solid floors
        if (solidFloors != null)
        {
            foreach (var sf in solidFloors)
            {
                if (MathF.Abs(footY - sf.Y) < 4 && x + entityW > sf.X && x < sf.Right)
                    return (sf.X, sf.Right);
            }
        }

        // Default: full level bounds
        return (boundsLeft, boundsRight);
    }

    /// <summary>
    /// Snap an entity to the nearest surface below the given position.
    /// Checks tile grid, platforms, solid floors, walls, and main floor.
    /// </summary>
    public static float SnapToSurface(
        float x, float y, int entityW, int entityH,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        Rectangle[] walls, float mainFloorY)
    {
        float bestY = mainFloorY - entityH;

        // Check platforms
        if (platforms != null)
        {
            foreach (var p in platforms)
            {
                float surfaceY = p.Y - entityH;
                if (x + entityW > p.X && x < p.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        // Check solid floors
        if (solidFloors != null)
        {
            foreach (var sf in solidFloors)
            {
                float surfaceY = sf.Y - entityH;
                if (x + entityW > sf.X && x < sf.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        // Check walls
        if (walls != null)
        {
            foreach (var w in walls)
            {
                float surfaceY = w.Y - entityH;
                if (x + entityW > w.X && x < w.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        // Check tile grid
        if (tileGrid != null)
        {
            int startCol = (int)(x / tileSize);
            int endCol = (int)((x + entityW - 1) / tileSize);
            int startRow = (int)(y / tileSize);
            int endRow = (int)(bestY / tileSize) + 1;
            for (int ty = startRow; ty <= endRow && ty < tileGrid.Height; ty++)
            {
                for (int tx = startCol; tx <= endCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, ty)))
                    {
                        float surfaceY = ty * tileSize - entityH;
                        if (surfaceY >= y - 20 && surfaceY < bestY)
                            bestY = surfaceY;
                    }
                }
            }
        }

        return bestY;
    }
}
