using System;
using System.Collections.Generic;

namespace TacticalEcho.SaveLoad.Data
{
    [Serializable]
    public sealed class SaveRecord
    {
        public string stableId;
        public string type;
        public string json;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        /// <summary>
        /// Schema this build writes. Every save is stamped with it, and every load compares
        /// against it before the data is trusted - see <see cref="SaveSchema"/>.
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long timestampUtcTicks;
        public List<SaveRecord> records = new();
    }
}
