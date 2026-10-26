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

        [Header("Шаг: два кадра — левой и правой (у конницы — галоп)")]
        [SerializeField] private Sprite[] grenadierWalk = new Sprite[2];
        [SerializeField] private Sprite[] jaegerWalk = new Sprite[2];
        [SerializeField] private Sprite[] officerWalk = new Sprite[2];
        [SerializeField] private Sprite[] flagBearerWalk = new Sprite[2];
        [SerializeField] private Sprite[] drummerWalk = new Sprite[2];
        [SerializeField] private Sprite[] cavalryWalk = new Sprite[2];
        [SerializeField] private Sprite[] cavalryOfficerWalk = new Sprite[2];

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

        /// <summary>Кадр шага (0 или 1); null — кадров нет, рисовать стоящую фигурку.</summary>
        public Sprite GetWalk(UnitType type, SoldierRole role, int frame)
        {
            Sprite[] frames;
            if (type == UnitType.Cavalry)
            {
                frames = role == SoldierRole.Officer ? cavalryOfficerWalk : cavalryWalk;
            }
            else
            {
                switch (role)
                {
                    case SoldierRole.Officer:
                        frames = officerWalk;
                        break;
                    case SoldierRole.FlagBearer:
                        frames = flagBearerWalk;
                        break;
                    case SoldierRole.Drummer:
                        frames = drummerWalk;
                        break;
                    default:
                        frames = type == UnitType.Jaegers ? jaegerWalk : grenadierWalk;
                        break;
                }
            }

            return frames != null && frame >= 0 && frame < frames.Length ? frames[frame] : null;
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
