# Distributed Asteroids

## Description

This project is a distributed version of the classic game Asteroids. It is implemented using the actor model with Akka.NET. The game is played in a web browser and the game state is managed by a cluster of Akka.NET actors. The players interact with the game through a Blazor front end that communicates with the actor system through a SignalR message server. The game state is saved to a Raft cluster storage API.

## More Information

- [Project Design](docs/PLAN.md)
- [Task List](docs/TASKS.md)
