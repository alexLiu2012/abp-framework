using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Local;

namespace StackTools.LocalEventBus.ConsoleApp
{
    public class MyService : ITransientDependency
    {
        public ILocalEventBus EventBus { get; set; }

        public MyService()
        {
            EventBus = NullLocalEventBus.Instance;
        }

        public void SendInfo(string information)
        {
            EventBus.PublishAsync(new TheAEvent()
            {
                Name = "service",
                Description = information
            });

        }

    }
}
