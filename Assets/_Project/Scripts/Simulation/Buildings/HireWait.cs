namespace Marchlands.Simulation.Buildings
{
    /// <summary>Почему найм в здании не идёт.</summary>
    public enum HireWait
    {
        /// <summary>Найм идёт.</summary>
        None,

        /// <summary>Найм выключен.</summary>
        Off,

        /// <summary>Людей уже хватает на отряд цели найма.</summary>
        SquadReady,

        /// <summary>У зданий ждёт предельное число людей.</summary>
        Full,

        /// <summary>Не хватает золота.</summary>
        NoGold
    }
}
