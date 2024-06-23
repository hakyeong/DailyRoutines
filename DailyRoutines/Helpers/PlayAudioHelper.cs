using WMPLib;

namespace DailyRoutines.Helpers;

public class PlayAudioHelper
{
    private static readonly WindowsMediaPlayer Player = new();

    public PlayAudioHelper() { Player.PlayStateChange += Player_PlayStateChange; }

    private void Player_PlayStateChange(int NewState)
    {
        if ((WMPPlayState)NewState == WMPPlayState.wmppsStopped) Stop();
    }

    public void Play(string path)
    {
        Player.URL = path;
        Player.controls.play();
    }

    public void Stop()
    {
        Player.close();
    }
}
