using System;

namespace Poi.Core
{
    // a straight (not rotated) rect on the ground (x/z), like the area the camera can see.
    public struct GroundRect
    {
        public float MinX, MinZ, MaxX, MaxZ;

        public GroundRect(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        // Footprint of a camera looking straight down (pitch 90) from `height` above the ground.
        // Perspective: half height = h * tan(fov/2), half width = half height * aspect.
        public static GroundRect TopDownPerspective(float camX, float camZ, float height, float verticalFovDeg, float aspect)
        {
            float halfZ = height * (float)Math.Tan(verticalFovDeg * 0.5 * Math.PI / 180.0);
            float halfX = halfZ * aspect;
            return new GroundRect(camX - halfX, camZ - halfZ, camX + halfX, camZ + halfZ);
        }

        // Orthographic: the visible area does not depend on height, only on orthographic size.
        public static GroundRect TopDownOrthographic(float camX, float camZ, float orthoSize, float aspect)
        {
            float halfX = orthoSize * aspect;
            return new GroundRect(camX - halfX, camZ - orthoSize, camX + halfX, camZ + orthoSize);
        }
    }
}
