namespace Game.Obstacles
{
    /// <summary>
    /// Неизменяемый набор параметров модели заражения. Чистая структура без зависимости
    /// от ScriptableObject — так <see cref="InfectionSimulator"/> можно вызывать из
    /// рантайма, редакторного валидатора и юнит-тестов одинаково.
    /// </summary>
    public readonly struct InfectionSettings
    {
        /// <summary>Множитель радиуса заражения относительно радиуса выстрела.</summary>
        public readonly float RadiusPerShotRadius;

        /// <summary>Доля энергии, сохраняющаяся при переходе волны к соседу (0..1).</summary>
        public readonly float SpreadEfficiency;

        /// <summary>Штраф энергии за метр зазора между поверхностями соседей.</summary>
        public readonly float EnergyCostPerMeter;

        /// <summary>Минимальная энергия, при которой узел ещё считается заражённым.</summary>
        public readonly float MinEnergy;

        public InfectionSettings(float radiusPerShotRadius, float spreadEfficiency,
            float energyCostPerMeter, float minEnergy)
        {
            RadiusPerShotRadius = radiusPerShotRadius;
            SpreadEfficiency = spreadEfficiency;
            EnergyCostPerMeter = energyCostPerMeter;
            MinEnergy = minEnergy;
        }
    }
}
