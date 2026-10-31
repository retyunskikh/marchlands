using System.Collections.Generic;
using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Обратная связь залпа: пороховой дым вдоль фронта стрелка и звук. Павших показывает отряд (<see cref="CasualtiesView"/>).
    /// Случайность здесь чисто визуальная и на симуляцию не влияет, поэтому идёт через UnityEngine.Random.
    /// </summary>
    public sealed class VolleyEffectsView : MonoBehaviour
    {
        [SerializeField] private SquadViewRegistry squadViews;

        [Header("Дым")]
        [SerializeField] private ParticleSystem smoke;
        [Tooltip("За сколько секунд облако рассеивается (smokeDuration из раздела 6).")]
        [SerializeField] private float smokeDuration = 6f;
        [Tooltip("Сколько клубов дыма на одного стрелка.")]
        [SerializeField] private float puffsPerShooter = 0.6f;
        [SerializeField] private Vector2 puffSize = new Vector2(1.1f, 1.8f);
        [Tooltip("Скорость, с которой дым выносит вперёд от фронта, u/s.")]
        [SerializeField] private Vector2 puffSpeed = new Vector2(0.15f, 0.5f);
        [SerializeField] private Color smokeColor = new Color(0.9f, 0.9f, 0.88f, 0.75f);
        [Tooltip("На каком расстоянии от центра здания в сторону цели стреляют часовые, u.")]
        [SerializeField] private float sentryFireOffset = 2.5f;
        [Tooltip("Сколько клубов дыма даёт залп часовых, если их фигурок не видно.")]
        [SerializeField] private int sentryPuffs = 3;
        [Tooltip("Насколько впереди стрелка-одиночки (часовой, рекрут) встаёт клуб дыма, u.")]
        [SerializeField] private float muzzleOffset = 0.45f;
        [Tooltip("Размер клуба дыма одиночного выстрела относительно залпового.")]
        [SerializeField] private float singlePuffScale = 0.75f;

        [Header("Звук")]
        [SerializeField] private AudioClip volleyClip;
        [SerializeField] private int audioVoices = 4;
        [SerializeField] private Vector2 volumeRange = new Vector2(0.35f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.08f);
        [Tooltip("Насколько сильно звук смещается в стерео по положению на экране.")]
        [SerializeField] private float stereoWidth = 0.6f;

        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly List<float2> muzzles = new List<float2>();
        private BuildingViewRegistry buildingViews;

        private Match match;
        private int nextVoice;

        /// <param name="buildingViews">Откуда брать стрелков толпы рекрутов и часовых; null — дым по фронту и у стены.</param>
        public void Initialize(Match match, BuildingViewRegistry buildingViews = null)
        {
            this.match = match;
            this.buildingViews = buildingViews;
            // Система без собственной эмиссии: клубы выпускаются только через Emit при залпе.
            smoke.Play();
            for (int i = 0; i < audioVoices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices.Add(source);
            }
        }

        /// <summary>Показывает залпы, произошедшие в последнем тике.</summary>
        public void Play(IReadOnlyList<VolleyEvent> volleys)
        {
            foreach (VolleyEvent volley in volleys)
            {
                Squad shooter = match.FindSquad(volley.ShooterId);
                if (shooter == null)
                {
                    continue;
                }

                // Выстрел вразнобой из толпы: клуб дыма у того, кто стрелял, а не по всей толпе.
                if (volley.ShooterIndex >= 0 && buildingViews != null && shooter.IsCamp
                    && buildingViews.TryGetCampShooter(shooter, volley.ShooterIndex, out float2 at))
                {
                    EmitSingleShot(at, volley.TargetPosition);
                    PlaySound(at, volley.Shooters);
                    continue;
                }

                EmitSmoke(shooter);
                PlaySound(shooter.Position, volley.Shooters);
            }
        }

        /// <summary>Показывает залпы часовых зданий за последний тик: клубы дыма у стены, обращённой к цели, и выстрел.</summary>
        public void PlaySentries(IReadOnlyList<SentryVolley> volleys)
        {
            foreach (SentryVolley volley in volleys)
            {
                // Видны часовые — у каждого свой клуб дыма перед ружьём.
                muzzles.Clear();
                buildingViews?.GetSentryMuzzles(volley.BuildingId, muzzles);
                if (muzzles.Count > 0)
                {
                    foreach (float2 muzzle in muzzles)
                    {
                        EmitSingleShot(muzzle, volley.TargetPosition);
                    }

                    PlaySound(volley.BuildingPosition, volley.Shooters);
                    continue;
                }

                float2 forward = math.normalizesafe(volley.TargetPosition - volley.BuildingPosition, new float2(1f, 0f));
                float2 right = new float2(forward.y, -forward.x);
                float2 origin = volley.BuildingPosition + forward * sentryFireOffset;
                for (int i = 0; i < sentryPuffs; i++)
                {
                    float2 position = origin + right * Random.Range(-0.8f, 0.8f) + forward * Random.Range(0.1f, 0.4f);
                    EmitPuff(position, forward * Random.Range(puffSpeed.x, puffSpeed.y));
                }

                PlaySound(volley.BuildingPosition, volley.Shooters);
            }
        }

        private void EmitSmoke(Squad shooter)
        {
            float2 front = FireGeometry.FrontCenter(shooter, match.Settings.squads);
            float halfWidth = FireGeometry.FrontHalfWidth(shooter, match.Settings.squads);
            float2 forward = Angles.Direction(shooter.Facing);
            float2 right = Angles.Right(shooter.Facing);
            int puffs = math.max(3, (int)math.ceil(VolleyMath.Shooters(shooter.Formation, shooter.Alive, match.Settings.squads) * puffsPerShooter));

            for (int i = 0; i < puffs; i++)
            {
                float along = math.lerp(-halfWidth, halfWidth, (i + Random.value) / puffs);
                float2 position = front + right * along + forward * Random.Range(0.1f, 0.6f);
                float2 velocity = forward * Random.Range(puffSpeed.x, puffSpeed.y) + right * Random.Range(-0.1f, 0.1f);
                EmitPuff(position, velocity);
            }
        }

        /// <summary>Один выстрел: клуб дыма перед стрелком в сторону цели.</summary>
        private void EmitSingleShot(float2 shooter, float2 target)
        {
            float2 forward = math.normalizesafe(target - shooter, new float2(1f, 0f));
            float2 position = shooter + forward * (muzzleOffset + Random.Range(0f, 0.15f));
            EmitPuff(position, forward * Random.Range(puffSpeed.x, puffSpeed.y), singlePuffScale);
        }

        private void EmitPuff(float2 position, float2 velocity, float scale = 1f)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, 0f),
                velocity = new Vector3(velocity.x, velocity.y, 0f),
                startSize = Random.Range(puffSize.x, puffSize.y) * scale,
                startLifetime = smokeDuration * Random.Range(0.8f, 1f),
                startColor = smokeColor,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false
            };
            smoke.Emit(emit, 1);
        }

        private void PlaySound(float2 at, int shooters)
        {
            if (volleyClip == null || voices.Count == 0)
            {
                return;
            }

            AudioSource source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;

            Camera view = Camera.main;
            float pan = 0f;
            if (view != null)
            {
                Vector3 viewport = view.WorldToViewportPoint(new Vector3(at.x, at.y, 0f));
                pan = math.clamp((viewport.x - 0.5f) * 2f, -1f, 1f) * stereoWidth;
            }

            float strength = math.saturate((float)shooters / match.Settings.grenadiers.squadSize);
            source.clip = volleyClip;
            source.volume = math.lerp(volumeRange.x, volumeRange.y, strength);
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.panStereo = pan;
            source.Play();
        }
    }
}
