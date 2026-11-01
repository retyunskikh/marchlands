namespace Marchlands.Simulation.Buildings
{
    /// <summary>Почему здание нельзя поставить в выбранную точку.</summary>
    public enum SiteProblem
    {
        None,
        NoHeadquarters,
        AtLimit,
        FarFromHeadquarters,
        TooCloseToBuilding,
        TooCloseToVillage,
        NoGold
    }
}
