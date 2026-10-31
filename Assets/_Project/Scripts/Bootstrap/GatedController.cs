using Marchlands.Simulation.Core;

namespace Marchlands.Bootstrap
{
    /// <summary>ИИ, который думает, только когда включён (обучение включает противника по ходу уроков).</summary>
    public sealed class GatedController : IMatchController
    {
        private readonly IMatchController inner;

        public GatedController(IMatchController inner)
        {
            this.inner = inner;
        }

        public bool Active { get; set; }

        public void Update(Match match)
        {
            if (Active)
            {
                inner.Update(match);
            }
        }
    }
}
