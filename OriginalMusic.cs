using Godot;
using System;
using System.Collections.Generic;

// Tracks are local copies of the soundtrack exported from the stock SWF.
public static class OriginalMusic
{
    public const string Menu = "36_MusicHype1.ogg";
    public const string Shop = "21_MusicShop1.ogg";
    public const string BonusShop = "13_MusicBonusShop1.ogg";
    public const string BlackMarket = "77_MusicBonusShop3.ogg";
    public const string Flight = "15_MusicPunk1.ogg";
    public const string Results = "46_MusicResults.ogg";
    public const string StoryIntro = "122_MusicBonusShop2.ogg";
    // Music.playResultsMusic sets loop=false in Flash. This port's handoff asks for all music to loop.
    public static bool LoopResults { get; set; } = true;
    public static readonly string[] FlightLayers =
    {
        "15_MusicPunk1.ogg", "14_MusicPunk2.ogg", "16_MusicPunk3.ogg"
    };

    public static AudioStreamPlayer Create(Node owner)
    {
        OriginalAudio.Ensure();
        var player = new AudioStreamPlayer();
        owner.AddChild(player);
        return player;
    }

    public static void Play(AudioStreamPlayer player, string file, bool loop = true,
        float gain = 1, int? fadeInMs = null)
    {
        var track = OriginalAudio.LoadClip(file, loop);
        if (track == null) return;
        OriginalAudio.Start(player, track, gain, fadeInMs ?? (file == Menu ? 500 : 0));
    }

    public static void PlayResults(AudioStreamPlayer player) => Play(player, Results, LoopResults);

    public static OriginalFlightMusicSet CreateFlightSet(Node owner)
    {
        var players = new List<AudioStreamPlayer>(FlightLayers.Length);
        foreach (string path in FlightLayers)
        {
            var player = Create(owner);
            players.Add(player);
        }
        return new OriginalFlightMusicSet(players);
    }
}

// Music.as: three simultaneous game tracks, 80-sample speed buffer, and the
// original 10/15/25/30 speed-zone fades. The source files are stock soundtrack.
public sealed class OriginalFlightMusicSet
{
    private readonly AudioStreamPlayer[] _players;
    private readonly double[] _speedBuffer = new double[80];
    private readonly float[] _layerGains = new float[OriginalMusic.FlightLayers.Length];
    private int _bufferIndex;
    private double _speedSum;
    private float _dayEndGain = 1;

    public OriginalFlightMusicSet(IReadOnlyList<AudioStreamPlayer> players)
    {
        _players = new AudioStreamPlayer[players.Count];
        for (int i = 0; i < players.Count; i++) _players[i] = players[i];
    }

    public void Play()
    {
        _bufferIndex = 0;
        _speedSum = 0;
        _dayEndGain = 1;
        Array.Clear(_speedBuffer, 0, _speedBuffer.Length);
        for (int i = 0; i < _players.Length; i++)
        {
            _layerGains[i] = i == 0 ? 1 : 0;
            OriginalMusic.Play(_players[i], OriginalMusic.FlightLayers[i], gain: _layerGains[i]);
        }
    }

    public void UpdateSpeed(double speed)
    {
        _speedSum -= _speedBuffer[_bufferIndex];
        _speedBuffer[_bufferIndex] = speed;
        _speedSum += speed;
        _bufferIndex = (_bufferIndex + 1) % _speedBuffer.Length;
        double buffered = _speedSum / _speedBuffer.Length;

        double track1 = OriginalFlightPhysics.Interpolate(1, 0, 10, 15, buffered, 4);
        double track2 = buffered <= 20
            ? OriginalFlightPhysics.Interpolate(0, 1, 10, 15, buffered, .25)
            : OriginalFlightPhysics.Interpolate(1, 0, 25, 30, buffered, 4);
        double track3 = OriginalFlightPhysics.Interpolate(0, 1, 25, 30, buffered, .25);
        SetLinearVolume(0, track1);
        SetLinearVolume(1, track2);
        SetLinearVolume(2, track3);
    }

    public void Stop()
    {
        foreach (var player in _players) OriginalAudio.Stop(player);
    }

    public void SetDayEndGain(float gain)
    {
        _dayEndGain = Mathf.Clamp(gain, 0, 1);
        for (int i = 0; i < _players.Length; i++) ApplyLayerGain(i);
    }

    private void SetLinearVolume(int index, double value)
    {
        if (index < 0 || index >= _players.Length) return;
        _layerGains[index] = (float)Math.Clamp(value, 0, 1);
        ApplyLayerGain(index);
    }

    private void ApplyLayerGain(int index) =>
        OriginalAudio.SetGain(_players[index], _layerGains[index] * _dayEndGain);
}
