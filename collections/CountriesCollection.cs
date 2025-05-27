// MinimalBlockingAPI/CountriesCollection.cs
using System.Collections.Concurrent;

namespace MinimalBlockingAPI
{
    public enum BlockedType
    {
        temporal, permanent
    }

    public class BlockedInfo
    {
        public BlockedType Type { get; } // Made public get
        public DateTimeOffset? BlockedUntilUtc { get; } // 

        public BlockedInfo(BlockedType type, DateTimeOffset? blockedUntilUtc = null)
        {
            this.Type = type;
            this.BlockedUntilUtc = blockedUntilUtc;
        }
    }

    public static class CountriesCollection
    {
       private static readonly HashSet<string> _validCountriesCode = new HashSet<string>(StringComparer.OrdinalIgnoreCase){
    "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR",
    "AS", "AT", "AU", "AW", "AX", "AZ", "BA", "BB", "BD", "BE",
    "BF", "BG", "BH", "BI", "BJ", "BL", "BM", "BN", "BO", "BQ",
    "BR", "BS", "BT", "BV", "BW", "BY", "BZ", "CA", "CC", "CD",
    "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN", "CO", "CR",
    "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM",
    "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI",
    "FJ", "FK", "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF",
    "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS",
    "GT", "GU", "GW", "GY", "HK", "HM", "HN", "HR", "HT", "HU",
    "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR", "IS", "IT",
    "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN",
    "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC", "LI", "LK",
    "LR", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME",
    "MF", "MG", "MH", "MK", "ML", "MM", "MN", "MO", "MP", "MQ",
    "MR", "MS", "MT", "MU", "MV", "MW", "MX", "MY", "MZ", "NA",
    "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP", "NR", "NU",
    "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM",
    "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS",
    "RU", "RW", "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI",
    "SJ", "SK", "SL", "SM", "SN", "SO", "SR", "SS", "ST", "SV",
    "SX", "SY", "SZ", "TC", "TD", "TF", "TG", "TH", "TJ", "TK",
    "TL", "TM", "TN", "TO", "TR", "TT", "TV", "TW", "TZ", "UA",
    "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
    "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW","UK"
};
        public static ConcurrentDictionary<Country, BlockedInfo> countries = new ConcurrentDictionary<Country, BlockedInfo>();

        public static void addNewBlockedCountry(Country country)
        {
      
            var key = new Country(country.Code, country.Name); 
            
            countries.AddOrUpdate(
                key,
                new BlockedInfo(BlockedType.permanent),
                (k, oldValue) => new BlockedInfo(BlockedType.permanent) 
            );
        }

        public static bool deleteBlockedCountry(string code)
        {

            var countryKey = new Country(code, ""); 
            return countries.TryRemove(countryKey, out _);
        }

        public static async Task<string> getCountryCode(string? ip)
        {
            if (string.IsNullOrEmpty(ip)) return "UNKNOWN";
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0"); // Good practice
                var response = await client.GetStringAsync($"https://ipapi.co/{ip}/country_code/");
                return response.Trim().ToUpperInvariant(); // Normalize
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        public static async Task<string> getCountryName(string? ip)
        {
            if (string.IsNullOrEmpty(ip)) return "UNKNOWN";
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0"); // Good practice
                var response = await client.GetStringAsync($"https://ipapi.co/{ip}/country_name/");
                return response.Trim();
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        public static bool isValidCountryCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 2)
            {
                return false;
            }
            return _validCountriesCode.Contains(code);
        }

        public static async Task addTemporaryBlockedCountry(string code, string? ip, int durationInMinutes)
        {
            var countryName = await getCountryName(ip);
            var expirationTime = DateTimeOffset.UtcNow.AddMinutes(durationInMinutes);
            

            var key = new Country(code.ToUpperInvariant(), countryName);

            countries.AddOrUpdate(
                key,
                new BlockedInfo(BlockedType.temporal, expirationTime),
                (k, oldValue) => new BlockedInfo(BlockedType.temporal, expirationTime) // Update if exists
            );
        }
    }
}