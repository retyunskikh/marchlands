namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Приказ игрока или ИИ. Единственный способ изменить состояние симуляции извне:
    /// команда ставится в очередь на конкретный тик и выполняется в начале этого тика.
    /// </summary>
    public interface ICommand
    {
        void Execute(Match match);
    }
}
