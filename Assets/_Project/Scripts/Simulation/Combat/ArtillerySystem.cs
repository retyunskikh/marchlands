using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Morale;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Combat
{
    /// <summary>
    /// Пушки (раздел 3.10 docs/design.md). Передок: чтобы ехать, пушку берут на передок, чтобы стрелять — снимают
    /// (limberTime). Снятая с передка заряженная пушка сама наводится (aimTime) на указанную или ближайшую цель
    /// в своём секторе и стреляет, затем перезаряжается. Ядро летит по навесной траектории (над своими), падает
    /// с разбросом у цели и рикошетом катится дальше по прямой, убивая с вероятностью roundShotKillChance каждого
    /// на пути — сильнее всего по глубокому строю и вдоль строя. Картечь бьёт вблизи: canisterBalls пуль,
    /// попадание падает с дистанцией.
    /// </summary>
    internal static class ArtillerySystem
    {
        private const float TimerEpsilon = 1e-4f;

        private static readonly List<Building> Damaged = new List<Building>();

        public static void Update(Match match, float deltaTime, List<ArtilleryShot> shots)
        {
            foreach (Squad squad in match.Squads)
            {
                if (!squad.IsArtillery || squad.Alive <= 0 || squad.IsWithdrawn)
                {
                    continue;
                }

                UpdateLimber(match, squad, deltaTime);
                UpdateFire(match, squad, deltaTime, shots);
            }
        }

        /// <summary>Хочет ли пушка ехать: приказ движения в другую точку (разворот на месте ехать не требует).</summary>
        public static bool WantsToMove(Squad squad, SquadSettings settings)
        {
            return !squad.IsRouting && squad.Order.Type == SquadOrderType.Move
                && math.distance(squad.Order.Target, squad.Position) > settings.arrivalTolerance;
        }

        private static void UpdateLimber(Match match, Squad squad, float deltaTime)
        {
            if (squad.IsInMelee)
            {
                return;
            }

            // Бегущие бросают всё и бегут: пушку считаем на передке (её увозят).
            bool needLimbered = squad.IsRouting || WantsToMove(squad, match.Settings.squads);
            if (squad.IsLimbered == needLimbered)
            {
                squad.LimberRemaining = 0f;
                return;
            }

            if (squad.LimberRemaining <= 0f)
            {
                squad.LimberRemaining = match.Settings.guns.limberTime;
                CancelAiming(squad);
            }

            squad.LimberRemaining -= deltaTime;
            if (squad.LimberRemaining <= TimerEpsilon)
            {
                squad.LimberRemaining = 0f;
                squad.IsLimbered = needLimbered;
            }
        }

        public static void CancelAiming(Squad squad)
        {
            if (squad.FireState == FireState.Aiming)
            {
                squad.FireState = FireState.Loaded;
                squad.FireTimer = 0f;
                squad.AimTargetId = 0;
            }
        }

        private static void UpdateFire(Match match, Squad gun, float deltaTime, List<ArtilleryShot> shots)
        {
            if (gun.FireState == FireState.Reloading)
            {
                gun.FireTimer -= deltaTime;
                if (gun.FireTimer <= TimerEpsilon)
                {
                    gun.FireTimer = 0f;
                    gun.FireState = FireState.Loaded;
                }

                return;
            }

            if (!gun.IsGunReady || gun.IsRouting || gun.IsInMelee)
            {
                CancelAiming(gun);
                return;
            }

            if (gun.FireState == FireState.Loaded)
            {
                if (FindTarget(match, gun, out Squad squad, out Building building))
                {
                    gun.FireState = FireState.Aiming;
                    gun.FireTimer = match.Settings.guns.aimTime;
                    gun.AimTargetId = squad != null ? squad.Id : -building.Id;
                }

                return;
            }

            gun.FireTimer -= deltaTime;
            if (gun.FireTimer > TimerEpsilon)
            {
                return;
            }

            // Цель ушла из сектора или дальности — наводка заново.
            Squad aimedSquad = gun.AimTargetId > 0 ? match.FindSquad(gun.AimTargetId) : null;
            Building aimedBuilding = gun.AimTargetId < 0 ? match.FindBuilding(-gun.AimTargetId) : null;
            if (!(aimedSquad != null && IsValidTarget(match, gun, aimedSquad))
                && !(aimedBuilding != null && IsValidBuilding(match, gun, aimedBuilding)))
            {
                CancelAiming(gun);
                return;
            }

            if (gun.ShotMode == ShotMode.Canister && aimedSquad != null)
            {
                FireCanister(match, gun, aimedSquad, shots);
            }
            else
            {
                FireRoundShot(match, gun, aimedSquad != null ? aimedSquad.Position : aimedBuilding.Position, aimedSquad, shots);
            }

            gun.FireState = FireState.Reloading;
            gun.FireTimer = match.Settings.guns.reloadTime;
            gun.AimTargetId = 0;
        }

        /// <summary>Дуло — середина переднего края расчёта.</summary>
        public static float2 Muzzle(Squad gun, SquadSettings settings)
        {
            return FireGeometry.FrontCenter(gun, settings);
        }

        /// <summary>Цель: указанная игроком, если её можно достать, иначе ближайший враг; ядром — иначе ближайшее здание.</summary>
        private static bool FindTarget(Match match, Squad gun, out Squad target, out Building building)
        {
            building = null;
            target = match.FindSquad(gun.PreferredTargetId);
            if (target != null && IsValidTarget(match, gun, target))
            {
                return true;
            }

            target = null;
            float best = float.MaxValue;
            foreach (Squad candidate in match.Squads)
            {
                if (!IsValidTarget(match, gun, candidate))
                {
                    continue;
                }

                float distance = math.distancesq(candidate.Position, gun.Position);
                if (distance < best || (distance == best && candidate.Id < target.Id))
                {
                    best = distance;
                    target = candidate;
                }
            }

            if (target != null || gun.ShotMode != ShotMode.RoundShot)
            {
                return target != null;
            }

            foreach (Building candidate in match.Buildings)
            {
                if (!IsValidBuilding(match, gun, candidate))
                {
                    continue;
                }

                float distance = math.distancesq(candidate.Position, gun.Position);
                if (distance < best)
                {
                    best = distance;
                    building = candidate;
                }
            }

            return building != null;
        }

        /// <summary>
        /// Враг в секторе пушки и в дальности заряда. В свалку рукопашной не бьют — там свои.
        /// </summary>
        public static bool IsValidTarget(Match match, Squad gun, Squad target)
        {
            if (target == null || target.Side == gun.Side || target.Alive <= 0 || target.IsWithdrawn || target.IsInMelee)
            {
                return false;
            }

            GunSettings guns = match.Settings.guns;
            float2 muzzle = Muzzle(gun, match.Settings.squads);
            if (!InSector(gun, target.Position, guns))
            {
                return false;
            }

            if (gun.ShotMode == ShotMode.Canister)
            {
                return FireGeometry.Distance(gun, target, match.Settings.squads) <= guns.canisterRange;
            }

            float distance = math.distance(muzzle, target.Position);
            return distance >= guns.roundShotMinRange && distance <= guns.roundShotRange;
        }

        private static bool IsValidBuilding(Match match, Squad gun, Building building)
        {
            GunSettings guns = match.Settings.guns;
            if (building == null || building.Owner == gun.Side || building.IsDestroyed || !InSector(gun, building.Position, guns))
            {
                return false;
            }

            float distance = math.distance(Muzzle(gun, match.Settings.squads), building.Position);
            return distance >= guns.roundShotMinRange && distance <= guns.roundShotRange;
        }

        private static bool InSector(Squad gun, float2 point, GunSettings guns)
        {
            float2 offset = point - gun.Position;
            return math.lengthsq(offset) > 0f && math.abs(Angles.Delta(gun.Facing, Angles.Of(offset))) <= guns.fireArcHalfAngle;
        }

        private static void FireRoundShot(Match match, Squad gun, float2 aim, Squad target, List<ArtilleryShot> shots)
        {
            GunSettings guns = match.Settings.guns;
            float2 muzzle = Muzzle(gun, match.Settings.squads);
            float2 toAim = aim - muzzle;
            float distance = math.length(toAim);
            float2 direction = distance > 0f ? toAim / distance : Angles.Direction(gun.Facing);
            float2 side = new float2(direction.y, -direction.x);

            // Навесная траектория: ядро ложится с разбросом вдоль выстрела и вбок, затем катится дальше.
            float along = match.Random.Range(-1f, 1f) * guns.roundShotSpread * distance;
            float lateral = match.Random.Range(-1f, 1f) * guns.roundShotLateralSpread * distance;
            float2 impact = aim + direction * along + side * lateral;
            float2 end = impact + direction * guns.ricochetLength;

            int total = 0;
            foreach (Squad victim in match.Squads)
            {
                if (victim.Side == gun.Side || victim.Alive <= 0 || victim.IsWithdrawn)
                {
                    continue;
                }

                int onPath = MenOnPath(victim, impact, end, match.Settings.squads);
                if (onPath <= 0)
                {
                    continue;
                }

                int aliveBefore = victim.Alive;
                int casualties = VolleyMath.RollCasualties(match.Random, onPath, guns.roundShotKillChance, aliveBefore);
                Hit(match, gun, victim, casualties, aliveBefore, false);
                total += casualties;
            }

            Damaged.Clear();
            foreach (Building building in match.Buildings)
            {
                if (building.Owner != gun.Side && !building.IsDestroyed
                    && math.distance(building.Position, impact) <= guns.roundShotBuildingRadius)
                {
                    Damaged.Add(building);
                }
            }

            // Разрушенное здание убирается из списка — поэтому урон после обхода.
            foreach (Building building in Damaged)
            {
                building.Health -= guns.roundShotBuildingDamage;
                building.SinceDamage = 0f;
                building.IsRepairing = false;
                if (building.Health <= 0f)
                {
                    match.DestroyBuilding(building);
                }
            }

            Damaged.Clear();

            shots.Add(new ArtilleryShot(match.CurrentTick, gun.Id, gun.Side, ShotMode.RoundShot, muzzle, impact, end, total, target != null ? target.Id : 0));
        }

        /// <summary>
        /// Сколько человек на пути ядра от <paramref name="from"/> до <paramref name="to"/> через строй: отрезок
        /// пути внутри прямоугольника отряда — поперёк шеренг по человеку на шеренгу, вдоль шеренги — по человеку на ряд.
        /// </summary>
        public static int MenOnPath(Squad squad, float2 from, float2 to, SquadSettings settings)
        {
            SquadFootprint box = SquadFootprint.Of(squad, settings);
            float2 a = from - box.Center;
            float2 b = to - box.Center;
            float2 localA = new float2(math.dot(a, box.Right), math.dot(a, box.Forward));
            float2 localB = new float2(math.dot(b, box.Right), math.dot(b, box.Forward));
            if (!ClipToBox(ref localA, ref localB, box.HalfExtents))
            {
                return 0;
            }

            float2 delta = math.abs(localB - localA);
            float men = delta.y / math.max(0.01f, settings.rankSpacing) + delta.x / math.max(0.01f, settings.fileSpacing);
            return math.max(1, (int)math.round(men));
        }

        /// <summary>Отсечение отрезка прямоугольником ±half (Лианг — Барски); false — не пересекает.</summary>
        private static bool ClipToBox(ref float2 a, ref float2 b, float2 half)
        {
            float2 d = b - a;
            float t0 = 0f;
            float t1 = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = d[axis];
                float lower = -half[axis] - a[axis];
                float upper = half[axis] - a[axis];
                if (math.abs(p) < 1e-6f)
                {
                    if (lower > 0f || upper < 0f)
                    {
                        return false;
                    }

                    continue;
                }

                float tA = lower / p;
                float tB = upper / p;
                if (tA > tB)
                {
                    float swap = tA;
                    tA = tB;
                    tB = swap;
                }

                t0 = math.max(t0, tA);
                t1 = math.min(t1, tB);
                if (t0 > t1)
                {
                    return false;
                }
            }

            float2 start = a + d * t0;
            b = a + d * t1;
            a = start;
            return true;
        }

        private static void FireCanister(Match match, Squad gun, Squad target, List<ArtilleryShot> shots)
        {
            GunSettings guns = match.Settings.guns;
            float distance = FireGeometry.Distance(gun, target, match.Settings.squads);
            float t = guns.canisterRange > 0f ? math.saturate(distance / guns.canisterRange) : 1f;
            float probability = math.lerp(guns.canisterHitNear, guns.canisterHitFar, t)
                * UnitStats.Of(target, match.Settings).targetHitMultiplier;
            int aliveBefore = target.Alive;
            int casualties = VolleyMath.RollCasualties(match.Random, guns.canisterBalls, math.saturate(probability), aliveBefore);
            Hit(match, gun, target, casualties, aliveBefore, distance <= match.Settings.combat.pointBlankRange);
            float2 muzzle = Muzzle(gun, match.Settings.squads);
            shots.Add(new ArtilleryShot(match.CurrentTick, gun.Id, gun.Side, ShotMode.Canister, muzzle, target.Position, target.Position, casualties, target.Id));
        }

        private static void Hit(Match match, Squad gun, Squad victim, int casualties, int aliveBefore, bool pointBlank)
        {
            if (casualties <= 0)
            {
                return;
            }

            victim.Alive = aliveBefore - casualties;
            bool flank = MoraleMath.IsFlank(victim, gun.Position, match.Settings.morale);
            MoraleMath.ApplyLoss(victim, casualties, aliveBefore, pointBlank, flank, match.Settings);
            MoraleMath.ApplyKills(gun, casualties, match.Settings);
        }
    }
}
