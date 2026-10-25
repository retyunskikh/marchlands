namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Участник матча, принимающий решения (ИИ). Вызывается в начале каждого тика, до исполнения команд.
    /// Состояние только читает; влиять на матч может только командами через <see cref="Match.Enqueue(int, ICommand)"/>.
    /// </summary>
    public interface IMatchController
    {
        void Update(Match match);
    }
}
