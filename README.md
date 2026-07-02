[![.NET CI](https://github.com/theunschut/quest-board/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/theunschut/quest-board/actions/workflows/dotnet.yml) [![Docker](https://github.com/theunschut/quest-board/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/theunschut/quest-board/actions/workflows/docker-publish.yml) [![Release](https://github.com/theunschut/quest-board/actions/workflows/binary-release.yml/badge.svg)](https://github.com/theunschut/quest-board/actions/workflows/binary-release.yml)

# D&D Quest Board

A web application for D&D groups to manage quests, players, and campaigns. Dungeon Masters post quests with proposed dates, players sign up and vote on when they can join, and the DM finalizes the session. The app also includes a character and guild system, an in-game shop with a gold economy, and email notifications when quests are confirmed.

## Getting Started

The easiest way to run the Quest Board is with Docker Compose.

1. Clone the repository
2. Copy `.env.example` to `.env` and fill in your settings
3. Start the application:

```bash
docker-compose up -d
```

The app will be available at `http://localhost:8080`.

## Local Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project QuestBoard.Service
```

## Tech Stack

ASP.NET Core MVC · SQL Server · Entity Framework Core · Docker
