using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;

namespace DistributedCachingDemo.WorkServiceApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IDistributedCache<TimeInfo> _cache;

        public Worker(ILogger<Worker> logger, IDistributedCache<TimeInfo> cache)
        {
            _logger = logger;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //try
            //{
                while (!stoppingToken.IsCancellationRequested)
                {
                    var timeInfo = await _cache.GetOrAddAsync(
                        "now",
                        () => Task.FromResult(new TimeInfo()),
                        () => new DistributedCacheEntryOptions()
                        {
                            AbsoluteExpiration = DateTime.Now.AddSeconds(30)
                        });                    

                    _logger.LogInformation("Worker running at: {time}", timeInfo.Time);

                    await Task.Delay(2000, stoppingToken);
                }
            //}
            //catch(Exception ex)
            //{
            //    Console.WriteLine(ex.Message);
            //    Console.WriteLine("out by throw");
            //}
            
        }        
    }
}
