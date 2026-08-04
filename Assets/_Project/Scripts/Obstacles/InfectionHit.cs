namespace Game.Obstacles
{
    /// <summary>
    /// Результат заражения одного узла: индекс в графе, глубина волны от точки посева
    /// и остаточная энергия на момент заражения.
    /// </summary>
    public readonly struct InfectionHit
    {
        /// <summary>Индекс узла в <see cref="InfectionGraph"/>.</summary>
        public readonly int Index;

        /// <summary>Глубина волны BFS от точки посева (0 — заражён напрямую попаданием).</summary>
        public readonly int Depth;

        /// <summary>Остаточная энергия заражения в момент, когда узел был заражён.</summary>
        public readonly float Energy;

        public InfectionHit(int index, int depth, float energy)
        {
            Index = index;
            Depth = depth;
            Energy = energy;
        }
    }
}
