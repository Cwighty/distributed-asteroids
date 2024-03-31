using Akka.Actor;
using Asteroids.Shared.Storage;

namespace Asteroids.Shared.Accounts;

public class UserExistsCheckCompleted
{
    public bool Exists { get; }
    public CreateNewAccountCommand OriginalCommand { get; }

    public UserExistsCheckCompleted(bool exists, CreateNewAccountCommand originalCommand)
    {
        Exists = exists;
        OriginalCommand = originalCommand;
    }
}

public class CompareAndSwapCompleted
{
    public bool Success { get; }
    public CreateNewAccountCommand OriginalCommand { get; }

    public CompareAndSwapCompleted(bool success, CreateNewAccountCommand originalCommand)
    {
        Success = success;
        OriginalCommand = originalCommand;
    }
}

public class AccountCreationSaga : ReceiveActor
{
    // References to other actors
    private readonly IActorRef _storageActor;
    private readonly IActorRef _accountEventEmitter;
    private CreateNewAccountCommand _currentCommand;

    public AccountCreationSaga(IActorRef storageActor, IActorRef accountEventEmitter)
    {
        _storageActor = storageActor;
        _accountEventEmitter = accountEventEmitter;

        Receive<CreateNewAccountCommand>(cmd => HandleCreateAccountCommand(cmd));
        Receive<StrongGetResponse>(response => HandleStrongGetResponse(response));
        // Other message handlers as before
    }

    private void HandleCreateAccountCommand(CreateNewAccountCommand cmd)
    {
        _currentCommand = cmd; 
        var requestId = Guid.NewGuid();
        _storageActor.Tell(new StrongGetQuery(requestId, $"user:{cmd.Username}"));
    }

    private void HandleStrongGetResponse(StrongGetResponse response)
    {
        var userExists = response.value != null; // Assuming a non-null response means the user exists
        if (userExists)
        {
            // User already exists, notify client
            _accountEventEmitter.Tell(new AccountCreatedEvent(_currentCommand.ConnectionId, false, "User already exists."));
        }
        else
        {
            // Proceed with account creation, for example, by calling an external service or directly storing the account
        }
    }

    private void FinalizeAccountCreation(CompareAndSwapCompleted msg)
    {
        if (msg.Success)
        {
            _accountEventEmitter.Tell(new AccountCreatedEvent(msg.OriginalCommand.ConnectionId, true, "Account created successfully."));
        }
        else
        {
            _accountEventEmitter.Tell(new AccountCreatedEvent(msg.OriginalCommand.ConnectionId, false, "Account creation failed."));
        }
    }
}

