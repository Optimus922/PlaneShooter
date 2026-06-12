using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效管理器(阶段7)。单例 + AudioSource 池,集中播放游戏音效。
///
/// 设计要点:
/// - 用一组可复用的 AudioSource(池)来 PlayOneShot,支持多音效同时叠放(连射不互相打断)。
/// - 各事件用语义化方法(PlayShoot/PlayHit/...),调用方不关心具体 clip。
/// - 全局音量与总开关;clip 在 Inspector 拖入。
/// - 与 GameManager 同为单例风格,通过 Instance 全局访问。
/// </summary>
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("音效片段")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip explosionClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip gameOverClip;

    [Header("设置")]
    [Tooltip("总音量(0~1)。")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.8f;

    [Tooltip("音效总开关。")]
    [SerializeField] private bool sfxEnabled = true;

    [Tooltip("AudioSource 池大小(可同时播放的音效数)。")]
    [SerializeField] private int voiceCount = 8;

    private readonly List<AudioSource> voices = new List<AudioSource>();
    private int nextVoice;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 预创建一组 AudioSource 作为播放声部
        for (int i = 0; i < Mathf.Max(1, voiceCount); i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            voices.Add(src);
        }
    }

    /// <summary>播放一个 clip(轮流用池里的声部,支持叠放)。volScale 为相对音量。</summary>
    public void Play(AudioClip clip, float volScale = 1f, float pitchJitter = 0f)
    {
        if (!sfxEnabled || clip == null || voices.Count == 0) return;

        AudioSource src = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;

        // 轻微随机音高,避免连射时听感呆板(可设 0 关闭)
        src.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        src.PlayOneShot(clip, Mathf.Clamp01(masterVolume * volScale));
    }

    // ---- 语义化事件方法 ----
    public void PlayShoot() => Play(shootClip, 0.5f, 0.06f);
    public void PlayHit() => Play(hitClip, 0.7f, 0.05f);
    public void PlayExplosion() => Play(explosionClip, 1f, 0.04f);
    public void PlayHurt() => Play(hurtClip, 0.9f);
    public void PlayGameOver() => Play(gameOverClip, 1f);

    public void SetEnabled(bool on) => sfxEnabled = on;
    public void SetVolume(float v) => masterVolume = Mathf.Clamp01(v);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
