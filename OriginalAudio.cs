using Godot;
using System;
using System.Runtime.CompilerServices;

public enum OriginalSound
{
    ButtonClick,
    ShopClick,
    MediumClick,
    Cash,
    CashLow,
    EquipSwap,
    SpringBoing,
    Medal2,
    Medal3,
    IntroWind,
    IntroBoing,
    IntroExplosion,
    IntroDummyCrash,
    IntroPenguinStep,
    IntroBookBang,
    IntroKnock,
    IntroBuildUpWhoosh,
    IntroDoorBust
}

// src/audio/Audio.as and SoundObject.as. Mute affects the mixer, so tracks
// continue at their current position while the speaker is disabled.
public static class OriginalAudio
{
    private sealed class Voice
    {
        public bool Transient;
        public float Gain;
        public float Fade = 1;
        public Tween? Tween;
    }

    private const float MasterVolume = 1.1f;
    private const float CategoryVolume = .75f;
    private const int FadeIntervalMs = 30;
    private const float SilenceDb = -80;
    private const string Root = "res://Assets/Flight/";
    private static readonly ConditionalWeakTable<AudioStreamPlayer, Voice> Voices = new();
    private static bool _initialized;

    public static bool IsMuted
    {
        get
        {
            Ensure();
            return AudioServer.IsBusMute(AudioServer.GetBusIndex("Master"));
        }
    }

