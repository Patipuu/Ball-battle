using TeamNet.Multiplayer.Core;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// Optional voice (EOS RTC room) hook for <see cref="EosLobbyService"/>. The service stays voice-agnostic:
    /// it asks the hook how to configure the lobby's RTC room on create/join and reports back each entry,
    /// so the game's voice module can start its own input-recovery bookkeeping.
    /// Pass <c>null</c> to the service (the default) for a lobby without voice.
    /// </summary>
    public interface ILobbyRtcHook
    {
        /// <summary>Open (create) / auto-join (join) the lobby's RTC room.</summary>
        bool EnableRtcRoom { get; }

        /// <summary>The game feeds microphone samples itself instead of the SDK capturing from the device.</summary>
        bool UseManualAudioInput { get; }

        /// <summary>Local audio input starts muted.</summary>
        bool LocalAudioInputStartsMuted { get; }

        /// <summary>
        /// Called once per successful create/join commit. <paramref name="manualAudioInput"/> is the value
        /// used for that entry, so a recovery routine can tell later whether the setup changed.
        /// </summary>
        void OnLobbyEntered(PlayerId localUser, string lobbyId, bool manualAudioInput);
    }
}
