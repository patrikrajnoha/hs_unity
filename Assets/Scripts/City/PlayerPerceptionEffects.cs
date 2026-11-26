using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace HighStakes.City
{
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class PlayerPerceptionEffects : MonoBehaviour
    {
        [Header("Drug effect")]
        [SerializeField, Min(60)] private float duration = 60f;
        [SerializeField, Min(60)] private float maximumDuration = 300f;
        [SerializeField, Min(0)] private float fadeIn = 4f, fadeOut = 6f;
        [SerializeField] private Color tint = new Color(0.85f, 0.8f, 1f);
        [SerializeField, Range(0, 1)] private float distortion = 0.65f, blur = 0.5f, chromatic = 0.85f;
        [SerializeField, Min(0)] private float distortionSpeed = 0.5f, rotationIntensity = 5f, rotationSpeed = 0.45f;
        [SerializeField, Min(1)] private float maximumIntensity = 2f;
        [Header("Heartbeat")]
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private float minimumSpeed = 0.8f, maximumSpeed = 1.5f;
        [SerializeField, Range(0, 1)] private float minimumVolume = 0.4f, maximumVolume = 0.85f;
        [SerializeField, Min(1)] private float intensityDistance = 30f;
        [SerializeField, Min(0.01f)] private float heartbeatFadeIn = 1f, heartbeatFadeOut = 2f;
        [SerializeField] private bool vignetteEnabled = true;
        [SerializeField, Range(0, 1)] private float vignetteIntensity = 0.2f;

        private Camera cam;
        private UniversalAdditionalCameraData cameraData;
        private bool oldPostProcessing;
        private LayerMask oldVolumeMask;
        private Volume volume;
        private VolumeProfile profile;
        private ColorAdjustments colors;
        private LensDistortion lens;
        private ChromaticAberration chroma;
        private DepthOfField depth;
        private Vignette vignette;
        private AudioSource heartbeat;
        private AudioClip generatedClip;
        private CanvasGroup heartCanvasGroup;
        private RectTransform heartIcon;
        private Texture2D heartTexture;
        private Sprite heartSprite;
        private float remaining, strength, currentIntensity, recoveryStart, recoveryElapsed, pulse, heartBlend, appliedRoll;
        private Color activeTint = Color.white, targetTint = Color.white;

        private void Awake()
        {
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.playOnAwake = false; heartbeat.loop = true; heartbeat.spatialBlend = 0;
            if (heartbeatClip == null) heartbeatClip = generatedClip = CreateHeartbeat();
            heartbeat.clip = heartbeatClip;
            CreateHeartHud();
            InitializeCamera();
        }

        private void InitializeCamera()
        {
            if (cam != null) return;
            cam = GetComponentInChildren<Camera>(true);
            if (cam == null) return;
            cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            oldPostProcessing = cameraData.renderPostProcessing;
            oldVolumeMask = cameraData.volumeLayerMask;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1;
            var go = new GameObject("Player perception volume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1000;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            colors = profile.Add<ColorAdjustments>();
            lens = profile.Add<LensDistortion>();
            chroma = profile.Add<ChromaticAberration>();
            depth = profile.Add<DepthOfField>();
            vignette = profile.Add<Vignette>();
            volume.weight = 0;
        }

        public void Consume(string id)
        {
            targetTint = id == "GreenBud" ? new Color(.65f, 1f, .65f) :
                id == "Mushroom" ? new Color(1f, .55f, 1f) :
                id == "CocaLeaf" ? new Color(.65f, .8f, 1f) : tint;
            bool active = remaining > 0 || currentIntensity > 0;
            strength = Mathf.Min(Mathf.Max(1f, maximumIntensity), active ? strength + .3f : 1f);
            float extension = Mathf.Max(60f, duration);
            remaining = Mathf.Min(Mathf.Max(extension, maximumDuration), remaining + extension);
            recoveryElapsed = 0;
        }

        private void Update()
        {
            if (cam == null) InitializeCamera();
            RemoveRoll();
            var stats = GetComponent<PlayerStats>();
            if (GameMaster.GameOverActive || (stats != null && stats.health <= 0)) { ResetEffects(); return; }
            if (remaining > 0)
            {
                remaining = Mathf.Max(0, remaining - Time.deltaTime);
                currentIntensity = Mathf.MoveTowards(currentIntensity, strength,
                    Time.deltaTime * Mathf.Max(1f, maximumIntensity) / Mathf.Max(.01f, fadeIn));
                if (remaining == 0) { recoveryStart = currentIntensity; recoveryElapsed = 0; }
            }
            else if (currentIntensity > 0)
            {
                recoveryElapsed += Time.deltaTime;
                currentIntensity = fadeOut <= 0 ? 0 : recoveryStart * Mathf.Clamp01(1f - recoveryElapsed / fadeOut);
                if (currentIntensity <= 0) strength = 0;
            }
            activeTint = Color.Lerp(activeTint, targetTint, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, fadeIn)));
            float nearest = float.PositiveInfinity;
            foreach (var officer in PoliceHeatRegistry.ActivePolice)
                if (officer != null && officer.isChasing)
                    nearest = Mathf.Min(nearest, Vector3.Distance(transform.position, officer.transform.position));
            bool pursued = !float.IsPositiveInfinity(nearest);
            heartBlend = Mathf.MoveTowards(heartBlend, pursued ? 1 : 0,
                Time.deltaTime / Mathf.Max(.01f, pursued ? heartbeatFadeIn : heartbeatFadeOut));
            float proximity = pursued ? 1f - Mathf.Clamp01(nearest / Mathf.Max(1, intensityDistance)) : 0;
            if (heartbeat != null)
            {
                if (heartBlend > 0 && !heartbeat.isPlaying) heartbeat.Play();
                if (heartBlend <= 0 && heartbeat.isPlaying) heartbeat.Stop();
                heartbeat.pitch = Mathf.Lerp(minimumSpeed, maximumSpeed, proximity);
                var audio = GameAudio.Instance;
                heartbeat.volume = heartBlend * Mathf.Lerp(minimumVolume, maximumVolume, proximity) *
                    (audio.Muted ? 0 : audio.GetVolume(AudioBus.Master) * audio.GetVolume(AudioBus.Effects));
            }
            pulse += Time.deltaTime * (heartbeat != null ? heartbeat.pitch : 1f);
            UpdateHeartHud();
        }

        private void CreateHeartHud()
        {
            var canvasObject = new GameObject("Police heartbeat HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            heartCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
            heartCanvasGroup.alpha = 0; heartCanvasGroup.blocksRaycasts = false; heartCanvasGroup.interactable = false;

            var backgroundObject = new GameObject("Heartbeat background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            var background = backgroundObject.GetComponent<Image>();
            background.color = new Color32(26, 17, 35, 235); background.raycastTarget = false;
            var rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-224, -32); rect.sizeDelta = new Vector2(68, 68);

            const int size = 64;
            heartTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            heartTexture.name = "Heartbeat icon";
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = (x + .5f - size * .5f) / (size * .40f);
                float py = (y + .5f - size * .52f) / (size * .40f);
                float a = px * px + py * py - 1f;
                float edge = a * a * a - px * px * py * py * py;
                heartTexture.SetPixel(x, y, edge <= 0 ? Color.white : Color.clear);
            }
            heartTexture.Apply();
            heartSprite = Sprite.Create(heartTexture, new Rect(0, 0, size, size), Vector2.one * .5f);
            var iconObject = new GameObject("Heart", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(backgroundObject.transform, false);
            var icon = iconObject.GetComponent<Image>(); icon.sprite = heartSprite;
            icon.color = new Color32(255, 90, 112, 255); icon.raycastTarget = false;
            heartIcon = icon.rectTransform;
            heartIcon.anchorMin = heartIcon.anchorMax = heartIcon.pivot = Vector2.one * .5f;
            heartIcon.anchoredPosition = Vector2.zero; heartIcon.sizeDelta = new Vector2(42, 42);
        }

        private void UpdateHeartHud()
        {
            if (heartCanvasGroup == null || heartIcon == null) return;
            heartCanvasGroup.alpha = heartBlend;
            float phase = heartbeat != null && heartbeat.clip != null && heartbeat.isPlaying
                ? (float)heartbeat.timeSamples / heartbeat.clip.frequency : 0f;
            float first = Mathf.Exp(-Mathf.Pow((phase - .035f) / .045f, 2));
            float second = Mathf.Exp(-Mathf.Pow((phase - .21f) / .055f, 2));
            heartIcon.localScale = Vector3.one * (1f + .24f * first + .17f * second);
        }

        private void LateUpdate()
        {
            if (volume == null) return;
            float amount = Mathf.Clamp01(currentIntensity / Mathf.Max(1, maximumIntensity));
            float wave = .5f + .5f * Mathf.Sin(Time.time * distortionSpeed);
            volume.weight = amount > 0 || (heartBlend > 0 && vignetteEnabled) ? 1 : 0;
            colors.colorFilter.overrideState = colors.saturation.overrideState = colors.contrast.overrideState = amount > 0;
            lens.intensity.overrideState = chroma.intensity.overrideState = amount > 0;
            depth.mode.overrideState = depth.gaussianStart.overrideState = depth.gaussianEnd.overrideState = amount > 0;
            vignette.color.overrideState = vignette.intensity.overrideState = vignetteEnabled && heartBlend > 0;
            colors.colorFilter.value = Color.Lerp(Color.white, activeTint, amount);
            colors.saturation.value = 50f * amount; colors.contrast.value = 25f * amount;
            lens.intensity.value = -distortion * amount * (.5f + .5f * wave);
            chroma.intensity.value = chromatic * amount * (.7f + .3f * wave);
            depth.mode.value = amount > 0 ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Off;
            depth.gaussianStart.value = Mathf.Lerp(30, 2, blur * amount);
            depth.gaussianEnd.value = Mathf.Lerp(40, 8, blur * amount);
            vignette.color.value = new Color(.4f, 0, 0);
            vignette.intensity.value = vignetteEnabled ? vignetteIntensity * heartBlend * (.75f + .25f * Mathf.Sin(pulse * Mathf.PI * 2)) : 0;
            if (cam != null && amount > 0)
            {
                appliedRoll = Mathf.Sin(Time.time * rotationSpeed) * rotationIntensity * amount;
                cam.transform.localRotation *= Quaternion.Euler(0, 0, appliedRoll);
            }
        }

        private void RemoveRoll()
        {
            if (cam != null && appliedRoll != 0) cam.transform.localRotation *= Quaternion.Euler(0, 0, -appliedRoll);
            appliedRoll = 0;
        }

        public void ResetEffects()
        {
            RemoveRoll(); remaining = strength = currentIntensity = recoveryStart = recoveryElapsed = heartBlend = 0;
            activeTint = targetTint = Color.white;
            if (volume != null) volume.weight = 0;
            if (heartbeat != null) heartbeat.Stop();
            if (heartCanvasGroup != null) heartCanvasGroup.alpha = 0;
            GetComponent<PlayerProductEffects>()?.ResetProductEffects();
        }
        private void OnDisable() => ResetEffects();
        private void OnDestroy()
        {
            ResetEffects();
            if (cameraData != null) { cameraData.renderPostProcessing = oldPostProcessing; cameraData.volumeLayerMask = oldVolumeMask; }
            if (profile != null) Destroy(profile);
            if (generatedClip != null) Destroy(generatedClip);
            if (heartSprite != null) Destroy(heartSprite);
            if (heartTexture != null) Destroy(heartTexture);
        }
        private static AudioClip CreateHeartbeat()
        {
            const int rate = 22050;
            var samples = new float[rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float a = Mathf.Exp(-t * 18f) * (0.65f * Mathf.Sin(2 * Mathf.PI * 90f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * 180f * t));
                float u = Mathf.Max(0, t - .18f);
                float b = t >= .18f ? .7f * Mathf.Exp(-u * 20f) * (0.65f * Mathf.Sin(2 * Mathf.PI * 75f * u) + 0.35f * Mathf.Sin(2 * Mathf.PI * 150f * u)) : 0;
                samples[i] = Mathf.Clamp(a + b, -1f, 1f);
            }
            var clip = AudioClip.Create("Generated heartbeat", rate, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
