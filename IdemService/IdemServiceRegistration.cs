using Beamable;
using Beamable.Common.Dependencies;

namespace Idem
{
    [BeamContextSystem]
    public class IdemServiceRegistration
    {
        [RegisterBeamableDependencies]
        public static void Register(IDependencyBuilder builder)
        {
            builder.AddSingleton<IdemService>();
        }
    }
    
    public static class IdemServiceExtension
    {
        public static IdemService IdemService(this BeamContext ctx) => ctx.ServiceProvider.GetService<IdemService>();
    }
}
