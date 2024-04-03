using Akka.Actor;
using Akka.Event;

namespace Asteroids.Shared.Contracts;

using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
public class DeadLetterActor : ReceiveActor
{
    private readonly IServiceScope scope;
    private ILogger<DeadLetterActor> logger { get; }
    public DeadLetterActor(IServiceProvider serviceProvider)
    {
        scope = serviceProvider.CreateScope();
        logger = scope.ServiceProvider.GetRequiredService<ILogger<DeadLetterActor>>();
        Receive<DeadLetter>(m =>
        {
            DeadLetterLogs.DeadLetterMessage(logger, m.Sender, m.Recipient, m.Message);
        });
        Receive<UnhandledMessage>(m =>
        {
            DeadLetterLogs.UnhandledMessage(logger, m.Sender, m.Recipient, m.Message);
        });
    }
}
public partial class DeadLetterLogs
{
    [LoggerMessage(
      Level = Microsoft.Extensions.Logging.LogLevel.Error,
      Message = "Dead letter: \nSender {Sender}, \nRecipient {Recipient}, \nMessage: {Message}"
    )]
    public static partial void DeadLetterMessage(ILogger logger, IActorRef Sender, IActorRef Recipient, object Message);
    [LoggerMessage(
      Level = Microsoft.Extensions.Logging.LogLevel.Error,
      Message = "Unhandled letter: \nSender {Sender}, \nRecipient {Recipient}, \nMessage: {Message}"
    )]
    public static partial void UnhandledMessage(ILogger logger, IActorRef Sender, IActorRef Recipient, object Message);
}
