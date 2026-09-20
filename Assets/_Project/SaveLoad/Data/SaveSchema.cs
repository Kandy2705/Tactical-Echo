using System;
using System.Collections.Generic;

namespace TacticalEcho.SaveLoad.Data
{
    public enum SaveLoadStatus
    {
        NotAttempted,
        Ok,
        MissingFile,
        Unreadable,
        Invalid,
        UnsupportedVersion
    }






    public static class SaveSchema
    {
        public const int CurrentVersion = SaveGameData.CurrentSchemaVersion;






        private static readonly Dictionary<int, Action<SaveGameData>> Migrations = new()
        {
            { 0, MigrateUnversionedToV1 }
        };






        public static bool TryPrepare(SaveGameData data, out SaveLoadStatus status, out string reason)
        {
            if (data == null)
            {
                status = SaveLoadStatus.Unreadable;
                reason = "save parsed to null";
                return false;
            }

            if (data.schemaVersion > CurrentVersion)
            {
                status = SaveLoadStatus.UnsupportedVersion;
                reason = $"written by a newer build (schema {data.schemaVersion}, this build reads {CurrentVersion})";
                return false;
            }

            if (!TryMigrate(data, out reason))
            {
                status = SaveLoadStatus.Invalid;
                return false;
            }

            if (!TryValidate(data, out reason))
            {
                status = SaveLoadStatus.Invalid;
                return false;
            }

            status = SaveLoadStatus.Ok;
            reason = string.Empty;
            return true;
        }

        private static bool TryMigrate(SaveGameData data, out string reason)
        {


            if (data.schemaVersion < 0)
            {
                data.schemaVersion = 0;
            }

            int guard = 0;
            while (data.schemaVersion < CurrentVersion)
            {
                int from = data.schemaVersion;
                if (!Migrations.TryGetValue(from, out Action<SaveGameData> step))
                {
                    reason = $"no migration step from schema {from} to {from + 1}";
                    return false;
                }

                step(data);
                data.schemaVersion = from + 1;

                if (++guard > 64)
                {
                    reason = "migration did not converge";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryValidate(SaveGameData data, out string reason)
        {
            if (data.schemaVersion != CurrentVersion)
            {
                reason = $"schema {data.schemaVersion} after migration, expected {CurrentVersion}";
                return false;
            }

            if (data.records == null)
            {
                reason = "records list is missing";
                return false;
            }

            if (data.timestampUtcTicks < DateTime.MinValue.Ticks || data.timestampUtcTicks > DateTime.MaxValue.Ticks)
            {
                reason = $"timestamp {data.timestampUtcTicks} is not a representable date";
                return false;
            }

            HashSet<string> seenIds = new(StringComparer.Ordinal);
            for (int index = 0; index < data.records.Count; index++)
            {
                SaveRecord record = data.records[index];
                if (record == null)
                {
                    reason = $"record {index} is null";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(record.stableId))
                {
                    reason = $"record {index} has no stableId, so it could never be restored";
                    return false;
                }

                if (!seenIds.Add(record.stableId))
                {
                    reason = $"duplicate stableId \"{record.stableId}\", restore would be ambiguous";
                    return false;
                }



                record.json ??= string.Empty;
                record.type ??= string.Empty;
            }

            reason = string.Empty;
            return true;
        }





        private static void MigrateUnversionedToV1(SaveGameData data)
        {
            data.records ??= new List<SaveRecord>();
        }
    }
}
