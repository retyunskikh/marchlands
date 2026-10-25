namespace Marchlands.Simulation.Morale
{
    /// <summary>Отряд либо держится и выполняет любые приказы, либо бежит. Промежуточных состояний нет.</summary>
    public enum MoraleState
    {
        Steady,
        Routing
    }
}
