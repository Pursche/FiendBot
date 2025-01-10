
namespace FiendBot.Commands
{
    public interface ICommand
    {
        Task<bool> CanExecute(BotContext context, string message);
        Task Execute(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e);
    }
}
