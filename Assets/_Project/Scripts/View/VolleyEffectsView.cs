using System.Collections.Generic;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Marchlands.View
{
    /// <summary>
    /// Обратная связь залпа: пороховой дым вдоль фронта стрелка, падающие фигурки в цели и звук.
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

        [Header("Павшие")]
        [SerializeField] private Sprite fallenSprite;
        [SerializeField] private float fallenLifetime = 5f;
        [SerializeField] private float fallenFadeTime = 1.5f;
        [Tooltip("Насколько темнее цвета стороны лежащая фигурка.")]
        [SerializeField] private float fallenDarkening = 0.75f;
        [SerializeField] private int fallenSortingOrder = 15;

        [Header("Звук")]
        [SerializeField] private AudioClip volleyClip;
        [SerializeField] private int audioVoices = 4;
        [SerializeField] private Vector2 volumeRange = new Vector2(0.35f, 1f);
        [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.08f);
        [Tooltip("Насколько сильно звук смещается в стерео по положению на экране.")]
        [SerializeField] private float stereoWidth = 0.6f;

        private readonly List<Fallen> fallen = new List<Fallen>();
        private readonly List<float2> slots = new List<float2>();
        private readonly List<int> slotIndices = new List<int>();
        private readonly List<AudioSource> voices = new List<AudioSource>();

        private Match match;
        private int nextVoice;

        private sealed class Fallen
        {
            public SpriteRenderer Renderer;
            public float Age;
            public Color Color;
        }

        public void Initialize(Match match)
        {
            this.match = match;
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
                if (shooter != null)
                {
                    EmitSmoke(shooter);
                    PlaySound(shooter, volley.Shooters);
                }

                SpawnFallen(volley);
            }
        }

        private void Update()
        {
            for (int i = fallen.Count - 1; i >= 0; i--)
            {
                Fallen body = fallen[i];
                if (!body.Renderer.gameObject.activeSelf)
                {
                    continue;
                }

                body.Age += Time.deltaTime;
                float fade = math.saturate((fallenLifetime - body.Age) / fallenFadeTime);
                Color color = body.Color;
                color.a *= fade;
                body.Renderer.color = color;
                if (body.Age >= fallenLifetime)
                {
                    body.Renderer.gameObject.SetActive(false);
                }
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
                var emit = new ParticleSystem.EmitParams
                {
                    position = new Vector3(position.x, position.y, 0f),
                    velocity = new Vector3(velocity.x, velocity.y, 0f),
                    startSize = Random.Range(puffSize.x, puffSize.y),
                    startLifetime = smokeDuration * Random.Range(0.8f, 1f),
                    startColor = smokeColor,
                    rotation = Random.Range(0f, 360f),
                    applyShapeToPosition = false
                };
                smoke.Emit(emit, 1);
            }
        }

        private void SpawnFallen(VolleyEvent volley)
        {
            if (volley.Casualties <= 0)
            {
                return;
            }

            FormationLayout.GetLocalSlots(volley.TargetFormation, volley.TargetAliveBefore, match.Settings.squads, slots);
            slotIndices.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                slotIndices.Add(i);
            }

            Side targetSide = volley.ShooterSide == Side.France ? Side.Austria : Side.France;
            Color color = squadViews.Style.GetSideColor(targetSide) * fallenDarkening;
            color.a = 1f;

            for (int k = 0; k < volley.Casualties && slotIndices.Count > 0; k++)
            {
                int pick = Random.Range(0, slotIndices.Count);
                float2 local = slots[slotIndices[pick]];
                slotIndices.RemoveAt(pick);

                float2 world = FormationLayout.LocalToWorld(local, volley.TargetPosition, volley.TargetFacing);
                Fallen body = TakeFallen();
                body.Age = 0f;
                body.Color = color;
                body.Renderer.color = color;
                body.Renderer.transform.SetPositionAndRotation(
                    new Vector3(world.x, world.y, 0f), Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                body.Renderer.gameObject.SetActive(true);
            }
        }

        private Fallen TakeFallen()
        {
            foreach (Fallen body in fallen)
            {
                if (!body.Renderer.gameObject.activeSelf)
                {
                    return body;
                }
            }

            var child = new GameObject($"Fallen {fallen.Count}");
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = fallenSprite;
            spriteRenderer.sortingOrder = fallenSortingOrder;
            var created = new Fallen { Renderer = spriteRenderer };
            fallen.Add(created);
            return created;
        }

        private void PlaySound(Squad shooter, int shooters)
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
                Vector3 viewport = view.WorldToViewportPoint(new Vector3(shooter.Position.x, shooter.Position.y, 0f));
                pan = math.clamp((viewport.x - 0.5f) * 2f, -1f, 1f) * stereoWidth;
            }

            float strength = math.saturate((float)shooters / match.Settings.squads.squadSize);
            source.clip = volleyClip;
            source.volume = math.lerp(volumeRange.x, volumeRange.y, strength);
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.panStereo = pan;
            source.Play();
        }
    }
}
