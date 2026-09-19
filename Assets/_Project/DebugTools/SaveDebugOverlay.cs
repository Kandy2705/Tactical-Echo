using System;
using System.IO;
using System.Text;
using TacticalEcho.SaveLoad.Data;
using TacticalEcho.SaveLoad.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalEcho.DebugTools
{
    
    
    
    
    
    public sealed class SaveDebugOverlay : DebugOverlayBase
    {
        [Header("Observed")]
        [SerializeField] private SaveManager observedSaveManager;
        [Tooltip("Debug harness only: SaveManager is not attached to anything yet, so the overlay can host one to exercise save/load.")]
        [SerializeField] private bool createSaveManagerIfMissing = true;

        [Header("Debug Keys")]
        [SerializeField] private bool enableHotkeys = true;
        [SerializeField] private Key saveKey = Key.F5;
        [SerializeField] private Key loadKey = Key.F9;

        protected override string OverlayName => "Save Debug";

        protected override void Awake()
        {
            base.Awake();
            ResolveSaveManager();
        }

        private void Update()
        {
            if (!enableHotkeys || observedSaveManager == null || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[saveKey].wasPressedThisFrame)
            {
                observedSaveManager.Save();
            }
            else if (Keyboard.current[loadKey].wasPressedThisFrame)
            {
                observedSaveManager.TryLoad(out _);
            }
        }

        public void Observe(SaveManager saveManager)
        {
            observedSaveManager = saveManager;
        }

        protected override void BuildText(StringBuilder text)
        {
            if (observedSaveManager == null)
            {
                text.AppendLine("no SaveManager observed");
                return;
            }

            text.Append("Schema this build writes: ").AppendLine(SaveSchema.CurrentVersion.ToString());
            text.Append("Participants: ").AppendLine(observedSaveManager.ParticipantCount.ToString());

            AppendFile(text, "Save", observedSaveManager.MainPath, observedSaveManager.HasSaveFile);
            AppendFile(text, "Backup", observedSaveManager.BackupPath, observedSaveManager.HasBackupFile);

            text.Append("Last write: ");
            if (observedSaveManager.LastSaveUtc == default)
            {
                text.AppendLine("not written this session");
            }
            else if (observedSaveManager.LastSaveSucceeded)
            {
                text.Append("ok at ").AppendLine(observedSaveManager.LastSaveUtc.ToLocalTime().ToString("HH:mm:ss"));
            }
            else
            {
                text.Append("FAILED - ").AppendLine(observedSaveManager.LastSaveError);
            }

            text.Append("Last read: ").Append(observedSaveManager.LastLoadStatus);
            if (observedSaveManager.LastLoadStatus == SaveLoadStatus.Ok)
            {
                text.Append("  schema ").Append(observedSaveManager.LastLoadedSchemaVersion);
                if (observedSaveManager.LastLoadUsedBackup)
                {
                    text.Append("  (from backup)");
                }

                text.AppendLine();
            }
            else
            {
                text.AppendLine();
                if (!string.IsNullOrEmpty(observedSaveManager.LastLoadReason))
                {
                    text.Append("   ").AppendLine(observedSaveManager.LastLoadReason);
                }
            }

            if (enableHotkeys)
            {
                text.Append("Keys: ").Append(saveKey).Append(" save, ").Append(loadKey).AppendLine(" load");
            }
        }

        private static void AppendFile(StringBuilder text, string label, string path, bool exists)
        {
            text.Append(label).Append(": ");
            if (!exists)
            {
                text.AppendLine("none");
                return;
            }

            try
            {
                FileInfo info = new(path);
                text.Append(info.Length).Append(" bytes, ")
                    .AppendLine(info.LastWriteTime.ToString("HH:mm:ss"));
            }
            catch (Exception exception)
            {
                text.Append("unreadable - ").AppendLine(exception.Message);
            }
        }

        private void ResolveSaveManager()
        {
            if (observedSaveManager != null)
            {
                return;
            }

            observedSaveManager = FindFirstObjectByType<SaveManager>();

            if (observedSaveManager == null && createSaveManagerIfMissing)
            {
                observedSaveManager = gameObject.AddComponent<SaveManager>();
            }
        }
    }
}
