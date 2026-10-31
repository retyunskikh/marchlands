namespace Marchlands.Simulation.Combat
{
    /// <summary>Почему залп сейчас невозможен — чтобы подсказать игроку.</summary>
    public enum VolleyBlock
    {
        /// <summary>Залп возможен.</summary>
        None,

        /// <summary>Отряд не стреляет, бежит, перестраивается или не заряжен.</summary>
        NotReady,

        /// <summary>Нет врага в секторе и дальности.</summary>
        NoTarget,

        /// <summary>Враг есть, но он сцепился в рукопашной со своими — стрелять нельзя, заденешь своих.</summary>
        AllyInMelee
    }
}
