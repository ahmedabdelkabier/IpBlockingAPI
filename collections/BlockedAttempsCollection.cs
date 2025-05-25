namespace MinimalBlockingAPI
{
    public static class BlockedAttempsCollection
    {
        public static List<BlockedAttempt> blockedAttempts = new List<BlockedAttempt>();

        public static void addNewBlockedAttempt(BlockedAttempt attempt)
        {
            if (blockedAttempts.Count() > 0)
            {
                var lastID = blockedAttempts.Max(x => x.Id);
                blockedAttempts.Add(new BlockedAttempt(lastID + 1, attempt.IpAddress, attempt.CountryCode, attempt.BlockedStatus));
            }
            else
            {
                blockedAttempts.Add(new BlockedAttempt(1, attempt.IpAddress, attempt.CountryCode, attempt.BlockedStatus));
            }
        }

        public static bool deleteBlockedAttempt(int id)
        {
            var attempt = blockedAttempts.FirstOrDefault(a => a.Id == id);
            if (attempt != null)
            {
                blockedAttempts.Remove(attempt);
                return true;
            }
            return false;
        }
    }
}