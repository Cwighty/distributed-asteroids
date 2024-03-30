# Task Schedule

## Week 0 (March 30)

- [x] setup repository with basic project structure
- [x] working dev and auto deployment environments
- [x] full circle frontend -> actor system -> signalR -> frontend
- [x] full monitoring infrastructure in both local development and deployed environments.
- [ ] login page / registration page - passwords are hashed and stored in Raft cluster storage API, with a user id
- [ ] create an abstraction to add traces to all actor system messages without much effort
- [ ] user can login and their user id is stored in a cookie (so on crash they can rejoin the game)

## Week 1 (April 6)

- [ ] user can create a lobby
- [ ] user can join existing lobby
- [ ] game can start and players can move their ships
- [ ] tests for loby creation, lobby joining, and ship movement (basically for any business logic implemented so far).
- [ ] at least one upgrade in the store (player can change ship color)
- [ ] players start with a certain amount of money
- [ ] upgrades and money are saved to the Raft cluster storage API by player id

## Week 2 (April 13)

- [ ] asteriods spawn and move
- [ ] asteroids can collide with ships
- [ ] ships can shoot bullets
- [ ] bullets can collide with asteroids
- [ ] players can die
- [ ] all players die and game ends
- [ ] tests for all new features

## Week 3 (April 20)

- [ ] run akka across a cluster with three nodes
- [ ] run multiple web font-ends
- [ ] handle redirecting of crashed lobby actors
- [ ] remaining power-ups and game logic
  - [ ] speed boost
  - [ ] extra health
  - [ ] different space background
- [ ] improved dashboards indicating metrics of game (e.g. lobbies in progress, players playing, damage/sec, in-game economy)
  - [ ] What is the full-system latency of a request to move?
  - [ ] Are game ticks happening every 100ms? or are they firing slower?
  - [ ] What is the CPU/RAM/Storage usage of the host servers?
  - [ ] How many players are currently in a game?
  - [ ] How many lobbies?
  - [ ] Inflation tracker of in-game currency

## Week 4 (April 27)

- [ ] finish up any remaining features
