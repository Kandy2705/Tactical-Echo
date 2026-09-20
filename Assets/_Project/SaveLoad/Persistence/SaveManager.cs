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

        public string MainPath => Path.Combine(Application.persistentDataPath, "tactical-echo-save.json");
        public string TempPath => MainPath + ".tmp";
        public string BackupPath => MainPath + ".bak";

        public int ParticipantCount => participants.Count;
        public bool HasSaveFile => File.Exists(MainPath);
        public bool HasBackupFile => File.Exists(BackupPath);

        public bool LastSaveSucceeded { get; private set; }
        public string LastSaveError { get; private set; } = string.Empty;
        public DateTime LastSaveUtc { get; private set; }

        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.NotAttempted;
        public string LastLoadReason { get; private set; } = string.Empty;
        public bool LastLoadUsedBackup { get; private set; }
        public int LastLoadedSchemaVersion { get; private set; }

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

        public bool Save()
        {
            SaveGameData data = new()
            {
                schemaVersion = SaveSchema.CurrentVersion,
                timestampUtcTicks = DateTime.UtcNow.Ticks
            };

            foreach (ISaveParticipant participant in participants)
            {
                SaveRecord record = participant?.CaptureState();
                if (record != null)
                {
                    data.records.Add(record);
                }
            }

            try
            {
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

                LastSaveSucceeded = true;
                LastSaveError = string.Empty;
                LastSaveUtc = new DateTime(data.timestampUtcTicks, DateTimeKind.Utc);
                return true;
            }
            catch (Exception exception)
            {
                LastSaveSucceeded = false;
                LastSaveError = exception.Message;
                Debug.LogWarning($"[Save] Failed to write save file: {exception.Message}");
                return false;
            }
        }







        public bool TryLoad(out SaveGameData data)
        {
            LastLoadUsedBackup = false;
            if (TryReadPrepared(MainPath, out data, out SaveLoadStatus mainStatus, out string mainReason))
            {
                Accept(mainStatus, mainReason, data);
                return true;
            }

            if (TryReadPrepared(BackupPath, out data, out SaveLoadStatus backupStatus, out string backupReason))
            {
                LastLoadUsedBackup = true;
                Accept(backupStatus, backupReason, data);
                Debug.LogWarning($"[Save] Main save was unusable ({mainReason}); loaded the backup instead.");
                return true;
            }


            LastLoadStatus = mainStatus != SaveLoadStatus.MissingFile ? mainStatus : backupStatus;
            LastLoadReason = mainStatus != SaveLoadStatus.MissingFile ? mainReason : backupReason;
            LastLoadedSchemaVersion = 0;
            data = null;
            return false;
        }

        private void Accept(SaveLoadStatus status, string reason, SaveGameData data)
        {
            LastLoadStatus = status;
            LastLoadReason = reason;
            LastLoadedSchemaVersion = data.schemaVersion;
        }

        private static bool TryReadPrepared(
            string path,
            out SaveGameData data,
            out SaveLoadStatus status,
            out string reason)
        {
            data = null;

            if (!File.Exists(path))
            {
                status = SaveLoadStatus.MissingFile;
                reason = "no save file";
                return false;
            }

            SaveGameData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                status = SaveLoadStatus.Unreadable;
                reason = exception.Message;
                Debug.LogWarning($"[Save] Failed to read save file '{path}': {exception.Message}");
                return false;
            }

            if (!SaveSchema.TryPrepare(parsed, out status, out reason))
            {
                Debug.LogWarning($"[Save] Rejected save file '{path}': {reason}");
                return false;
            }

            data = parsed;
            return true;
        }
    }
}
