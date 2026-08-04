using Game.Utils;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Тесты конвертации радиус ↔ масса (плотность = 1).</summary>
    public class MassUtilsTests
    {
        [Test]
        public void RadiusFromMass_Zero_ReturnsZero()
        {
            Assert.AreEqual(0f, MassUtils.RadiusFromMass(0f), 1e-6f);
        }

        [Test]
        public void MassFromRadius_Zero_ReturnsZero()
        {
            Assert.AreEqual(0f, MassUtils.MassFromRadius(0f), 1e-6f);
        }

        [TestCase(0.1f)]
        [TestCase(0.35f)]
        [TestCase(1f)]
        [TestCase(1.2f)]
        [TestCase(5f)]
        [TestCase(50f)]
        public void RadiusToMassToRadius_RoundTrips(float radius)
        {
            float mass = MassUtils.MassFromRadius(radius);
            float roundTripRadius = MassUtils.RadiusFromMass(mass);

            Assert.AreEqual(radius, roundTripRadius, 1e-4f);
        }

        [TestCase(0.1f)]
        [TestCase(1f)]
        [TestCase(3.5f)]
        [TestCase(100f)]
        public void MassToRadiusToMass_RoundTrips(float mass)
        {
            float radius = MassUtils.RadiusFromMass(mass);
            float roundTripMass = MassUtils.MassFromRadius(radius);

            Assert.AreEqual(mass, roundTripMass, 1e-3f);
        }

        [Test]
        public void RadiusFromMass_IsMonotonicallyIncreasing()
        {
            float previousRadius = 0f;
            for (float mass = 0.5f; mass <= 20f; mass += 0.5f)
            {
                float radius = MassUtils.RadiusFromMass(mass);
                Assert.Greater(radius, previousRadius, $"radius should grow for mass={mass}");
                previousRadius = radius;
            }
        }

        [Test]
        public void MassFromRadius_IsMonotonicallyIncreasing()
        {
            float previousMass = 0f;
            for (float radius = 0.1f; radius <= 10f; radius += 0.1f)
            {
                float mass = MassUtils.MassFromRadius(radius);
                Assert.Greater(mass, previousMass, $"mass should grow for radius={radius}");
                previousMass = mass;
            }
        }

        [Test]
        public void RadiusFromMass_NegativeMass_ClampsToZeroResult()
        {
            Assert.AreEqual(0f, MassUtils.RadiusFromMass(-5f), 1e-6f);
        }

        [Test]
        public void MassFromRadius_NegativeRadius_ClampsToZeroResult()
        {
            Assert.AreEqual(0f, MassUtils.MassFromRadius(-5f), 1e-6f);
        }
    }
}
