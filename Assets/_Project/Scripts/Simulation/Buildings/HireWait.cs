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

        /// <summary>Нет мест: население упёрлось в предел — нужен дом у штаба.</summary>
        NoHousing,

        /// <summary>Нет коней: все места в конюшнях заняты — расширьте конюшню или постройте ещё одну.</summary>
        NoHorses,

        /// <summary>Не хватает золота.</summary>
        NoGold
    }
}
