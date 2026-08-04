using UnityEngine;

namespace Game.Utils
{
    /// <summary>
    /// Единственное место в проекте, где радиус шара превращается в массу и обратно.
    /// Плотность считается равной 1, поэтому объём численно равен массе:
    /// mass = volume = (4/3) * PI * r^3.
    /// </summary>
    public static class MassUtils
    {
        private const float ThirdPower = 1f / 3f;

        /// <summary>
        /// Радиус шара по его массе (при плотности 1). Отрицательная масса клампится в 0.
        /// </summary>
        public static float RadiusFromMass(float mass)
        {
            mass = Mathf.Max(0f, mass);
            float volume = mass;
            return Mathf.Pow(3f * volume / (4f * Mathf.PI), ThirdPower);
        }

        /// <summary>
        /// Масса шара по его радиусу (при плотности 1). Отрицательный радиус клампится в 0.
        /// </summary>
        public static float MassFromRadius(float radius)
        {
            radius = Mathf.Max(0f, radius);
            return (4f / 3f) * Mathf.PI * radius * radius * radius;
        }
    }
}
