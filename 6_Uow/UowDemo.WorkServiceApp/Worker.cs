using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.Uow;

namespace UowDemo.WorkServiceApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IUnitOfWorkManager _uowManager;

        public Worker(ILogger<Worker> logger, IUnitOfWorkManager uowManager)
        {
            _logger = logger;
            _uowManager = uowManager;
        }

        //[UnitOfWork]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var uow = _uowManager.Begin())
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await Task.Delay(1000, stoppingToken);
                }

                await uow.CompleteAsync();
            }                
        }
    }
}
