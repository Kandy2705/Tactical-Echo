using TacticalEcho.SaveLoad.Data;

namespace TacticalEcho.SaveLoad.Contracts
{
    public interface ISaveParticipant
    {
        string StableId { get; }
        SaveRecord CaptureState();
        void RestoreState(SaveRecord record);
    }
}
