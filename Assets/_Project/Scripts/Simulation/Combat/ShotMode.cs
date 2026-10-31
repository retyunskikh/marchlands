namespace Marchlands.Simulation.Combat
{
    /// <summary>Чем заряжена пушка.</summary>
    public enum ShotMode
    {
        /// <summary>Ядро: далеко, по навесной траектории, рикошетом сметает ряды — сильнее всего по глубокому строю.</summary>
        RoundShot,

        /// <summary>Картечь: вблизи, веером пуль — страшна для любого строя.</summary>
        Canister
    }
}
