using System.Data;
using System.Data.Common;

namespace JavascriptProvider
{
    internal class EngineConfig
    {
        public string Engine => "Jint";
        public int LimitMemory { get; private set; } = 100;
        public int TimeoutInterval { get; private set; } = 30;
        public int MaxStatements { get; private set; } = 2000;

        private EngineConfig() { }

        public static EngineConfig Parse(string configString)
        {
            if (string.IsNullOrEmpty(configString))
            {
                throw new DataException("Missing required connection string.");
            }

            var sb = new DbConnectionStringBuilder();
            try
            {
                sb.ConnectionString = configString;
            }
            catch
            {
                throw new DataException("Bad connection string.");
            }

            var config = new EngineConfig();

            if (!sb.ContainsKey(nameof(Engine)))
            {
                throw new DataException("Missing required connection string section 'Engine'.");
            }
            if (!"Jint".Equals((string)sb[nameof(Engine)], StringComparison.InvariantCultureIgnoreCase))
            {
                throw new DataException("Engine must be 'Jint'.");
            }

            if (sb.TryGetValue(nameof(LimitMemory), out var limitMemoryObj) && limitMemoryObj.ToString() is var limitMemory)
            {
                if (int.TryParse(limitMemory, out var limitMemoryInt) && limitMemoryInt >= 10 && limitMemoryInt <= 1000)
                {
                    config.LimitMemory = limitMemoryInt;
                }
                else
                {
                    throw new DataException("LimitMemory must be an integer between 10 and 1000 (MB).");
                }
            }

            if (sb.TryGetValue(nameof(TimeoutInterval), out var timeoutIntervalObj) && timeoutIntervalObj.ToString() is var timeoutInterval)
            {
                if (int.TryParse(timeoutInterval, out var timeoutIntervalInt) && timeoutIntervalInt >= 10 && timeoutIntervalInt <= 600)
                {
                    config.TimeoutInterval = timeoutIntervalInt;
                }
                else
                {
                    throw new DataException("TimeoutInterval must be an integer between 10 and 600 (seconds).");
                }
            }

            if (sb.TryGetValue(nameof(MaxStatements), out var maxStatementsObj) && maxStatementsObj.ToString() is var maxStatements)
            {
                if (int.TryParse(maxStatements, out var maxStatementsInt) && maxStatementsInt >= 100 && maxStatementsInt <= 10000)
                {
                    config.MaxStatements = maxStatementsInt;
                }
                else
                {
                    throw new DataException("MaxStatements must be an integer between 100 and 10000.");
                }
            }

            return config;
        }
    }
}
