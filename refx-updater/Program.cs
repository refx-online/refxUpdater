using System;
using System.Threading.Tasks;
using refx_updater.Services;

namespace refx_updater
{
    class Program
    {
        [STAThread]
        static void Main() 
            => MainAsync().GetAwaiter().GetResult();

        static async Task MainAsync() 
            => await new Updater().RunAsync();
    }
}