# Idem integration into Unity Beamable
This repository contains Idem.gg API implemented as Unity package providing Beamable service.

## Installation
* [Install Beamable](https://beamable.com)
* Create an empty microservice with Beamable
* Configure mandatory parameters in the Beamable portal -> Your project -> Operate -> Config -> 'Idem' namespace
* Install this package either from git link [https://github.com/idem-matchmaking/integration-beamable.git](https://github.com/idem-matchmaking/integration-beamable) or by copying the source into the project
* Deploy `IdemMicroservice`
* Use `IdemService` methods to start/stop matchmaking and report game results

## IdemService
### StartMatchmaking
* Starts the matchmaking process for the client
* Takes game mode name and available for the player servers list
* Game mode must be one of the specified in the config game modes list
* Servers list values can be any valid string: server names, ip addresses, etc
* If there is only one server for your game, you can use any value as long as it is the same for all the clients

### StopMatchmaking
* Stops matchmaking process and cancels unconfirmed match if any
* When a match is ready, use `CompleteMatch` to finish it

### CompleteMatch
Takes game length in seconds, map `teamId` -> `team rank`, 0 means the best result, map `playerId` -> `score` for score per player

### IsMatchmaking
Returns `true` if matchmaking is in progress

### CurrentMatchInfo
Contains all the details of the currently found match if any

### event OnMatchmakingStopped
Called when the matchmaking process is stopped by the server

### event OnMatchFound
Called when there is a match candidate but not all players confirmed 

### event OnMatchReady
Called when the match was confirmed by all the players and gameplay can be started

### MatchInfo
* `ready` - `true` if the match was confirmed and gameplay can be started
* `gameMode` - game mode name of the match
* `matchId` - unique id string of the match
* `server` - server chosen for the match
* `player` - list of players with their `teamId` and `playerId`
