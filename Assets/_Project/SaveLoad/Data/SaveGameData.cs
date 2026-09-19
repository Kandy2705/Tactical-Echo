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
        
        
        
        
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long timestampUtcTicks;
        public List<SaveRecord> records = new();
    }
}
