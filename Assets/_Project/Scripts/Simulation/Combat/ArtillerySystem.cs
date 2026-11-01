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
            UpdateRoundShots(match, deltaTime);
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
        public static bool WantsToMove(Squad squad, SimulationSettings settings)
        {
            // Пока расчёт обходит пушку (передок), отряд смещается — считаем от места, где пушка стояла: разворот на
            // месте не повод брать передок.
            float2 standing = squad.Position - Angles.Direction(squad.Facing) * squad.LimberShift;
            float tolerance = settings.squads.arrivalTolerance + StandingMargin;
            return !squad.IsRouting && squad.Order.Type == SquadOrderType.Move
                && math.distance(squad.Order.Target, standing) > tolerance;
        }

        // Запас к «стоит на месте» сверх обхода расчёта, u.
        private const float StandingMargin = 0.3f;

        private static void UpdateLimber(Match match, Squad squad, float deltaTime)
        {
            if (squad.IsInMelee)
            {
                return;
            }

            // Бегущие пушку бросают — переставлять её некому.
            if (squad.IsRouting)
            {
                squad.LimberRemaining = 0f;
                squad.LimberShift = 0f;
                return;
            }

            bool needLimbered = WantsToMove(squad, match.Settings);
            float2 forward = Angles.Direction(squad.Facing);
            if (squad.IsLimbered == needLimbered)
            {
                // Передумали посреди перестановки — расчёт возвращается на прежнее место.
                if (squad.LimberShift != 0f)
                {
                    squad.Position -= forward * squad.LimberShift;
                    squad.LimberShift = 0f;
                }

                squad.LimberRemaining = 0f;
                return;
            }

            GunSettings guns = match.Settings.guns;
            if (squad.LimberRemaining <= 0f)
            {
                squad.LimberRemaining = guns.limberTime;
                squad.LimberShift = 0f;
                CancelAiming(squad);
            }

            // Пушка стоит на месте, расчёт её обходит: на передок — вперёд (пушка остаётся позади),
            // с передка — назад (пушка оказывается впереди).
            float total = GetLimberWalk(squad, match.Settings);
            float target = (needLimbered ? 1f : -1f) * total;
            float step = guns.limberTime > 0f ? total * deltaTime / guns.limberTime : total;
            float next = needLimbered ? math.min(target, squad.LimberShift + step) : math.max(target, squad.LimberShift - step);
            squad.Position += forward * (next - squad.LimberShift);
            squad.LimberShift = next;

            squad.LimberRemaining -= deltaTime;
            if (squad.LimberRemaining <= TimerEpsilon)
            {
                float2 standing = squad.Position - forward * squad.LimberShift;
                squad.Position += forward * (target - squad.LimberShift);
                // Приказ «стоять здесь» (разворот на месте) — теперь «здесь» на новом месте расчёта, иначе пушка
                // решила бы, что её надо везти обратно, и снова взяла бы передок.
                if (squad.Order.Type == SquadOrderType.Move
                    && math.distance(squad.Order.Target, standing) <= match.Settings.squads.arrivalTolerance + StandingMargin)
                {
                    squad.Order = SquadOrder.Move(squad.Position, squad.Order.FinalFacing);
                }

                squad.LimberRemaining = 0f;
                squad.LimberShift = 0f;
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
                // Расчёт в рукопашной пушку не заряжает.
                if (!gun.IsInMelee)
                {
                    gun.FireTimer -= deltaTime;
                }

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

            // Пушка бьёт только ядрами (картечи нет).
            FireRoundShot(match, gun, aimedSquad != null ? aimedSquad.Position : aimedBuilding.Position, aimedSquad, shots);

            gun.FireState = FireState.Reloading;
            gun.FireTimer = match.Settings.guns.reloadTime;
            gun.AimTargetId = 0;
        }

        /// <summary>
        /// Сколько проходит расчёт, переставляя пушку: от «пушка впереди фронта» до «пушка позади тыла» — глубина
        /// строя расчёта, вынос пушки вперёд и длина упряжки.
        /// </summary>
        public static float GetLimberWalk(Squad gun, SimulationSettings settings)
        {
            float depth = FormationLayout.GetHalfExtents(gun, settings.squads).y * 2f;
            return depth + settings.guns.gunAhead + settings.guns.towLength;
        }

        /// <summary>Дуло — середина переднего края расчёта.</summary>
        public static float2 Muzzle(Squad gun, SquadSettings settings)
        {
            return FireGeometry.FrontCenter(gun, settings);
        }

        /// <summary>Цель: указанная игроком, если её можно достать, иначе ближайший враг; ядром — иначе ближайшее здание.</summary>
        internal static bool FindTarget(Match match, Squad gun, out Squad target, out Building building)
        {
            building = null;
            target = null;
            if (gun.PreferredTargetId < 0)
            {
                // Указанное здание бьют ядрами; картечью по стенам не стреляют — тогда по ближайшему врагу.
                Building preferred = match.FindBuilding(-gun.PreferredTargetId);
                if (preferred != null && gun.ShotMode == ShotMode.RoundShot && IsValidBuilding(match, gun, preferred))
                {
                    building = preferred;
                    return true;
                }
            }
            else
            {
                target = match.FindSquad(gun.PreferredTargetId);
                if (target != null && IsValidTarget(match, gun, target))
                {
                    return true;
                }
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
            if (target == null || target.Side == gun.Side || target.Alive <= 0 || target.IsWithdrawn || target.IsInMelee
                || !match.IsVisible(target, gun.Side))
            {
                return false;
            }

            GunSettings guns = match.Settings.guns;
            float2 muzzle = Muzzle(gun, match.Settings.squads);
            if (!InSector(gun, target.Position, guns))
            {
                return false;
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

            // Упреждение: по идущему отряду пушка бьёт туда, где он будет к прилёту ядра, но путь его оценивает
            // с ошибкой ± leadError.
            if (target != null)
            {
                float2 velocity = target.LastStep / SimulationTime.TickDuration;
                float flight = FlightTime(math.distance(muzzle, aim), guns);
                float2 lead = velocity * FlightTime(math.distance(muzzle, aim + velocity * flight), guns);
                aim += lead * (1f + match.Random.Range(-1f, 1f) * guns.leadError);
            }

            float2 toAim = aim - muzzle;
            float distance = math.length(toAim);
            float2 direction = distance > 0f ? toAim / distance : Angles.Direction(gun.Facing);
            float2 side = new float2(direction.y, -direction.x);

            // Навесная траектория: ядро ложится с разбросом вдоль выстрела и вбок, затем катится дальше.
            float along = match.Random.Range(-1f, 1f) * guns.roundShotSpread * distance;
            float lateral = match.Random.Range(-1f, 1f) * guns.roundShotLateralSpread * distance;
            float2 impact = aim + direction * along + side * lateral;
            float2 end = impact + direction * guns.ricochetLength;

            var flightShot = new RoundShotFlight
            {
                GunId = gun.Id,
                Side = gun.Side,
                Muzzle = muzzle,
                Impact = impact,
                End = end,
                FlightTime = FlightTime(math.distance(muzzle, impact), guns)
            };
            match.RoundShots.Add(flightShot);
            shots.Add(new ArtilleryShot(match.CurrentTick, gun.Id, gun.Side, ShotMode.RoundShot, muzzle, impact, end, 0,
                target != null ? target.Id : 0, flightShot.FlightTime, guns.ricochetSpeed));
        }

        private static float FlightTime(float distance, GunSettings guns)
        {
            return guns.roundShotFlightSpeed > 0f ? distance / guns.roundShotFlightSpeed : 0f;
        }

        /// <summary>
        /// Ядра в полёте: долетев, ядро бьёт здания у точки падения, затем катится рикошетом и проходит сквозь
        /// отряды на своём пути — каждый получает потери, когда ядро до него докатилось.
        /// </summary>
        private static void UpdateRoundShots(Match match, float deltaTime)
        {
            GunSettings guns = match.Settings.guns;
            List<RoundShotFlight> flying = match.RoundShots;
            for (int i = 0; i < flying.Count; i++)
            {
                RoundShotFlight shot = flying[i];
                shot.Age += deltaTime;
                if (shot.Age + TimerEpsilon < shot.FlightTime)
                {
                    continue;
                }

                if (!shot.Landed)
                {
                    shot.Landed = true;
                    DamageBuildings(match, shot, guns);
                }

                float length = math.distance(shot.Impact, shot.End);
                float rolled = guns.ricochetSpeed > 0f ? math.min(length, (shot.Age - shot.FlightTime) * guns.ricochetSpeed) : length;
                StrikeSquads(match, shot, rolled, guns);
                shot.Rolled = rolled;
            }

            flying.RemoveAll(shot => shot.Landed && shot.Rolled >= math.distance(shot.Impact, shot.End) - TimerEpsilon);
        }

        /// <summary>Ядро докатилось до отряда: потери по всему его пути сквозь строй, удар по духу.</summary>
        private static void StrikeSquads(Match match, RoundShotFlight shot, float rolled, GunSettings guns)
        {
            float length = math.distance(shot.Impact, shot.End);
            float2 direction = length > 0f ? (shot.End - shot.Impact) / length : float2.zero;
            float2 from = shot.Impact + direction * shot.Rolled;
            float2 to = shot.Impact + direction * rolled;
            Squad gun = match.FindSquad(shot.GunId);
            foreach (Squad victim in match.Squads)
            {
                if (victim.Side == shot.Side || victim.Alive <= 0 || victim.IsWithdrawn || shot.HitSquads.Contains(victim.Id))
                {
                    continue;
                }

                // Дошло ли ядро за этот тик до строя (или упало прямо в него).
                if (MenOnPath(victim, from, to, match.Settings.squads) <= 0)
                {
                    continue;
                }

                shot.HitSquads.Add(victim.Id);
                int onPath = MenOnPath(victim, from, shot.End, match.Settings.squads, guns.roundShotMenPerRank);
                if (onPath <= 0)
                {
                    continue;
                }

                // Ядро сквозь строй страшно само по себе — дух падает и сверх убитых.
                if (guns.roundShotMoraleShock > 0f)
                {
                    victim.Morale = math.max(0f, victim.Morale - guns.roundShotMoraleShock);
                }

                int aliveBefore = victim.Alive;
                int casualties = VolleyMath.RollCasualties(match.Random, onPath, guns.roundShotKillChance, aliveBefore);
                Hit(match, gun, shot.Muzzle, victim, casualties, aliveBefore, false);
            }
        }

        private static void DamageBuildings(Match match, RoundShotFlight shot, GunSettings guns)
        {
            Damaged.Clear();
            foreach (Building building in match.Buildings)
            {
                if (building.Owner != shot.Side && !building.IsDestroyed
                    && math.distance(building.Position, shot.Impact) <= guns.roundShotBuildingRadius)
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
        }

        /// <summary>
        /// Сколько человек на пути ядра от <paramref name="from"/> до <paramref name="to"/> через строй: отрезок
        /// пути внутри прямоугольника отряда — поперёк шеренг по человеку на шеренгу, вдоль шеренги — по человеку на ряд.
        /// </summary>
        /// <param name="menPerRank">Сколько человек в шеренге валит ядро, идущее поперёк шеренг (≤ 0 — один).</param>
        public static int MenOnPath(Squad squad, float2 from, float2 to, SquadSettings settings, float menPerRank = 1f)
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
            float perRank = menPerRank > 0f ? menPerRank : 1f;
            float men = delta.y / math.max(0.01f, settings.rankSpacing) * perRank + delta.x / math.max(0.01f, settings.fileSpacing);
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
            Hit(match, gun, gun.Position, victim, casualties, aliveBefore, pointBlank);
        }

        /// <summary>Потери от пушки; <paramref name="gun"/> может уже не быть (ядро долетело после гибели расчёта).</summary>
        private static void Hit(Match match, Squad gun, float2 from, Squad victim, int casualties, int aliveBefore, bool pointBlank)
        {
            if (casualties <= 0)
            {
                return;
            }

            victim.Alive = aliveBefore - casualties;
            bool flank = MoraleMath.IsFlank(victim, from, match.Settings.morale);
            MoraleMath.ApplyLoss(victim, casualties, aliveBefore, pointBlank, flank, match.Settings);
            if (gun != null)
            {
                MoraleMath.ApplyKills(gun, casualties, match.Settings);
            }
        }
    }
}
