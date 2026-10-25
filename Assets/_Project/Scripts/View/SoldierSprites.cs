using System;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Роль солдата в отряде — от неё зависит фигурка.</summary>
    public enum SoldierRole
    {
        Private,
        Officer,
        FlagBearer,
        Drummer
    }

    /// <summary>Фигурки солдат одной стороны, вид сверху, фронтом вверх; цвета мундиров уже нарисованы.</summary>
    [Serializable]
    public sealed class SoldierSprites
    {
        [SerializeField] private Sprite grenadier;
        [SerializeField] private Sprite jaeger;
        [SerializeField] private Sprite officer;
        [SerializeField] private Sprite flagBearer;
        [SerializeField] private Sprite drummer;
        [SerializeField] private Sprite cavalry;
        [SerializeField] private Sprite cavalryOfficer;

        [Header("Рукопашная: тело без оружия и руки с оружием отдельно")]
        [SerializeField] private Sprite grenadierBody;
        [SerializeField] private Sprite grenadierArms;
        [SerializeField] private Sprite jaegerBody;
        [SerializeField] private Sprite jaegerArms;
        [SerializeField] private Sprite officerBody;
        [SerializeField] private Sprite officerArms;
        [SerializeField] private Sprite cavalryBody;
        [SerializeField] private Sprite cavalryOfficerBody;
        [SerializeField] private Sprite cavalryArms;

        public bool IsComplete => grenadier != null && jaeger != null && officer != null && flagBearer != null && drummer != null
            && cavalry != null && cavalryOfficer != null;

        /// <summary>
        /// Фигурка для рукопашной: тело и отдельно руки с оружием, которые делают выпад.
        /// false — у роли нет выпада (знаменосец, барабанщик) или спрайты не назначены.
        /// </summary>
        public bool TryGetMelee(UnitType type, SoldierRole role, out Sprite body, out Sprite arms)
        {
            body = null;
            arms = null;
            if (type == UnitType.Cavalry)
            {
                body = role == SoldierRole.Officer ? cavalryOfficerBody : cavalryBody;
                arms = cavalryArms;
            }
            else if (role == SoldierRole.Officer)
            {
                body = officerBody;
                arms = officerArms;
            }
            else if (role == SoldierRole.Private)
            {
                body = type == UnitType.Jaegers ? jaegerBody : grenadierBody;
                arms = type == UnitType.Jaegers ? jaegerArms : grenadierArms;
            }

            return body != null && arms != null;
        }

        public Sprite Get(UnitType type, SoldierRole role)
        {
            if (type == UnitType.Cavalry)
            {
                return role == SoldierRole.Officer ? cavalryOfficer : cavalry;
            }

            switch (role)
            {
                case SoldierRole.Officer:
                    return officer;
                case SoldierRole.FlagBearer:
                    return flagBearer;
                case SoldierRole.Drummer:
                    return drummer;
                default:
                    return type == UnitType.Jaegers ? jaeger : grenadier;
            }
        }
    }
}