    public static void Ensure()
    {
        if (_initialized) return;
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), false);
        _initialized = true;
    }

    public static bool ToggleMute()
    {
        bool mute = !IsMuted;
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), mute);
        return mute;
    }

    public static Texture2D SpeakerTexture() => GD.Load<Texture2D>(
        IsMuted ? "res://Assets/Start/speaker-off.png" : "res://Assets/Start/speaker-on.png");

    public static AudioStreamPlayer? PlaySfx(Node owner, OriginalSound sound, bool loop = false,
        int fadeInMs = 0, float? gain = null)
    {
        var (file, sourceGain) = SoundAsset(sound);
        var clip = LoadClip(file, loop);
        if (clip == null) return null;
        var player = new AudioStreamPlayer();
        owner.AddChild(player);
        Start(player, clip, gain ?? sourceGain, fadeInMs, transient: true);
        if (!loop) player.Finished += player.QueueFree;
        return player;
    }

    private static (string File, float Gain) SoundAsset(OriginalSound sound) => sound switch
    {
        OriginalSound.ButtonClick => ("57_sound.Bubble2.mp3", 1),
        OriginalSound.ShopClick => ("60_sound.Pop1.mp3", .6f),
        OriginalSound.MediumClick => ("56_sound.Pop3.mp3", .6f),
        OriginalSound.Cash => ("133_sound.Cash2.mp3", 1),
        OriginalSound.CashLow => ("133_sound.Cash2.mp3", .8f),
        OriginalSound.EquipSwap => ("125_sound.Equip2.mp3", .8f),
        OriginalSound.SpringBoing => ("31_sound.SpringBoing.mp3", 1),
        OriginalSound.Medal2 => ("53_sound.Medal2.ogg", .8f),
        OriginalSound.Medal3 => ("131_sound.Medal3.mp3", 1),
        OriginalSound.IntroWind => ("2_sound.intro.WindSound.ogg", 1),
        OriginalSound.IntroBoing => ("30_sound.newgameintro.BoingSound.ogg", 1),
        OriginalSound.IntroExplosion => ("102_sound.Explode2.mp3", .6f),
        OriginalSound.IntroDummyCrash => ("121_sound.newgameintro.DummyCrash.ogg", 1),
        OriginalSound.IntroPenguinStep => ("24_sound.newgameintro.PenguinStep.ogg", 1),
        OriginalSound.IntroBookBang => ("79_sound.newgameintro.BookBang.ogg", 1),
        OriginalSound.IntroKnock => ("1_sound.newgameintro.KnockKnockKnock.ogg", .6f),
        OriginalSound.IntroBuildUpWhoosh => ("41_sound.newgameintro.BuildUpWhoosh.ogg", 1),
        OriginalSound.IntroDoorBust => ("23_sound.newgameintro.DoorBust.ogg", .5f),
        _ => throw new ArgumentOutOfRangeException(nameof(sound), sound, null)
    };

    internal static AudioStream? LoadClip(string file, bool loop)
    {
        string path = Root + file;
        var source = GD.Load<AudioStream>(path);
        if (source == null)
        {
            GD.PushError($"Missing original LTF3 audio {path}");
            return null;
        }
        var clip = (AudioStream)source.Duplicate();
        switch (clip)
        {
            case AudioStreamOggVorbis ogg:
                ogg.Loop = loop;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = loop;
                break;
            case AudioStreamWav wav:
                wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
                break;
        }
        return clip;
    }

    internal static void Start(AudioStreamPlayer player, AudioStream clip, float gain,
        int fadeInMs = 0, bool transient = false)
    {
        Ensure();
        var voice = Voices.GetValue(player, _ => new Voice());
        voice.Tween?.Kill();
        voice.Transient = transient;
        voice.Gain = gain;
        voice.Fade = fadeInMs > 0 ? 0 : 1;
        ApplyGain(player, voice);
        player.Stream = clip;
        player.Play();
        if (fadeInMs > 0) FadeTo(player, voice, 1, fadeInMs);
    }

    internal static void SetGain(AudioStreamPlayer player, float gain)
    {
        if (!Voices.TryGetValue(player, out var voice)) return;
        voice.Gain = gain;
        ApplyGain(player, voice);
    }

    public static void FadeOut(AudioStreamPlayer? player, int durationMs = 300)
    {
        if (!GodotObject.IsInstanceValid(player) || player!.IsQueuedForDeletion() || !player.Playing ||
            !Voices.TryGetValue(player, out var voice)) return;
        FadeTo(player, voice, 0, durationMs);
    }

    public static void Stop(AudioStreamPlayer? player)
    {
        if (!GodotObject.IsInstanceValid(player) || player!.IsQueuedForDeletion()) return;
        if (Voices.TryGetValue(player, out var voice))
        {
            voice.Tween?.Kill();
            if (voice.Transient) player.QueueFree();
        }
        player.Stop();
    }

    public static void StopAll()
    {
        foreach (var entry in Voices) Stop(entry.Key);
    }

    // FadingSoundObject.as applies a linear fade every 30 ms, before audioTaper.
    private static void FadeTo(AudioStreamPlayer player, Voice voice, float end, int durationMs)
    {
        voice.Tween?.Kill();
        if (durationMs <= 0)
        {
            voice.Fade = end;
            ApplyGain(player, voice);
            if (end == 0) Stop(player);
            return;
        }
        float start = voice.Fade;
        int ticks = (durationMs + FadeIntervalMs - 1) / FadeIntervalMs;
        var tween = player.CreateTween();
        voice.Tween = tween;
        for (int i = 1; i <= ticks; i++)
        {
            float fraction = Math.Min(1f, (float)(i * FadeIntervalMs) / durationMs);
            tween.TweenInterval(FadeIntervalMs / 1000.0);
            tween.TweenCallback(Callable.From(() =>
            {
                voice.Fade = Mathf.Lerp(start, end, fraction);
                ApplyGain(player, voice);
            }));
        }
        if (end == 0)
        {
            tween.TweenCallback(Callable.From(() =>
            {
                voice.Tween = null;
                Stop(player);
            }));
        }
    }

    private static void ApplyGain(AudioStreamPlayer player, Voice voice)
    {
        // lbg/utils/Tools.as::audioTaper.
        float value = Math.Max(0, MasterVolume * CategoryVolume * voice.Gain * voice.Fade);
        float tapered = Math.Min(2, value <= 1 ? MathF.Pow(value, 2.2f) : value * 2.2f);
        player.VolumeDb = tapered <= .0001f ? SilenceDb : Mathf.LinearToDb(tapered);
    }
}
