namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Состояние стрельбы: Loaded → Aiming (aimTime) → залп (мгновенно) → Reloading (reloadTime) → Loaded.
    /// Отдельного состояния Firing нет: урон наносится в тот тик, когда кончилось прицеливание.
    /// </summary>
    public enum FireState
    {
        Loaded,
        Aiming,
        Reloading
    }
}
