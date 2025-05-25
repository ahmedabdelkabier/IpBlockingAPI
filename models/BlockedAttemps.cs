namespace MinimalBlockingAPI
{
    public class BlockedAttempt
    {
        private int _id;
        public int Id { get => _id; }
        public string IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public string CountryCode { get; set; }
        public string BlockedStatus { get; set; }

        public BlockedAttempt(string? ipAddress, string countryCode, string blockedStatus)
        {
            IpAddress = ipAddress;
            CountryCode = countryCode;
            BlockedStatus = blockedStatus;
            Timestamp = DateTime.Now;
        }

        public BlockedAttempt(int id, string? ipAddress, string countryCode, string blockedStatus)
        {
            _id = id;
            IpAddress = ipAddress;
            CountryCode = countryCode;
            BlockedStatus = blockedStatus;
            Timestamp = DateTime.Now;
        }
    }
}