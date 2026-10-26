namespace Marchlands.Simulation.Buildings
{
    /// <summary>Почему найм в здании не идёт.</summary>
    public enum HireWait
    {
        /// <summary>Найм идёт.</summary>
        None,

        /// <summary>Найм выключен.</summary>
        Off,

        /// <summary>По всем включённым родам войск ждёт предельное число людей.</summary>
        Full,

        /// <summary>Не хватает золота.</summary>
        NoGold
    }
}
