using System.Speech.Synthesis;

namespace DailyRoutines.Notifications;

public class TextToTalk : DailyNotificationBase
{
    public override NotifyType NotifyType => NotifyType.TextToTalk;

    private static SpeechSynthesizer? TTS { get; set; }

    public override void Init()
    {
        TTS ??= new SpeechSynthesizer();
        TTS.SetOutputToDefaultAudioDevice();
    }

    public static void Speak(string content, bool cancelPrevious)
    {
        if (cancelPrevious && TTS.State == SynthesizerState.Speaking)
        {
            TTS.SpeakAsyncCancelAll();
        }

        TTS.SpeakAsync(content);
    }

    public override void Uninit()
    {
        TTS?.Dispose();
        TTS = null;
    }
}
