using System;
using Microsoft.Xna.Framework;

namespace Genesis;

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
        if (velocity.Y > 600f) velocity.Y = 600f; // terminal velocity

        // Move X first
        position.X += velocity.X * dt;
        ResolveHorizontalTileCollision(ref position, ref velocity, entityW, entityH, tileGrid, tileSize);

        // Store pre-move Y for "was above" checks
        float prevFootY = position.Y + entityH;

        // Move Y
        position.Y += velocity.Y * dt;
        bool onGround = ResolveVerticalCollision(ref position, ref velocity, entityW, entityH,
            tileGrid, tileSize, platforms, solidFloors, mainFloorY, prevFootY);

        return onGround;
    }

    // Convert world coordinate to tile index, accounting for grid origin
    private static int WorldToTileX(int worldX, TileGrid tg)
        => worldX >= tg.OriginX ? (worldX - tg.OriginX) / tg.TileSize : (worldX - tg.OriginX) / tg.TileSize - 1;
    private static int WorldToTileY(int worldY, TileGrid tg)
        => worldY >= tg.OriginY ? (worldY - tg.OriginY) / tg.TileSize : (worldY - tg.OriginY) / tg.TileSize - 1;
    // Convert tile index back to world coordinate
    private static int TileToWorldX(int tx, TileGrid tg) => tg.OriginX + tx * tg.TileSize;
    private static int TileToWorldY(int ty, TileGrid tg) => tg.OriginY + ty * tg.TileSize;

    private static void ResolveHorizontalTileCollision(
        ref Vector2 position, ref Vector2 velocity,
        int entityW, int entityH, TileGrid tileGrid, int tileSize)
    {
        if (tileGrid == null) return;

        int left = (int)position.X;
        int right = (int)position.X + entityW - 1;
        int top = (int)position.Y + 2;
        int bottom = (int)position.Y + entityH - 2;

        if (velocity.X > 0)
        {
            int tileCol = WorldToTileX(right, tileGrid);
            for (int py = top; py <= bottom; py += tileSize / 2)
            {
                int tileRow = WorldToTileY(py, tileGrid);
                if (tileCol >= 0 && tileCol < tileGrid.Width && tileRow >= 0 && tileRow < tileGrid.Height)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tileCol, tileRow)))
                    {
                        position.X = TileToWorldX(tileCol, tileGrid) - entityW;
                        velocity.X = 0;
                        return;
                    }
                }
            }
        }
        else if (velocity.X < 0)
        {
            int tileCol = WorldToTileX(left, tileGrid);
            for (int py = top; py <= bottom; py += tileSize / 2)
            {
                int tileRow = WorldToTileY(py, tileGrid);
                if (tileCol >= 0 && tileCol < tileGrid.Width && tileRow >= 0 && tileRow < tileGrid.Height)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tileCol, tileRow)))
                    {
                        position.X = TileToWorldX(tileCol + 1, tileGrid);
                        velocity.X = 0;
                        return;
                    }
                }
            }
        }
    }

    private static bool ResolveVerticalCollision(
        ref Vector2 position, ref Vector2 velocity,
        int entityW, int entityH,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float mainFloorY,
        float prevFootY)
    {
        bool onGround = false;

        if (velocity.Y >= 0)
        {
            float footY = position.Y + entityH;

            // Tile collision (solid tiles block from all directions)
            if (tileGrid != null)
            {
                int leftCol = WorldToTileX((int)position.X + 2, tileGrid);
                int rightCol = WorldToTileX((int)position.X + entityW - 3, tileGrid);
                int footRow = WorldToTileY((int)footY, tileGrid);

                for (int tx = leftCol; tx <= rightCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || footRow < 0 || footRow >= tileGrid.Height) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                    {
                        float surfaceY = TileToWorldY(footRow, tileGrid);
                        // Only land if feet crossed into the tile this frame
                        if (footY >= surfaceY && prevFootY <= surfaceY + 4)
                        {
                            position.Y = surfaceY - entityH;
                            velocity.Y = 0;
                            onGround = true;
                        }
                    }
                }
            }

            // Platform collision (one-way: must have been above before)
            if (!onGround && platforms != null)
            {
                footY = position.Y + entityH;
                foreach (var p in platforms)
                {
                    if (position.X + entityW > p.X + 2 && position.X < p.Right - 2 &&
                        footY >= p.Y && prevFootY <= p.Y + 4)
                    {
                        position.Y = p.Y - entityH;
                        velocity.Y = 0;
                        onGround = true;
                        break;
                    }
                }
            }

            // Solid floors (same as platforms but thicker)
            if (!onGround && solidFloors != null)
            {
                footY = position.Y + entityH;
                foreach (var sf in solidFloors)
                {
                    if (position.X + entityW > sf.X + 2 && position.X < sf.Right - 2 &&
                        footY >= sf.Y && prevFootY <= sf.Y + 4)
                    {
                        position.Y = sf.Y - entityH;
                        velocity.Y = 0;
                        onGround = true;
                        break;
                    }
                }
            }

            // Main floor
            footY = position.Y + entityH;
            if (!onGround && footY >= mainFloorY)
            {
                position.Y = mainFloorY - entityH;
                velocity.Y = 0;
                onGround = true;
            }
        }
        else
        {
            // Moving up — ceiling collision
            if (tileGrid != null)
            {
                int leftCol = WorldToTileX((int)position.X + 2, tileGrid);
                int rightCol = WorldToTileX((int)position.X + entityW - 3, tileGrid);
                int headRow = WorldToTileY((int)position.Y, tileGrid);

                for (int tx = leftCol; tx <= rightCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || headRow < 0 || headRow >= tileGrid.Height) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, headRow)))
                    {
                        position.Y = TileToWorldY(headRow + 1, tileGrid);
                        velocity.Y = 0;
                    }
                }
            }
        }

        return onGround;
    }

    public static (float Left, float Right) FindSurfaceEdges(
        float x, float footY, int entityW,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        float boundsLeft, float boundsRight)
    {
        // Build a unified walkable surface by merging tiles + platforms at foot level
        float left = boundsLeft;
        float right = boundsRight;
        bool foundAny = false;

        // Start with the tile the entity is standing on
        if (tileGrid != null)
        {
            int footRow = WorldToTileY((int)footY, tileGrid);
            int startCol = WorldToTileX((int)(x + entityW / 2f), tileGrid); // center of entity

            if (footRow >= 0 && footRow < tileGrid.Height && startCol >= 0 && startCol < tileGrid.Width
                && TileProperties.IsSolid(tileGrid.GetTileAt(startCol, footRow)))
            {
                // Scan left through contiguous solid tiles
                float tileLeft = TileToWorldX(startCol, tileGrid);
                for (int tx = startCol - 1; tx >= 0; tx--)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        tileLeft = TileToWorldX(tx, tileGrid);
                    else
                        break;
                }
                // Scan right
                float tileRight = TileToWorldX(startCol + 1, tileGrid);
                for (int tx = startCol + 1; tx < tileGrid.Width; tx++)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        tileRight = TileToWorldX(tx + 1, tileGrid);
                    else
                        break;
                }
                left = tileLeft;
                right = tileRight;
                foundAny = true;
            }
        }

        // Extend with any platforms that connect at the same Y level (within 4px)
        if (platforms != null)
        {
            foreach (var p in platforms)
            {
                if (MathF.Abs(footY - p.Y) < 4)
                {
                    if (foundAny)
                    {
                        // Extend left/right if platform overlaps or touches our current surface
                        if (p.Right >= left - 2 && p.X <= right + 2)
                        {
                            left = MathF.Min(left, p.X);
                            right = MathF.Max(right, p.Right);
                        }
                    }
                    else if (x + entityW > p.X && x < p.Right)
                    {
                        left = p.X;
                        right = p.Right;
                        foundAny = true;
                    }
                }
            }
        }

        // Same for solid floors
        if (solidFloors != null)
        {
            foreach (var sf in solidFloors)
            {
                if (MathF.Abs(footY - sf.Y) < 4)
                {
                    if (foundAny)
                    {
                        if (sf.Right >= left - 2 && sf.X <= right + 2)
                        {
                            left = MathF.Min(left, sf.X);
                            right = MathF.Max(right, sf.Right);
                        }
                    }
                    else if (x + entityW > sf.X && x < sf.Right)
                    {
                        left = sf.X;
                        right = sf.Right;
                        foundAny = true;
                    }
                }
            }
        }

        // If platform found, also check if tiles extend from it
        if (foundAny && tileGrid != null)
        {
            int footRow = WorldToTileY((int)footY, tileGrid);
            if (footRow >= 0 && footRow < tileGrid.Height)
            {
                // Check tile at left edge
                int leftCol = WorldToTileX((int)left - 1, tileGrid);
                for (int tx = leftCol; tx >= 0; tx--)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        left = MathF.Min(left, TileToWorldX(tx, tileGrid));
                    else break;
                }
                // Check tile at right edge
                int rightCol = WorldToTileX((int)right, tileGrid);
                for (int tx = rightCol; tx < tileGrid.Width; tx++)
                {
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, footRow)))
                        right = MathF.Max(right, TileToWorldX(tx + 1, tileGrid));
                    else break;
                }
            }
        }

        if (!foundAny)
            return (boundsLeft, boundsRight);

        return (left, right);
    }

    public static float SnapToSurface(
        float x, float y, int entityW, int entityH,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        Rectangle[] walls, float mainFloorY)
    {
        float bestY = mainFloorY - entityH;

        if (platforms != null)
        {
            foreach (var p in platforms)
            {
                float surfaceY = p.Y - entityH;
                if (x + entityW > p.X && x < p.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        if (solidFloors != null)
        {
            foreach (var sf in solidFloors)
            {
                float surfaceY = sf.Y - entityH;
                if (x + entityW > sf.X && x < sf.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        if (walls != null)
        {
            foreach (var w in walls)
            {
                float surfaceY = w.Y - entityH;
                if (x + entityW > w.X && x < w.Right && surfaceY >= y - 20 && surfaceY < bestY)
                    bestY = surfaceY;
            }
        }

        if (tileGrid != null)
        {
            int startCol = WorldToTileX((int)x, tileGrid);
            int endCol = WorldToTileX((int)(x + entityW - 1), tileGrid);
            int startRow = WorldToTileY((int)y, tileGrid);
            int endRow = WorldToTileY((int)bestY, tileGrid) + 1;
            for (int ty = startRow; ty <= endRow && ty < tileGrid.Height; ty++)
            {
                for (int tx = startCol; tx <= endCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || ty < 0) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, ty)))
                    {
                        float surfaceY = TileToWorldY(ty, tileGrid) - entityH;
                        if (surfaceY >= y - 20 && surfaceY < bestY)
                            bestY = surfaceY;
                    }
                }
            }
        }

        return bestY;
    }

    /// <summary>
    /// Check if a position is inside a solid tile. If so, push upward to surface.
    /// </summary>
    public static Vector2 PushOutOfSolid(float x, float y, int entityW, int entityH,
        TileGrid tileGrid, int tileSize)
    {
        if (tileGrid == null) return new Vector2(x, y);

        int col = WorldToTileX((int)(x + entityW / 2f), tileGrid);
        int row = WorldToTileY((int)(y + entityH / 2f), tileGrid);

        if (col < 0 || col >= tileGrid.Width || row < 0 || row >= tileGrid.Height)
            return new Vector2(x, y);

        if (!TileProperties.IsSolid(tileGrid.GetTileAt(col, row)))
            return new Vector2(x, y);

        // Entity center is inside solid. Scan upward for first non-solid row.
        for (int ty = row - 1; ty >= 0; ty--)
        {
            if (!TileProperties.IsSolid(tileGrid.GetTileAt(col, ty)))
            {
                y = TileToWorldY(ty + 1, tileGrid) - entityH;
                return new Vector2(x, y);
            }
        }

        return new Vector2(x, y);
    }
}
