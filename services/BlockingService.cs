// MinimalBlockingAPI/TemporalBlockRemovalService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalBlockingAPI
{
    public class TemporalBlockRemovalService : BackgroundService
    {
        private readonly ILogger<TemporalBlockRemovalService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(5); 

        public TemporalBlockRemovalService(ILogger<TemporalBlockRemovalService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TemporalBlockRemovalService is starting.");

            using var timer = new PeriodicTimer(_period);

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("TemporalBlockRemovalService running at: {time}", DateTimeOffset.Now);
                    RemoveExpiredTemporalBlocks();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing TemporalBlockRemovalService work item.");
                }
            }
            _logger.LogInformation("TemporalBlockRemovalService is stopping.");
        }

        private void RemoveExpiredTemporalBlocks()
        {
            var now = DateTimeOffset.UtcNow;
            var keysToRemove = new List<Country>();

            foreach (var entry in CountriesCollection.countries)
            {
                if (entry.Value.Type == BlockedType.temporal &&
                    entry.Value.BlockedUntilUtc.HasValue &&
                    entry.Value.BlockedUntilUtc.Value <= now)
                {
                    keysToRemove.Add(entry.Key);
                }
            }

            if (keysToRemove.Any())
            {
                _logger.LogInformation("Found {Count} expired temporal blocks to remove.", keysToRemove.Count);
                foreach (var key in keysToRemove)
                {
                    if (CountriesCollection.countries.TryRemove(key, out _))
                    {
                        _logger.LogInformation("Removed expired temporal block for country: {CountryCode} ({CountryName})", key.Code, key.Name);
                    }
                    else
                    {

                        _logger.LogWarning("Failed to remove expired temporal block for country: {CountryCode} ({CountryName}). It might have been removed already.", key.Code, key.Name);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No expired temporal blocks found to remove at this time.");
            }
        }
    }
}