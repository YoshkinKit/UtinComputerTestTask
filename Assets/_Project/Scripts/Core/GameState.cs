namespace Game.Core
{
    /// <summary>
    /// Состояния основного игрового цикла.
    /// </summary>
    public enum GameState
    {
        /// <summary>Ожидание ввода игрока, шар неподвижен.</summary>
        Idle,

        /// <summary>Тап удержан, масса перетекает из игрока в выстрел.</summary>
        Charging,

        /// <summary>Выстрел выпущен и летит к цели/препятствию.</summary>
        ShotFlying,

        /// <summary>Идёт цепная реакция заражения и взрывов препятствий.</summary>
        Resolving,

        /// <summary>Игрок прыжками продвигается по расчищенному коридору.</summary>
        Advancing,

        /// <summary>Игрок достиг двери — победа.</summary>
        Won,

        /// <summary>Игра завершена поражением.</summary>
        Lost
    }

    /// <summary>
    /// Причина поражения, используется UI для отображения сообщения.
    /// </summary>
    public enum LoseReason
    {
        /// <summary>Поражения не было.</summary>
        None,

        /// <summary>Игрок перекачал всю массу в выстрел и опустился ниже критического размера.</summary>
        Overcharged,

        /// <summary>Оставшейся массы недостаточно, чтобы расчистить путь дальше.</summary>
        NotEnoughMass
    }
}
