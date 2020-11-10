using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Modularity;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace DistributedCachingDemo.WorkServiceApp
{
    [DependsOn(typeof(AbpCachingStackExchangeRedisModule))]
    public class MainModule : AbpModule
    {        
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpDistributedCacheOptions>(options =>
            {
                options.HideErrors = true;
            });

            Configure<RedisCacheOptions>(options =>
            {
                options.Configuration = "127.0.0.1";

                var configOpt = new ConfigurationOptions();
                configOpt.EndPoints.Add("127.0.0.1");
                configOpt.AsyncTimeout = 1000;

                options.ConfigurationOptions = configOpt;
                
            });
            
            context.Services.AddHostedService<Worker>();
        }
    }
}
