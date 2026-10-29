using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class GroundRectTests
    {
        [Test]
        public void TopDown_perspective_footprint_grows_with_height()
        {
            // fov 90 -> tan(45) = 1, so half height == camera height
            var r = GroundRect.TopDownPerspective(100f, 50f, 200f, 90f, 2f);
            Assert.AreEqual(100f - 400f, r.MinX, 0.01f);
            Assert.AreEqual(100f + 400f, r.MaxX, 0.01f);
            Assert.AreEqual(50f - 200f, r.MinZ, 0.01f);
            Assert.AreEqual(50f + 200f, r.MaxZ, 0.01f);

            var high = GroundRect.TopDownPerspective(0f, 0f, 400f, 60f, 1f);
            var low = GroundRect.TopDownPerspective(0f, 0f, 50f, 60f, 1f);
            Assert.Greater(high.MaxX - high.MinX, low.MaxX - low.MinX);
        }

        [Test]
        public void Orthographic_footprint_uses_ortho_size()
        {
            var r = GroundRect.TopDownOrthographic(0f, 0f, 10f, 1.5f);
            Assert.AreEqual(-15f, r.MinX, 0.001f);
            Assert.AreEqual(10f, r.MaxZ, 0.001f);
            Assert.IsTrue(r.Contains(0f, 0f));
            Assert.IsFalse(r.Contains(16f, 0f));
        }
    }
}
