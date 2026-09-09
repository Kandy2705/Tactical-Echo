using System;
using System.Collections.Generic;
using System.IO;
using TacticalEcho.SaveLoad.Contracts;
using TacticalEcho.SaveLoad.Data;
using UnityEngine;

namespace TacticalEcho.SaveLoad.Persistence
{
    public sealed class SaveManager : MonoBehaviour
    {
        private readonly List<ISaveParticipant> participants = new();

        private string MainPath => Path.Combine(Application.persistentDataPath, "tactical-echo-save.json");
        private string TempPath => MainPath + ".tmp";
        private string BackupPath => MainPath + ".bak";

        public void Register(ISaveParticipant participant)
        {
            if (participant != null && !participants.Contains(participant))
            {
                participants.Add(participant);
            }
        }

        public void Unregister(ISaveParticipant participant)
        {
            participants.Remove(participant);
        }

        public void Save()
        {
            SaveGameData data = new()
            {
                timestampUtcTicks = DateTime.UtcNow.Ticks
            };

            foreach (ISaveParticipant participant in participants)
            {
                data.records.Add(participant.CaptureState());
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(MainPath))
            {
                File.Replace(TempPath, MainPath, BackupPath);
            }
            else
            {
                File.Move(TempPath, MainPath);
            }
        }

        public bool TryLoad(out SaveGameData data)
        {
            if (TryRead(MainPath, out data))
            {
                return true;
            }

            return TryRead(BackupPath, out data);
        }

        private static bool TryRead(string path, out SaveGameData data)
        {
            data = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to read save file '{path}': {exception.Message}");
                return false;
            }
        }
    }
}
